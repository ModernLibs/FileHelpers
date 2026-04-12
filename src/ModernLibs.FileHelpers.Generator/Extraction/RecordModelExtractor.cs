// MA0051: ExtractDelimited is a single-responsibility transform; extracting sub-methods would
// require passing many local variables, reducing readability without improving maintainability.
#pragma warning disable MA0051
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernLibs.FileHelpers.Generator.Models;

namespace ModernLibs.FileHelpers.Generator.Extraction;

internal static class RecordModelExtractor
{
    internal static RecordModel? ExtractDelimited(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

        // Skip file-scoped types — they are not accessible outside the declaring file,
        // so emitting a descriptor that references them by fully-qualified name would
        // produce CS0234 compile errors in the generated code.
        if (typeSymbol.IsFileLocal) return null;

        // Extract all type information as strings — no ISymbol escapes this method
        string ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();
        string typeName = typeSymbol.Name;
        string typeKind = typeSymbol.IsRecord
            ? typeSymbol.IsValueType ? "record struct" : "record class"
            : typeSymbol.IsValueType ? "struct" : "class";
        bool isReadOnly = typeSymbol.IsReadOnly;

        // Extract [DelimitedRecord] constructor arg (the delimiter string)
        var attr = ctx.Attributes[0];
        string delimiter = ",";
        if (attr.ConstructorArguments.Length > 0
            && attr.ConstructorArguments[0].Value is string sep)
        {
            delimiter = sep;
        }

        // Validate: empty or null delimiter gets null return (diagnostic emitted later, Phase 4)
        if (string.IsNullOrEmpty(delimiter)) return null;

        // Extract fields: public instance fields and properties only
        var fields = ImmutableArray.CreateBuilder<FieldModel>();
        int implicitOrder = 0;

        foreach (var member in typeSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member.IsStatic) continue;
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.IsImplicitlyDeclared) continue;  // Skip compiler-generated members (e.g., EqualityContract on record class)

            ITypeSymbol? memberType = null;
            bool isInitOnly = false;
            switch (member)
            {
                case IFieldSymbol field when field.AssociatedSymbol is null: // skip backing fields
                    memberType = field.Type;
                    break;
                case IPropertySymbol prop when !prop.IsIndexer:
                    memberType = prop.Type;
                    isInitOnly = prop.SetMethod?.IsInitOnly ?? false;
                    break;
                default:
                    continue;
            }

            // Extract type info as strings before any symbol escapes
            string memberName = member.Name;
            string memberTypeName = memberType.ToDisplayString();
            string fullTypeName = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            int declarationIndex = implicitOrder++;
            int order = declarationIndex; // default; overridden by [FieldOrder] below
            string? trimMode = null;
            bool isOptional = false;
            bool isHidden = false;
            bool isValueDiscarded = false;

            // Scan member attributes for field-level overrides
            foreach (var mAttr in member.GetAttributes())
            {
                ct.ThrowIfCancellationRequested();
                string? attrName = mAttr.AttributeClass?.ToDisplayString();
                // WR-01: AttributeClass is null for unresolved / error-symbol attributes;
                // skip them rather than falling into the switch with a null key.
                if (attrName is null) continue;
                switch (attrName)
                {
                    case "ModernLibs.FileHelpers.FieldOrderAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int o:
                        order = o;
                        break;
                    case "ModernLibs.FileHelpers.FieldTrimAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int tm:
                        trimMode = tm.ToString(CultureInfo.InvariantCulture);
                        break;
                    case "ModernLibs.FileHelpers.FieldOptionalAttribute":
                        isOptional = true;
                        break;
                    case "ModernLibs.FileHelpers.FieldHiddenAttribute":
                    case "ModernLibs.FileHelpers.FieldNotInFileAttribute":
                        isHidden = true;
                        break;
                    case "ModernLibs.FileHelpers.FieldValueDiscardedAttribute":
                        isValueDiscarded = true;
                        break;
                }
            }

