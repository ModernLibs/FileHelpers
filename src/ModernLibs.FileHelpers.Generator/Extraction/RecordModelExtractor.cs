// MA0051: Extract is a single-responsibility transform; splitting it further would require passing
// many local variables, reducing readability without improving maintainability.
#pragma warning disable MA0051
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator.Models;

namespace ModernLibs.FileHelpers.Generator.Extraction;

/// <summary>
/// Turns a <c>[DelimitedRecord]</c> / <c>[FixedLengthRecord]</c> type symbol into an equatable
/// <see cref="RecordModel"/>. Every symbol-dependent decision (type classification, construction
/// strategy, assignability, diagnostics) is made here so the emitter only works on strings.
/// </summary>
internal static class RecordModelExtractor
{
    private const string AttributeNamespace = "ModernLibs.FileHelpers";
    private const string DelimitedRecordAttribute = "ModernLibs.FileHelpers.DelimitedRecordAttribute";
    private const string FixedLengthRecordAttribute = "ModernLibs.FileHelpers.FixedLengthRecordAttribute";

    internal static RecordModel? ExtractDelimited(GeneratorAttributeSyntaxContext ctx, CancellationToken ct) =>
        ExtractSafely(ctx, isFixed: false, ct);

    internal static RecordModel? ExtractFixedLength(GeneratorAttributeSyntaxContext ctx, CancellationToken ct) =>
        ExtractSafely(ctx, isFixed: true, ct);