            fields.Add(new FieldModel(memberName, memberTypeName, fullTypeName,
                declarationIndex, order, trimMode, isOptional, isHidden, isValueDiscarded,
                IsInitOnly: isInitOnly));
        }

        // Sort by order for deterministic output (SGEN-11).
        // ThenBy(DeclarationIndex) is the tiebreaker when two fields share the same [FieldOrder]
        // value — ensures stable, declaration-order output rather than relying on LINQ's implicit
        // sort stability (WR-02).
        var sortedArray = fields
            .OrderBy(static f => f.Order)
            .ThenBy(static f => f.DeclarationIndex)
            .ToImmutableArray();

        return new RecordModel(
            ns, typeName, typeKind, isReadOnly, "Delimited", delimiter,
            new EquatableArray<FieldModel>(sortedArray));
    }

    internal static RecordModel? ExtractFixedLength(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;
        if (typeSymbol.IsFileLocal) return null;

        string ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();
        string typeName = typeSymbol.Name;
        string typeKind = typeSymbol.IsRecord
            ? typeSymbol.IsValueType ? "record struct" : "record class"
            : typeSymbol.IsValueType ? "struct" : "class";
        bool isReadOnly = typeSymbol.IsReadOnly;

        // Read [FixedLengthRecord] FixedMode argument (int enum value, 0-3)
        var attr = ctx.Attributes[0];
        string fixedMode = "ExactLength"; // default when no argument
        if (attr.ConstructorArguments.Length > 0
            && attr.ConstructorArguments[0].Value is int fm)
        {
            fixedMode = fm switch
            {
                0 => "ExactLength",
                1 => "AllowMoreChars",
                2 => "AllowLessChars",
                3 => "AllowVariableLength",
                _ => "ExactLength",
            };
        }

        var fields = ImmutableArray.CreateBuilder<FieldModel>();
        int implicitOrder = 0;

        foreach (var member in typeSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member.IsStatic) continue;
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.IsImplicitlyDeclared) continue;  // Skip compiler-generated members (e.g., EqualityContract on record class)

            ITypeSymbol? memberType = null;
            bool isInitOnly = false;
            switch (member)
            {
                case IFieldSymbol field when field.AssociatedSymbol is null:
                    memberType = field.Type;
                    break;
                case IPropertySymbol prop when !prop.IsIndexer:
                    memberType = prop.Type;
                    isInitOnly = prop.SetMethod?.IsInitOnly ?? false;
                    break;
                default:
                    continue;
            }

            string memberName = member.Name;
            string memberTypeName = memberType.ToDisplayString();
            string fullTypeName = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            int declarationIndex = implicitOrder++;
            int order = declarationIndex;
            string? trimMode = null;
            bool isOptional = false;
            bool isHidden = false;
            bool isValueDiscarded = false;
            int fixedLength = -1; // sentinel: [FieldFixedLength] not found

            foreach (var mAttr in member.GetAttributes())
            {
                ct.ThrowIfCancellationRequested();
                string? attrName = mAttr.AttributeClass?.ToDisplayString();
                if (attrName is null) continue;
                switch (attrName)
                {
                    case "ModernLibs.FileHelpers.FieldOrderAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int o:
                        order = o;
                        break;
                    case "ModernLibs.FileHelpers.FieldFixedLengthAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int fl:
                        fixedLength = fl;
                        break;
                    case "ModernLibs.FileHelpers.FieldTrimAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int tm:
                        trimMode = tm.ToString(CultureInfo.InvariantCulture);
                        break;
                    case "ModernLibs.FileHelpers.FieldOptionalAttribute":
                        isOptional = true;
                        break;
                    case "ModernLibs.FileHelpers.FieldHiddenAttribute":
                    case "ModernLibs.FileHelpers.FieldNotInFileAttribute":
                        isHidden = true;
                        break;
                    case "ModernLibs.FileHelpers.FieldValueDiscardedAttribute":
                        isValueDiscarded = true;
                        break;
                }
            }

            fields.Add(new FieldModel(memberName, memberTypeName, fullTypeName,
                declarationIndex, order, trimMode, isOptional, isHidden, isValueDiscarded,
                FixedLength: fixedLength, IsInitOnly: isInitOnly));
        }

        var sortedArray = fields
            .OrderBy(static f => f.Order)
            .ThenBy(static f => f.DeclarationIndex)
            .ToImmutableArray();

        return new RecordModel(
            ns, typeName, typeKind, isReadOnly, "FixedLength", Delimiter: string.Empty,
            new EquatableArray<FieldModel>(sortedArray),
            FixedMode: fixedMode);
    }
}