    /// <summary>
    /// An unexpected failure while extracting one record becomes a model carrying
    /// <see cref="RecordModel.Failure"/> (reported as MLFH020) instead of an exception that would drop
    /// every generated descriptor.
    /// </summary>
    private static RecordModel? ExtractSafely(GeneratorAttributeSyntaxContext ctx, bool isFixed, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        try
        {
            return Extract(type, ctx.Attributes[0], ctx.SemanticModel.Compilation, isFixed, bindToTree: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RecordModel(
                string.Empty, type.Name, "class", false, isFixed ? "FixedLength" : "Delimited", string.Empty, default,
                Location: LocationInfo.From(type.Locations.FirstOrDefault(), bindToTree: false),
                Failure: ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// Extracts the model of one record type. <paramref name="bindToTree"/> keeps the syntax trees in the
    /// locations; only the analyzer sets it (see <see cref="LocationInfo"/>).
    /// </summary>
    internal static RecordModel? Extract(INamedTypeSymbol typeSymbol, AttributeData attr, Compilation compilation, bool isFixed, bool bindToTree, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        LocationInfo? recordLocation = LocationInfo.From(attr.ApplicationSyntaxReference, bindToTree, ct)
                                       ?? LocationInfo.From(typeSymbol.Locations.FirstOrDefault(), bindToTree);

        string ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        string typeName = typeSymbol.Name;
        string displayName = typeSymbol.ToDisplayString();
        string typeKind = typeSymbol.IsRecord
            ? typeSymbol.IsValueType ? "record struct" : "record class"
            : typeSymbol.IsValueType ? "struct" : "class";
        string recordKind = isFixed ? "FixedLength" : "Delimited";

        var shell = new RecordModel(
            ns, typeName, typeKind, typeSymbol.IsReadOnly, recordKind, isFixed ? string.Empty : ",",
            default,
            FullyQualifiedName: typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            HintName: BuildHintName(typeSymbol, isFixed),
            Location: recordLocation,
            IsReferenceable: IsReferenceableRecordType(typeSymbol, compilation));

        // A type with both record attributes is reported once (by the delimited arm).
        if (HasAttribute(typeSymbol, DelimitedRecordAttribute) && HasAttribute(typeSymbol, FixedLengthRecordAttribute))
        {
            return isFixed ? null : Failed(shell, DiagnosticInfo.Create("MLFH011", recordLocation, displayName));
        }

        // Generic records (or records nested in generic types) cannot be described statically.
        for (INamedTypeSymbol? t = typeSymbol; t is not null; t = t.ContainingType)
        {
            if (t.TypeParameters.Length > 0)
                return Failed(shell, DiagnosticInfo.Create("MLFH016", recordLocation, displayName));
        }

        // File-local types cannot be referenced from the generated file; they are skipped silently
        // (attribute-only usage, as before).
        if (typeSymbol.IsFileLocal) return null;

        // The descriptor lives in another file of the same assembly.
        if (!IsAccessibleRecordType(typeSymbol, compilation))
            return Failed(shell, DiagnosticInfo.Create("MLFH017", recordLocation, displayName));

        // A ref struct cannot be a generic type argument (engines, registry, IRecordDescriptor<T>).
        if (typeSymbol.IsRefLikeType)
            return Failed(shell, DiagnosticInfo.Create("MLFH027", recordLocation, displayName));

        // Empty record delimiter.
        string delimiter = string.Empty;
        if (!isFixed)
        {
            delimiter = ",";
            if (attr.ConstructorArguments.Length > 0)
            {
                if (attr.ConstructorArguments[0].Value is string sep && sep.Length > 0)
                    delimiter = sep;
                else
                    return Failed(shell, DiagnosticInfo.Create("MLFH012", recordLocation, "[DelimitedRecord]", displayName));
            }
        }

        string fixedMode = "ExactLength";
        if (isFixed && attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int fm)
        {
            fixedMode = fm switch
            {
                1 => "AllowMoreChars",
                2 => "AllowLessChars",
                3 => "AllowVariableLength",
                _ => "ExactLength",
            };
        }

        var (igFirst, igLast, igEmpty, igSpaces, commentMarker, commentAnyPlace, condition, conditionPattern) = ExtractRecordLevelFilters(typeSymbol, ct);

        // [DelimitedRecord(MultilineMode = ...)] overrides [FieldQuoted] multiline modes only when set.
        int? explicitMultiline = null;
        if (!isFixed)
        {
            foreach (var named in attr.NamedArguments)
            {
                if (string.Equals(named.Key, "MultilineMode", StringComparison.Ordinal) && named.Value.Value is int mm)
                    explicitMultiline = mm;
            }
        }

        bool implementsNotifyRead = ImplementsINotifyRead(typeSymbol, compilation);

        // ---- members (base types first, hidden members excluded, assignability checked) ----
        var diagnostics = new List<DiagnosticInfo>();
        var members = new List<MemberData>();
        var hiddenNames = new HashSet<string>(StringComparer.Ordinal);
        var hiddenOrders = new HashSet<int>();
        foreach (ISymbol member in GetAllMembers(typeSymbol, ct))
        {
            ct.ThrowIfCancellationRequested();

            // The descriptor is generated in the same assembly, so public, internal and protected
            // internal members are mapped. Private / protected members cannot be accessed, and a property
            // without an accessible getter cannot be formatted: they are not part of the layout. They are
            // reported as errors when they carry FileHelpers field attributes, and as warnings when
            // FileHelpers 3.x (which maps every instance field through reflection) would map them.
            string? unmappedReason = null;
            if (!member.IsStatic && !member.IsImplicitlyDeclared)
            {
                if (!compilation.IsSymbolAccessibleWithin(member, compilation.Assembly))
                    unmappedReason = AccessibilityText(member.DeclaredAccessibility);
                else if (member is IPropertySymbol { IsIndexer: false, ReturnsByRef: false, ReturnsByRefReadonly: false } readable
                         && (readable.GetMethod is null || !compilation.IsSymbolAccessibleWithin(readable.GetMethod, compilation.Assembly)))
                    unmappedReason = readable.GetMethod is null ? "write-only" : "readable only through a " + AccessibilityText(readable.GetMethod.DeclaredAccessibility) + " get accessor";
            }
            if (unmappedReason is not null)
            {
                LocationInfo? memberLocation = LocationInfo.From(member.Locations.FirstOrDefault(), bindToTree) ?? recordLocation;
                if (HasLayoutAttribute(member))
                    diagnostics.Add(DiagnosticInfo.Create("MLFH023", memberLocation, member.Name, displayName, unmappedReason));
                else if (IsMappedByLegacyFileHelpers(member))
                    diagnostics.Add(DiagnosticInfo.Create("MLFH028", memberLocation, member.Name, displayName, unmappedReason));
                continue;
            }

            MemberData? data = ReadMember(member, compilation, bindToTree, ct);
            if (data is null) continue;
            if (data.IsHidden)
            {
                hiddenNames.Add(member.Name);
                if (data.Order is int hiddenOrder) hiddenOrders.Add(hiddenOrder);
                continue;
            }
            members.Add(data);
        }

        // ---- construction strategy ----
        var ctorArgs = new List<(string Parameter, MemberData? Member, string? Default)>();
        string? failure = ChooseConstruction(typeSymbol, members, hiddenNames, compilation, out bool useConstructor, out IMethodSymbol? constructor, ctorArgs);
        if (failure is not null)
            return Failed(shell, DiagnosticInfo.Create("MLFH014", recordLocation, displayName, failure));

        // ---- final mapping: drop members that cannot be assigned ----
        var mapped = new List<MemberData>();
        foreach (var data in members)
        {
            bool assignable = data.IsCtorBound
                || data.Assign == FieldAssignKind.Setter
                || data.Assign == FieldAssignKind.Init;
            if (!assignable)
            {
                if (data.IsValueDiscarded)
                {
                    data.Assign = FieldAssignKind.None; // read and discarded, never stored
                }
                else
                {
                    if (data.HasFieldAttribute)
                        diagnostics.Add(DiagnosticInfo.Create("MLFH015", data.Location, data.Symbol.Name, displayName));
                    continue;
                }
            }
            mapped.Add(data);
        }

        // [FieldOrder] on all mapped members or on none (3.x rule).
        if (mapped.Exists(static m => m.Order is not null))
        {
            foreach (var data in mapped)
            {
                if (data.Order is null)
                    diagnostics.Add(DiagnosticInfo.Create("MLFH024", data.Location, data.Symbol.Name, displayName));
            }
        }

        // ---- field models ----
        var fields = ImmutableArray.CreateBuilder<FieldModel>(mapped.Count);
        bool hasUnresolvedTypes = false;
        for (int i = 0; i < mapped.Count; i++)
        {
            var data = mapped[i];
            data.DeclarationIndex = i;
            if (data.Order is null) data.Order = i;
            if (explicitMultiline is int recordMultiline && data.IsQuoted)
                data.QuoteMultiline = recordMultiline;
            ValidateField(data, displayName, isFixed, diagnostics);
            hasUnresolvedTypes |= data.HasUnresolvedType;
            fields.Add(data.ToModel());
        }

        // Sort by order for deterministic output; declaration order breaks ties.
        var sorted = fields
            .OrderBy(static f => f.Order)
            .ThenBy(static f => f.DeclarationIndex)
            .ToImmutableArray();

        // MLFH008: gaps in explicit [FieldOrder] values (orders used by [FieldHidden] members fill gaps).
        ReportFieldOrderGaps(sorted, hiddenOrders, displayName, diagnostics);

        // Once a field is optional, every following field must be optional (3.x rule).
        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i - 1].IsOptional && !sorted[i].IsOptional && !sorted[i].IsInNewLine)
                diagnostics.Add(DiagnosticInfo.Create("MLFH018", sorted[i].Location, sorted[i].Name, displayName, sorted[i - 1].Name));
        }

        // Constructor arguments reference the sorted field indices.
        var args = ImmutableArray.CreateBuilder<ConstructorArgModel>(ctorArgs.Count);
        foreach (var (parameter, member, defaultExpression) in ctorArgs)
        {
            int index = -1;
            string? fallback = defaultExpression;
            if (member is not null)
            {
                for (int i = 0; i < sorted.Length; i++)
                {
                    if (string.Equals(sorted[i].Name, member.Symbol.Name, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }
                if (member.IsValueDiscarded || index < 0)
                {
                    index = -1;
                    fallback = member.NullValueExpression ?? "default!";
                }
            }
            args.Add(new ConstructorArgModel(parameter, index, fallback));
        }

        // CreateDefaultInstance (INotifyRead.BeforeRead) never passes fake arguments to a constructor that
        // may validate them: it uses an accessible parameterless constructor, or else creates the instance
        // without running any constructor.
        IMethodSymbol? defaultConstructor = useConstructor
            ? typeSymbol.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, compilation.Assembly))
            : constructor;
        bool defaultUninitialized = useConstructor && defaultConstructor is null && !typeSymbol.IsValueType;

        // Required members the generated object initializers must set (CS9035), unless the constructor
        // that is called has [SetsRequiredMembers].
        var requiredInits = ImmutableArray.CreateBuilder<RequiredMemberModel>();
        var defaultRequired = ImmutableArray.CreateBuilder<string>();
        List<ISymbol> requiredMembers = GetRequiredMembers(typeSymbol);
        if (!defaultUninitialized && !HasSetsRequiredMembers(defaultConstructor))
        {
            foreach (ISymbol required in requiredMembers)
                defaultRequired.Add(EscapeIdentifier(required.Name));
        }
        if (!HasSetsRequiredMembers(constructor))
        {
            foreach (ISymbol required in requiredMembers)
            {
                string identifier = EscapeIdentifier(required.Name);
                int index = -1;
                for (int i = 0; i < sorted.Length; i++)
                {
                    if (string.Equals(sorted[i].Name, required.Name, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= 0 && sorted[index].IsValueDiscarded)
                    index = -1;
                if (index >= 0 && sorted[index].Assign is FieldAssignKind.Setter or FieldAssignKind.Init)
                    continue; // already set by the object initializer
                requiredInits.Add(new RequiredMemberModel(identifier, index));
            }
        }

        return shell with
        {
            RequiredInits = new EquatableArray<RequiredMemberModel>(requiredInits.ToImmutable()),
            DefaultRequired = new EquatableArray<string>(defaultRequired.ToImmutable()),
            DefaultUninitialized = defaultUninitialized,
            Delimiter = delimiter,
            Fields = new EquatableArray<FieldModel>(sorted),
            FixedMode = isFixed ? fixedMode : null,
            IgnoreFirst = igFirst,
            IgnoreLast = igLast,
            IgnoreEmptyLines = igEmpty,
            IgnoreEmptySpaces = igSpaces,
            CommentMarker = commentMarker,
            CommentAnyPlace = commentAnyPlace,
            Condition = condition,
            ConditionPattern = conditionPattern,
            MultilineMode = explicitMultiline ?? CombinedMultiline(sorted),
            ImplementsNotifyRead = implementsNotifyRead,
            Diagnostics = new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()),
            UseConstructor = useConstructor,
            ConstructorArgs = new EquatableArray<ConstructorArgModel>(args.ToImmutable()),
            HasUnresolvedTypes = hasUnresolvedTypes,
        };
    }

    private static void ReportFieldOrderGaps(ImmutableArray<FieldModel> sorted, HashSet<int> hiddenOrders, string displayName, List<DiagnosticInfo> diagnostics)
    {
        if (sorted.Length < 2 || sorted.All(static f => f.Order == f.DeclarationIndex))
            return; // implicit 0,1,2,... ordering has no gaps

        for (int i = 1; i < sorted.Length; i++)
        {
            int previous = sorted[i - 1].Order;
            int current = sorted[i].Order;
            bool gap = false;
            for (int o = previous + 1; o < current; o++)
            {
                if (!hiddenOrders.Contains(o))
                {
                    gap = true;
                    break;
                }
            }
            if (gap)
            {
                diagnostics.Add(DiagnosticInfo.Create("MLFH008", sorted[i].Location, displayName,
                    previous.ToString(CultureInfo.InvariantCulture), current.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    /// <summary>
    /// Record-level multiline mode derived from the quoted fields (3.x has it per field only): reading or
    /// writing is allowed when at least one quoted field allows it. NotAllow (3) without quoted fields.
    /// </summary>
    private static int CombinedMultiline(ImmutableArray<FieldModel> fields)
    {
        bool read = false, write = false;
        foreach (var f in fields)
        {
            if (!f.IsQuoted) continue;
            read |= f.QuoteMultiline is 0 or 1;
            write |= f.QuoteMultiline is 0 or 2;
        }
        return (read, write) switch
        {
            (true, true) => 0,
            (true, false) => 1,
            (false, true) => 2,
            _ => 3,
        };
    }

    private static RecordModel Failed(RecordModel shell, DiagnosticInfo diagnostic) =>
        shell with { Diagnostics = new EquatableArray<DiagnosticInfo>(ImmutableArray.Create(diagnostic)) };

    /// <summary>Per-field diagnostics: quoted fixed-length field, empty field delimiter, unsupported type, ConverterKind mismatch.</summary>
    private static void ValidateField(MemberData data, string displayName, bool isFixed, List<DiagnosticInfo> diagnostics)
    {
        // Quoting has no meaning for positional fields (3.x BadUsageException).
        if (isFixed && data.IsQuoted)
            diagnostics.Add(DiagnosticInfo.Create("MLFH025", data.Location, data.Symbol.Name, displayName));

        if (data.Delimiter is { Length: 0 })
            diagnostics.Add(DiagnosticInfo.Create("MLFH012", data.Location, "Field '" + data.Symbol.Name + "'", displayName));

        // An unresolved field or converter type already has a compiler error (CS0246); the type rules
        // below would only add noise. The record is skipped without a FileHelpers diagnostic.
        if (data.HasUnresolvedType || ContainsErrorType(data.Type))
        {
            data.HasUnresolvedType = true;
            return;
        }

        if (data.ConverterKind != GeneratorConverterKind.None)
        {
            if (!ConverterKindMatches(data.ConverterKind, data.ValueKind))
            {
                diagnostics.Add(DiagnosticInfo.Create("MLFH019", data.Location, data.Symbol.Name, displayName,
                    data.ConverterKind.ToString(), data.Type.ToDisplayString()));
            }
            else if (data.ConverterKind == GeneratorConverterKind.DateMultiFormat && data.ConverterArgs.Length == 0)
            {
                diagnostics.Add(DiagnosticInfo.Create("MLFH021", data.Location, data.Symbol.Name, displayName,
                    data.ConverterKind.ToString(), "at least one date format is required"));
            }
            return;
        }

        if (data.MissingArgsCtor is not null)
        {
            diagnostics.Add(DiagnosticInfo.Create("MLFH022", data.Location, data.Symbol.Name, displayName,
                data.ConverterArgs.Length.ToString(CultureInfo.InvariantCulture), data.MissingArgsCtor));
            return;
        }

        if (data.ConverterTypeName is not null || data.MissingParameterlessCtor is not null)
            return;

        if (data.ValueKind == FieldValueKind.Unsupported)
            diagnostics.Add(DiagnosticInfo.Create("MLFH013", data.Location, data.Symbol.Name, displayName, data.Type.ToDisplayString()));
    }

    /// <summary>True when <paramref name="type"/> is, or is built from, a type the compiler could not resolve.</summary>
    private static bool ContainsErrorType(ITypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Error } => true,
        IArrayTypeSymbol array => ContainsErrorType(array.ElementType),
        IPointerTypeSymbol pointer => ContainsErrorType(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsErrorType),
        _ => false,
    };

    private static bool ConverterKindMatches(GeneratorConverterKind kind, FieldValueKind valueKind) => kind switch
    {
        GeneratorConverterKind.Date or GeneratorConverterKind.DateMultiFormat => valueKind == FieldValueKind.DateTime,
        GeneratorConverterKind.Byte => valueKind == FieldValueKind.Byte,
        GeneratorConverterKind.SByte => valueKind == FieldValueKind.SByte,
        GeneratorConverterKind.Int16 => valueKind == FieldValueKind.Int16,
        GeneratorConverterKind.Int32 => valueKind == FieldValueKind.Int32,
        GeneratorConverterKind.Int64 => valueKind == FieldValueKind.Int64,
        GeneratorConverterKind.UInt16 => valueKind == FieldValueKind.UInt16,
        GeneratorConverterKind.UInt32 => valueKind == FieldValueKind.UInt32,
        GeneratorConverterKind.UInt64 => valueKind == FieldValueKind.UInt64,
        GeneratorConverterKind.Decimal => valueKind == FieldValueKind.Decimal,
        GeneratorConverterKind.Double or GeneratorConverterKind.PercentDouble => valueKind == FieldValueKind.Double,
        GeneratorConverterKind.Single => valueKind == FieldValueKind.Single,
        GeneratorConverterKind.Boolean => valueKind == FieldValueKind.Boolean,
        GeneratorConverterKind.Char => valueKind == FieldValueKind.Char,
        GeneratorConverterKind.Guid => valueKind == FieldValueKind.Guid,
        GeneratorConverterKind.Enum => valueKind == FieldValueKind.Enum,
        _ => false,
    };

    /// <summary>
    /// Chooses between an object initializer (parameterless constructor, or any struct) and a
    /// constructor whose parameters bind to members by name. Returns a failure reason, or null.
    /// </summary>
    private static string? ChooseConstruction(
        INamedTypeSymbol type,
        List<MemberData> members,
        HashSet<string> hiddenNames,
        Compilation compilation,
        out bool useConstructor,
        out IMethodSymbol? constructor,
        List<(string Parameter, MemberData? Member, string? Default)> ctorArgs)
    {
        useConstructor = false;
        constructor = null;
        if (!type.IsValueType && type.IsAbstract)
            return "the type is abstract";

        IMethodSymbol? parameterless = type.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, compilation.Assembly));
        bool hasParameterless = type.IsValueType || parameterless is not null;
        constructor = parameterless;
        int ctorOnlyCount = members.Count(static m => m.Assign == FieldAssignKind.Constructor && !m.IsValueDiscarded);

        if (ctorOnlyCount == 0 && hasParameterless)
            return null;

        List<(string, MemberData?, string?)>? best = null;
        IMethodSymbol? bestCtor = null;
        int bestBound = -1;
        int bestArgs = -1;
        foreach (IMethodSymbol ctor in type.InstanceConstructors)
        {
            if (ctor.Parameters.Length == 0 || !compilation.IsSymbolAccessibleWithin(ctor, compilation.Assembly))
                continue;

            var candidate = new List<(string, MemberData?, string?)>();
            var used = new HashSet<MemberData>();
            int bound = 0;
            bool ok = true;
            foreach (IParameterSymbol p in ctor.Parameters)
            {
                MemberData? match = FindMemberForParameter(members, p, compilation);
                if (match is not null && used.Add(match))
                {
                    candidate.Add((EscapeIdentifier(p.Name), match, null));
                    if (match.Assign == FieldAssignKind.Constructor && !match.IsValueDiscarded) bound++;
                }
                else if (hiddenNames.Any(h => string.Equals(h, p.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    candidate.Add((EscapeIdentifier(p.Name), null, "default!"));
                }
                else if (!p.IsOptional && !p.IsParams)
                {
                    ok = false;
                    break;
                }
            }

            if (!ok) continue;
            if (bound > bestBound || (bound == bestBound && candidate.Count > bestArgs))
            {
                best = candidate;
                bestCtor = ctor;
                bestBound = bound;
                bestArgs = candidate.Count;
            }
        }

        if (best is not null && (bestBound > 0 || !hasParameterless))
        {
            useConstructor = true;
            constructor = bestCtor;
            foreach (var arg in best)
            {
                ctorArgs.Add(arg);
                if (arg.Item2 is { } m) m.IsCtorBound = true;
            }
            return null;
        }

        if (hasParameterless)
            return null; // constructor-only members are skipped

        return "it has no accessible parameterless constructor and no accessible constructor whose parameters all match mapped members";
    }

    /// <summary><c>required</c> fields and properties of the type and all its base types (most derived wins).</summary>
    private static List<ISymbol> GetRequiredMembers(INamedTypeSymbol type)
    {
        var result = new List<ISymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? t = type; t is not null && t.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); t = t.BaseType)
        {
            foreach (ISymbol member in t.GetMembers())
            {
                bool required = member is IFieldSymbol { IsRequired: true } or IPropertySymbol { IsRequired: true };
                if (required && seen.Add(member.Name))
                    result.Add(member);
            }
        }
        return result;
    }

    private static bool HasSetsRequiredMembers(IMethodSymbol? constructor)
    {
        if (constructor is null) return false;
        foreach (AttributeData attribute in constructor.GetAttributes())
        {
            if (string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static MemberData? FindMemberForParameter(List<MemberData> members, IParameterSymbol parameter, Compilation compilation)
    {
        MemberData? caseInsensitive = null;
        foreach (var m in members)
        {
            if (!string.Equals(m.Symbol.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsAssignableTo(m.Type, parameter.Type, compilation)) continue;
            if (string.Equals(m.Symbol.Name, parameter.Name, StringComparison.Ordinal)) return m;
            caseInsensitive ??= m;
        }
        return caseInsensitive;
    }

    private static bool IsAssignableTo(ITypeSymbol source, ITypeSymbol target, Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(source, target)) return true;
        var conversion = compilation.ClassifyCommonConversion(source, target);
        return conversion.Exists && conversion.IsImplicit;
    }

    /// <summary>
    /// True when <paramref name="member"/> carries a FileHelpers <c>[Field...]</c> attribute other than
    /// <c>[FieldHidden]</c> / <c>[FieldNotInFile]</c>, i.e. it declares itself part of the layout.
    /// </summary>
    private static bool HasLayoutAttribute(ISymbol member)
    {
        bool any = false;
        foreach (AttributeData attribute in member.GetAttributes())
        {
            INamedTypeSymbol? attrClass = attribute.AttributeClass;
            if (attrClass is null
                || !attrClass.Name.StartsWith("Field", StringComparison.Ordinal)
                || !string.Equals(attrClass.ContainingNamespace?.ToDisplayString(), AttributeNamespace, StringComparison.Ordinal))
            {
                continue;
            }
            if (attrClass.Name is "FieldHiddenAttribute" or "FieldNotInFileAttribute")
                return false;
            any = true;
        }
        return any;
    }

    /// <summary>
    /// True when FileHelpers 3.x would map <paramref name="member"/> as a column: 3.x maps every instance
    /// field of the record and its base types (<c>RecursiveGetFields</c>: public and non-public, readonly
    /// included, delegate-typed fields excluded), which includes the backing field of an auto-property,
    /// unless it is marked <c>[FieldHidden]</c> / <c>[FieldNotInFile]</c>. Properties without a backing
    /// field are never mapped by 3.x.
    /// </summary>
    private static bool IsMappedByLegacyFileHelpers(ISymbol member)
    {
        if (member.IsStatic || IsHidden(member)) return false;
        IFieldSymbol? field = member switch
        {
            IFieldSymbol { IsConst: false, AssociatedSymbol: null } f => f,
            IPropertySymbol property => BackingField(property),
            _ => null,
        };
        return field is not null && !IsHidden(field) && field.Type.TypeKind != TypeKind.Delegate;
    }

    /// <summary>The compiler-generated backing field of an auto-property, or null.</summary>
    private static IFieldSymbol? BackingField(IPropertySymbol property)
    {
        foreach (ISymbol candidate in property.ContainingType.GetMembers())
        {
            if (candidate is IFieldSymbol field && SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property))
                return field;
        }
        return null;
    }

    /// <summary>True when the member carries <c>[FieldHidden]</c> or <c>[FieldNotInFile]</c>.</summary>
    private static bool IsHidden(ISymbol member)
    {
        foreach (AttributeData attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() is "ModernLibs.FileHelpers.FieldHiddenAttribute" or "ModernLibs.FileHelpers.FieldNotInFileAttribute")
                return true;
        }
        return false;
    }

    private static string AccessibilityText(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Internal => "internal (declared in another assembly)",
        Accessibility.ProtectedOrInternal => "protected internal (declared in another assembly)",
        _ => "not accessible",
    };

    /// <summary>Reads one candidate member; returns null when it is not a mappable instance field/property.</summary>
    private static MemberData? ReadMember(ISymbol member, Compilation compilation, bool bindToTree, CancellationToken ct)
    {
        if (member.IsStatic || member.IsImplicitlyDeclared) return null;

        ITypeSymbol memberType;
        FieldAssignKind assign;
        bool isInitOnly = false;
        switch (member)
        {
            case IFieldSymbol field when field.AssociatedSymbol is null && !field.IsConst:
                memberType = field.Type;
                assign = field.IsReadOnly ? FieldAssignKind.Constructor : FieldAssignKind.Setter;
                break;
            case IPropertySymbol prop when !prop.IsIndexer && !prop.ReturnsByRef && !prop.ReturnsByRefReadonly:
                if (prop.GetMethod is null || !compilation.IsSymbolAccessibleWithin(prop.GetMethod, compilation.Assembly))
                    return null; // not readable — cannot be formatted
                memberType = prop.Type;
                if (prop.SetMethod is { } setter && compilation.IsSymbolAccessibleWithin(setter, compilation.Assembly))
                {
                    isInitOnly = setter.IsInitOnly;
                    assign = setter.IsInitOnly ? FieldAssignKind.Init : FieldAssignKind.Setter;
                }
                else
                {
                    assign = FieldAssignKind.Constructor; // get-only / inaccessible setter: constructor or nothing
                }
                break;
            default:
                return null;
        }

        var data = new MemberData(member, memberType)
        {
            Assign = assign,
            IsInitOnly = isInitOnly,
            Location = LocationInfo.From(member.Locations.FirstOrDefault(), bindToTree),
            ValueKind = Classify(UnwrapNullable(memberType)),
        };

        foreach (AttributeData mAttr in member.GetAttributes())
        {
            ct.ThrowIfCancellationRequested();
            INamedTypeSymbol? attrClass = mAttr.AttributeClass;
            // AttributeClass is null for unresolved / error-symbol attributes.
            if (attrClass is null) continue;
            string attrName = attrClass.ToDisplayString();
            if (attrClass.Name.StartsWith("Field", StringComparison.Ordinal)
                && string.Equals(attrClass.ContainingNamespace?.ToDisplayString(), AttributeNamespace, StringComparison.Ordinal))
            {
                data.HasFieldAttribute = true;
            }

            switch (attrName)
            {
                case "ModernLibs.FileHelpers.FieldOrderAttribute"
                    when mAttr.ConstructorArguments.Length > 0 && mAttr.ConstructorArguments[0].Value is int o:
                    data.Order = o;
                    break;
                case "ModernLibs.FileHelpers.FieldFixedLengthAttribute"
                    when mAttr.ConstructorArguments.Length > 0 && mAttr.ConstructorArguments[0].Value is int fl:
                    data.FixedLength = fl;
                    foreach (var namedArg in mAttr.NamedArguments)
                    {
                        if (string.Equals(namedArg.Key, "OverflowMode", StringComparison.Ordinal) && namedArg.Value.Value is int om)
                            data.OverflowMode = om;
                    }
                    break;
                case "ModernLibs.FileHelpers.FieldTrimAttribute"
                    when mAttr.ConstructorArguments.Length > 0 && mAttr.ConstructorArguments[0].Value is int tm:
                    // TrimMode int values 0=None, 1=Both, 2=Left, 3=Right.
                    data.TrimMode = tm;
                    data.TrimChars = ReadTrimChars(mAttr);
                    break;
                case "ModernLibs.FileHelpers.FieldOptionalAttribute":
                    data.IsOptional = true;
                    break;
                case "ModernLibs.FileHelpers.FieldHiddenAttribute":
                case "ModernLibs.FileHelpers.FieldNotInFileAttribute":
                    data.IsHidden = true;
                    break;
                case "ModernLibs.FileHelpers.FieldValueDiscardedAttribute":
                    data.IsValueDiscarded = true;
                    break;
                case "ModernLibs.FileHelpers.FieldNullValueAttribute":
                    // Translate the constant into a C# expression of the field type
                    ExtractNullValue(mAttr, memberType, compilation, out data.NullValueExpression, out data.IncompatibleNullValueType);
                    break;
                case "ModernLibs.FileHelpers.FieldNotEmptyAttribute":
                    data.IsNotEmpty = true;
                    break;
                case "ModernLibs.FileHelpers.FieldInNewLineAttribute":
                    data.IsInNewLine = true;
                    break;
                case "ModernLibs.FileHelpers.FieldQuotedAttribute":
                    // 3.x overloads: (), (char), (QuoteMode), (QuoteMode, MultilineMode), (char, QuoteMode),
                    // (char, QuoteMode, MultilineMode), (MultilineMode). Defaults: '"', OptionalForRead, AllowForBoth.
                    data.IsQuoted = true;
                    foreach (var ca in mAttr.ConstructorArguments)
                    {
                        if (ca.Value is char qc)
                            data.QuoteChar = qc;
                        else if (ca.Value is int enumValue)
                        {
                            string? enumType = ca.Type?.ToDisplayString();
                            if (string.Equals(enumType, "ModernLibs.FileHelpers.QuoteMode", StringComparison.Ordinal))
                                data.QuoteMode = enumValue;
                            else if (string.Equals(enumType, "ModernLibs.FileHelpers.MultilineMode", StringComparison.Ordinal))
                                data.QuoteMultiline = enumValue;
                        }
                    }
                    break;
                case "ModernLibs.FileHelpers.FieldDelimiterAttribute" when mAttr.ConstructorArguments.Length > 0:
                    // Per-field delimiter override; empty / null is reported later.
                    data.Delimiter = mAttr.ConstructorArguments[0].Value as string ?? string.Empty;
                    break;
                case "ModernLibs.FileHelpers.FieldCaptionAttribute"
                    when mAttr.ConstructorArguments.Length > 0 && mAttr.ConstructorArguments[0].Value is string fc:
                    data.Caption = fc;
                    break;
                case "ModernLibs.FileHelpers.FieldAlignAttribute":
                    if (mAttr.ConstructorArguments.Length > 0 && mAttr.ConstructorArguments[0].Value is int am)
                        data.AlignMode = am;
                    if (mAttr.ConstructorArguments.Length > 1 && mAttr.ConstructorArguments[1].Value is char ac)
                        data.AlignChar = ac;
                    break;
                case "ModernLibs.FileHelpers.FieldConverterAttribute":
                    ReadConverter(mAttr, data, compilation);
                    break;
            }
        }

        return data;
    }

    private static void ReadConverter(AttributeData mAttr, MemberData data, Compilation compilation)
    {
        if (mAttr.ConstructorArguments.Length == 0) return;

        // A null params array ([FieldConverter(kind, null)]) means no arguments.
        if (mAttr.ConstructorArguments.Length > 1 && mAttr.ConstructorArguments[1].Kind == TypedConstantKind.Array && !mAttr.ConstructorArguments[1].IsNull)
        {
            var argsArr = mAttr.ConstructorArguments[1].Values;
            var strArgs = new string[argsArr.Length];
            for (int ai = 0; ai < argsArr.Length; ai++)
                strArgs[ai] = argsArr[ai].Value as string ?? string.Empty;
            data.ConverterArgs = new EquatableArray<string>(ImmutableArray.Create(strArgs));
        }

        var firstArg = mAttr.ConstructorArguments[0];
        if (firstArg.Kind == TypedConstantKind.Type && firstArg.Value is INamedTypeSymbol typeSym)
        {
            if (ContainsErrorType(typeSym))
            {
                data.HasUnresolvedType = true; // CS0246 already; no MLFH004
                return;
            }

            bool isConverterBase = IsConverterBaseSubtype(typeSym);
            bool? targetsUnderlying = isConverterBase ? null : SpanConverterTarget(typeSym, data.Type, compilation);
            if (!isConverterBase && targetsUnderlying is null)
            {
                // Unrecognised type — MLFH004, which blocks the descriptor.
                data.UnrecognisedConverterType = typeSym.ToDisplayString();
                return;
            }

            // 3.x passes the attribute arguments to a matching constructor; here it is called with `new`.
            int argCount = data.ConverterArgs.Length;
            int shape = ConverterConstructorShape(typeSym, argCount);
            if (shape < 0)
            {
                if (argCount == 0)
                    data.MissingParameterlessCtor = typeSym.ToDisplayString();
                else
                    data.MissingArgsCtor = typeSym.ToDisplayString();
                return;
            }

            data.ConverterTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!isConverterBase)
            {
                data.DirectSpanConverter = true;
                data.ConverterTargetsUnderlying = targetsUnderlying!.Value;
            }
        }
        else if (firstArg.Kind == TypedConstantKind.Enum && firstArg.Value is int kindInt)
        {
            data.ConverterKind = (GeneratorConverterKind)kindInt;
        }

        // CultureName named argument
        foreach (var na in mAttr.NamedArguments)
        {
            if (string.Equals(na.Key, "CultureName", StringComparison.Ordinal) && na.Value.Value is string cn)
            {
                data.CultureName = cn;
                break;
            }
        }
    }

    /// <summary>
    /// FileHelpers 3.x default trim characters (<c>LineInfo.WhitespaceChars</c>), used by
    /// <c>[FieldTrim(mode)]</c> without explicit characters.
    /// </summary>
    private const string DefaultTrimChars =
        "\t\n\v\f\r \u00A0\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u200B\u3000\uFEFF";

    /// <summary>[FieldTrim(mode, params char[] chars)]: the given characters, or the 3.x whitespace set.</summary>
    private static string ReadTrimChars(AttributeData attr)
    {
        // A null params array ([FieldTrim(mode, null)]) means the default characters.
        if (attr.ConstructorArguments.Length < 2 || attr.ConstructorArguments[1].Kind != TypedConstantKind.Array || attr.ConstructorArguments[1].IsNull)
            return DefaultTrimChars;
        var chars = new StringBuilder();
        foreach (var c in attr.ConstructorArguments[1].Values)
        {
            if (c.Value is char ch) chars.Append(ch);
        }
        return chars.ToString();
    }

    /// <summary>Built-in conversion family of a (non-nullable) type.</summary>
    private static FieldValueKind Classify(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String: return FieldValueKind.String;
            case SpecialType.System_Boolean: return FieldValueKind.Boolean;
            case SpecialType.System_Char: return FieldValueKind.Char;
            case SpecialType.System_Byte: return FieldValueKind.Byte;
            case SpecialType.System_SByte: return FieldValueKind.SByte;
            case SpecialType.System_Int16: return FieldValueKind.Int16;
            case SpecialType.System_UInt16: return FieldValueKind.UInt16;
            case SpecialType.System_Int32: return FieldValueKind.Int32;
            case SpecialType.System_UInt32: return FieldValueKind.UInt32;
            case SpecialType.System_Int64: return FieldValueKind.Int64;
            case SpecialType.System_UInt64: return FieldValueKind.UInt64;
            case SpecialType.System_Decimal: return FieldValueKind.Decimal;
            case SpecialType.System_Double: return FieldValueKind.Double;
            case SpecialType.System_Single: return FieldValueKind.Single;
            case SpecialType.System_DateTime: return FieldValueKind.DateTime;
        }

        if (type.TypeKind == TypeKind.Enum) return FieldValueKind.Enum;

        if (type is INamedTypeSymbol named
            && named.ContainingNamespace is { } ns
            && string.Equals(ns.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            switch (named.MetadataName)
            {
                case "Guid": return FieldValueKind.Guid;
                case "DateOnly": return FieldValueKind.DateOnly;
                case "TimeOnly": return FieldValueKind.TimeOnly;
                case "DateTimeOffset": return FieldValueKind.DateTimeOffset;
                case "TimeSpan": return FieldValueKind.TimeSpan;
            }
        }

        return FieldValueKind.Unsupported;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named && named.TypeArguments.Length > 0
            ? named.TypeArguments[0]
            : type;

    /// <summary>Escapes reserved C# keywords used as identifiers (<c>class</c> → <c>@class</c>).</summary>
    internal static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? "@" + name : name;

    /// <summary>
    /// Unique hint name from the full metadata name (namespace, containing types joined with '+',
    /// generic arity) plus the record kind, sanitized to file-name-safe characters. Roslyn compares
    /// hint names case-insensitively and sanitizing maps several characters to '_', so a stable hash of
    /// the exact metadata name is appended (<c>Rec</c> and <c>REC</c> get different files).
    /// </summary>
    private static string BuildHintName(INamedTypeSymbol type, bool isFixed)
    {
        var chain = new Stack<INamedTypeSymbol>();
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
            chain.Push(t);

        var raw = new StringBuilder();
        if (!type.ContainingNamespace.IsGlobalNamespace)
            raw.Append(type.ContainingNamespace.ToDisplayString()).Append('.');
        bool first = true;
        foreach (var t in chain)
        {
            if (!first) raw.Append('+');
            raw.Append(t.MetadataName);
            first = false;
        }

        var sanitized = new StringBuilder(raw.Length + 32);
        foreach (char c in raw.ToString())
            sanitized.Append(char.IsLetterOrDigit(c) || c is '_' or '.' or '+' or '-' ? c : '_');
        sanitized.Append('.').Append(StableHash(raw.ToString()).ToString("x8", CultureInfo.InvariantCulture));
        sanitized.Append(isFixed ? ".FixedLengthDescriptor.g.cs" : ".DelimitedDescriptor.g.cs");
        return sanitized.ToString();
    }

    /// <summary>32-bit FNV-1a over the UTF-16 code units (deterministic across processes, unlike string.GetHashCode).</summary>
    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (char c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    /// <summary>The record type and all containing types must be visible to other files of the assembly.</summary>
    private static bool IsAccessibleRecordType(INamedTypeSymbol type, Compilation compilation)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            if (t.IsFileLocal) return false;
        }
        return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
    }

    /// <summary>
    /// True when generated code in another file can use the record type as a type argument: accessible,
    /// not file-local, non-generic (containing types included), not static and not a ref struct.
    /// </summary>
    private static bool IsReferenceableRecordType(INamedTypeSymbol type, Compilation compilation)
    {
        if (type.IsStatic || type.IsRefLikeType || !IsAccessibleRecordType(type, compilation)) return false;
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            if (t.TypeParameters.Length > 0) return false;
        }
        return true;
    }

    private static bool HasAttribute(INamedTypeSymbol type, string fullName)
    {
        foreach (var a in type.GetAttributes())
        {
            if (string.Equals(a.AttributeClass?.ToDisplayString(), fullName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Extracts record-level filtering attributes from the annotated type symbol:
    /// [IgnoreFirst(N)], [IgnoreLast(N)], [IgnoreEmptyLines], [IgnoreCommentedLines(char)],
    /// [ConditionalRecord(condition, pattern)]. Returns defaults when absent.
    /// </summary>
    private static (int IgnoreFirst, int IgnoreLast, bool IgnoreEmptyLines, bool IgnoreEmptySpaces, char CommentMarker, bool CommentAnyPlace, int Condition, string ConditionPattern)
        ExtractRecordLevelFilters(INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        int igFirst = 0;
        int igLast = 0;
        bool igEmpty = false;
        bool igSpaces = false;
        char commentMarker = '\0';
        bool commentAnyPlace = true; // 3.x default
        int condition = 0; // RecordCondition.None = 0
        string conditionPattern = string.Empty;

        foreach (var ta in typeSymbol.GetAttributes())
        {
            // Cancellation between attributes.
            ct.ThrowIfCancellationRequested();
            string? attrName = ta.AttributeClass?.ToDisplayString();
            if (attrName is null) continue;
            switch (attrName)
            {
                case "ModernLibs.FileHelpers.IgnoreFirstAttribute":
                    igFirst = ta.ConstructorArguments.Length > 0 && ta.ConstructorArguments[0].Value is int n1 ? n1 : 1;
                    break;
                case "ModernLibs.FileHelpers.IgnoreLastAttribute":
                    igLast = ta.ConstructorArguments.Length > 0 && ta.ConstructorArguments[0].Value is int n2 ? n2 : 1;
                    break;
                case "ModernLibs.FileHelpers.IgnoreEmptyLinesAttribute":
                    igEmpty = true;
                    igSpaces = ta.ConstructorArguments.Length > 0 && ta.ConstructorArguments[0].Value is true;
                    break;
                case "ModernLibs.FileHelpers.IgnoreCommentedLinesAttribute":
                    if (ta.ConstructorArguments.Length > 0 && ta.ConstructorArguments[0].Value is char cm)
                        commentMarker = cm;
                    if (ta.ConstructorArguments.Length > 1 && ta.ConstructorArguments[1].Value is bool anyPlace)
                        commentAnyPlace = anyPlace;
                    break;
                case "ModernLibs.FileHelpers.ConditionalRecordAttribute":
                    // Condition type and pattern from constructor args
                    if (ta.ConstructorArguments.Length >= 2)
                    {
                        if (ta.ConstructorArguments[0].Value is int condValue)
                            condition = condValue;
                        if (ta.ConstructorArguments[1].Value is string pattern)
                            conditionPattern = pattern;
                    }
                    break;
            }
        }

        return (igFirst, igLast, igEmpty, igSpaces, commentMarker, commentAnyPlace, condition, conditionPattern);
    }

    /// <summary>True if <paramref name="typeSym"/> inherits from <c>ModernLibs.FileHelpers.Converters.ConverterBase</c>.</summary>
    private static bool IsConverterBaseSubtype(INamedTypeSymbol typeSym)
    {
        const string converterBaseFqn = "ModernLibs.FileHelpers.Converters.ConverterBase";
        for (INamedTypeSymbol? current = typeSym.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), converterBaseFqn, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// When <paramref name="converterType"/> implements <c>ISpanConverter&lt;T&gt;</c> for the field type,
    /// returns whether it converts the underlying type of a <c>T?</c> field (<see langword="true"/>) or the
    /// field type itself (<see langword="false"/>); returns null when it is no converter for the field.
    /// </summary>
    private static bool? SpanConverterTarget(INamedTypeSymbol converterType, ITypeSymbol fieldType, Compilation compilation)
    {
        INamedTypeSymbol? ifaceTemplate = compilation.GetTypeByMetadataName("ModernLibs.FileHelpers.Converters.ISpanConverter`1");
        if (ifaceTemplate is null)
            return null;

        ITypeSymbol underlying = UnwrapNullable(fieldType);
        bool? result = null;
        foreach (INamedTypeSymbol iface in converterType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, ifaceTemplate) || iface.TypeArguments.Length != 1)
                continue;
            ITypeSymbol typeArg = iface.TypeArguments[0];
            if (SymbolEqualityComparer.Default.Equals(typeArg, fieldType))
                return false;
            if (SymbolEqualityComparer.Default.Equals(typeArg, underlying))
                result = true;
        }

        return result;
    }

    /// <summary>
    /// The public constructor the generated code calls for a custom converter with
    /// <paramref name="argCount"/> string arguments: 0 = parameterless, 1 = exactly that many <c>string</c>
    /// parameters, 2 = a single <c>params string[]</c> parameter; -1 when there is none.
    /// </summary>
    private static int ConverterConstructorShape(INamedTypeSymbol type, int argCount)
    {
        if (argCount == 0)
            return HasPublicParameterlessConstructor(type) ? 0 : -1;

        int shape = -1;
        foreach (IMethodSymbol ctor in type.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public)
                continue;

            var parameters = ctor.Parameters;
            if (parameters.Length == argCount && parameters.All(static p => p.Type.SpecialType == SpecialType.System_String && p.RefKind == RefKind.None))
                return 1;

            if (parameters.Length == 1 && parameters[0].IsParams
                && parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String })
                shape = 2;
        }

        return shape;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        foreach (IMethodSymbol ctor in type.InstanceConstructors)
        {
            if (ctor.Parameters.Length != 0)
                continue;

            return ctor.DeclaredAccessibility == Accessibility.Public;
        }

        return false;
    }

    /// <summary>
    /// Translates <c>[FieldNullValue(x)]</c> into a C# expression already cast to the field type
    /// (e.g. <c>(int)(-1)</c>). A <see langword="null"/> constant means "no null value" (FileHelpers 3.x).
    /// The constant must be implicitly convertible to the field type; a numeric constant is also accepted
    /// for <c>decimal</c> fields because attributes cannot hold decimal constants. Anything else sets
    /// <paramref name="incompatibleType"/> so the orchestrator reports MLFH010.
    /// </summary>
    private static void ExtractNullValue(
        AttributeData attr,
        ITypeSymbol memberType,
        Compilation compilation,
        out string? expression,
        out string? incompatibleType)
    {
        expression = null;
        incompatibleType = null;
        if (attr.ConstructorArguments.Length == 0) return;

        var constant = attr.ConstructorArguments[0];
        if (constant.IsNull) return;

        if (constant.Kind == TypedConstantKind.Array || constant.Type is null)
        {
            incompatibleType = constant.Type?.ToDisplayString() ?? "array";
            return;
        }

        var conversion = compilation.ClassifyCommonConversion(constant.Type, memberType);
        ITypeSymbol targetCore = UnwrapNullable(memberType);
        bool allowed = conversion.Exists
            && (conversion.IsImplicit || (conversion.IsNumeric && targetCore.SpecialType == SpecialType.System_Decimal));
        string? literal = allowed ? FormatConstantLiteral(constant) : null;
        if (literal is null)
        {
            incompatibleType = constant.Type.ToDisplayString();
            return;
        }

        expression = $"({memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})({literal})";
    }

    /// <summary>
    /// Formats a primitive or enum attribute constant as a culture-invariant C# literal, or returns
    /// null for unsupported constants.
    /// </summary>
    private static string? FormatConstantLiteral(TypedConstant constant)
    {
        object? value = constant.Value;
        switch (value)
        {
            case bool b:
                return b ? "true" : "false";
            case char c:
                return "'\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture) + "'";
            case string str:
                return SymbolDisplay.FormatLiteral(str, quote: true);
            case float f:
                if (float.IsNaN(f)) return "global::System.Single.NaN";
                if (float.IsPositiveInfinity(f)) return "global::System.Single.PositiveInfinity";
                if (float.IsNegativeInfinity(f)) return "global::System.Single.NegativeInfinity";
                return f.ToString("R", CultureInfo.InvariantCulture) + "F";
            case double d:
                if (double.IsNaN(d)) return "global::System.Double.NaN";
                if (double.IsPositiveInfinity(d)) return "global::System.Double.PositiveInfinity";
                if (double.IsNegativeInfinity(d)) return "global::System.Double.NegativeInfinity";
                return d.ToString("R", CultureInfo.InvariantCulture) + "D";
            case uint ui:
                return ui.ToString(CultureInfo.InvariantCulture) + "U";
            case long l:
                return l.ToString(CultureInfo.InvariantCulture) + "L";
            case ulong ul:
                return ul.ToString(CultureInfo.InvariantCulture) + "UL";
            case sbyte or byte or short or ushort or int:
                return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    /// <summary>True when <paramref name="type"/> is <c>Nullable&lt;T&gt;</c> (<c>int?</c>, <c>DateTime?</c>, ...).</summary>
    private static bool IsNullableValueType(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>Detects whether <paramref name="typeSymbol"/> implements <c>INotifyRead&lt;T&gt;</c>.</summary>
    private static bool ImplementsINotifyRead(INamedTypeSymbol typeSymbol, Compilation compilation)
    {
        INamedTypeSymbol? ifaceTemplate = compilation.GetTypeByMetadataName("ModernLibs.FileHelpers.INotifyRead`1");
        if (ifaceTemplate is null)
            return false;

        foreach (INamedTypeSymbol iface in typeSymbol.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, ifaceTemplate))
                continue;
            if (iface.TypeArguments.Length != 1)
                continue;
            if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], typeSymbol))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Collects members of <paramref name="type"/> and its base classes in FileHelpers 3.x order —
    /// base-class members first, then derived. A member redeclared in a derived class (override /
    /// <c>new</c>) keeps the base position but uses the most-derived symbol. A type marked
    /// <c>[IgnoreInheritedClass]</c> contributes its own members but none of its base types'.
    /// </summary>
    private static List<ISymbol> GetAllMembers(INamedTypeSymbol type, CancellationToken ct)
    {
        var chain = new List<INamedTypeSymbol>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            ct.ThrowIfCancellationRequested();
            if (current.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType)
                break;
            chain.Add(current);
            // As in 3.x (RecursiveGetFields), [IgnoreInheritedClass] on any type of the chain stops the
            // walk after that type, so its own members are mapped but not those of its base types.
            if (HasAttribute(current, "ModernLibs.FileHelpers.IgnoreInheritedClassAttribute")) break;
        }
        chain.Reverse();

        var result = new List<ISymbol>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in chain)
        {
            foreach (var member in t.GetMembers())
            {
                if (member is not (IFieldSymbol or IPropertySymbol)) continue;
                if (positions.TryGetValue(member.Name, out int index))
                {
                    result[index] = member;
                }
                else
                {
                    positions[member.Name] = result.Count;
                    result.Add(member);
                }
            }
        }

        return result;
    }

    /// <summary>Mutable per-member scratch data; never escapes the extractor.</summary>
    private sealed class MemberData(ISymbol symbol, ITypeSymbol type)
    {
        public ISymbol Symbol { get; } = symbol;

        public ITypeSymbol Type { get; } = type;

        public FieldAssignKind Assign;
        public bool IsInitOnly;
        public bool IsCtorBound;
        public bool HasFieldAttribute;
        public bool HasUnresolvedType;
        public LocationInfo? Location;
        public FieldValueKind ValueKind;
        public int DeclarationIndex;
        public int? Order;
        public int? TrimMode;
        public string? TrimChars;
        public bool IsOptional;
        public bool IsHidden;
        public bool IsValueDiscarded;
        public bool IsNotEmpty;
        public bool IsInNewLine;
        public int FixedLength = -1;
        public int OverflowMode;
        public GeneratorConverterKind ConverterKind;
        public string? ConverterTypeName;
        public bool DirectSpanConverter;
        public bool ConverterTargetsUnderlying;
        public EquatableArray<string> ConverterArgs;
        public bool IsQuoted;
        public char QuoteChar = '"';
        public int QuoteMode = 1;      // QuoteMode.OptionalForRead
        public int QuoteMultiline;     // MultilineMode.AllowForBoth
        public string? Delimiter;
        public string? UnrecognisedConverterType;
        public string? MissingParameterlessCtor;
        public string? MissingArgsCtor;
        public int? AlignMode;
        public char? AlignChar;
        public string? Caption;
        public string? CultureName;
        public string? NullValueExpression;
        public string? IncompatibleNullValueType;

        public FieldModel ToModel()
        {
            ITypeSymbol underlying = UnwrapNullable(Type).WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            return new FieldModel(
                Symbol.Name,
                Type.ToDisplayString(),
                Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DeclarationIndex,
                Order ?? DeclarationIndex,
                TrimMode,
                IsOptional,
                IsHidden,
                IsValueDiscarded,
                FixedLength: FixedLength,
                IsInitOnly: IsInitOnly,
                ConverterKind: ConverterKind,
                ConverterTypeName: ConverterTypeName,
                DirectSpanConverter: DirectSpanConverter,
                ConverterArgs: ConverterArgs,
                IsQuoted: IsQuoted,
                QuoteChar: QuoteChar,
                QuoteMode: QuoteMode,
                QuoteMultiline: QuoteMultiline,
                Delimiter: Delimiter,
                UnrecognisedConverterType: UnrecognisedConverterType,
                AlignMode: AlignMode,
                AlignChar: AlignChar,
                OverflowMode: OverflowMode,
                IsNotEmpty: IsNotEmpty,
                IsInNewLine: IsInNewLine,
                Caption: Caption,
                CultureName: CultureName,
                MissingParameterlessCtorTypeName: MissingParameterlessCtor,
                IsValueType: Type.IsValueType,
                IsNullableValueType: IsNullableValueType(Type),
                NullValueExpression: NullValueExpression,
                IncompatibleNullValueType: IncompatibleNullValueType,
                Identifier: EscapeIdentifier(Symbol.Name),
                UnderlyingTypeName: underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ValueKind: ValueKind,
                ConverterTargetsUnderlying: ConverterTargetsUnderlying,
                Assign: IsCtorBound ? FieldAssignKind.Constructor : Assign,
                Location: Location,
                TrimChars: TrimChars);
        }
    }
}
