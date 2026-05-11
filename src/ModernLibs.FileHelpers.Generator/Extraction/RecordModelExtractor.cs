// MA0051: ExtractDelimited is a single-responsibility transform; extracting sub-methods would
// require passing many local variables, reducing readability without improving maintainability.
#pragma warning disable MA0051
using System.Collections.Immutable;
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

        // Phase 7 + Phase 15: extract record-level filtering attributes (IgnoreFirst, IgnoreLast, IgnoreEmptyLines, IgnoreCommentedLines, ConditionalRecord).
        // These feed RecordOptions on the generated descriptor (D-07, CREC-01).
        var (igFirst, igLast, igEmpty, commentMarker, condition, conditionPattern) = ExtractRecordLevelFilters(typeSymbol, ct);

        // Phase 18 MULT-01: Extract MultilineMode from DelimitedRecordAttribute
        int multilineMode = 3; // Default to NotAllow = 3
        var multilineArg = attr.NamedArguments.FirstOrDefault(
            kv => string.Equals(kv.Key, "MultilineMode", System.StringComparison.Ordinal)).Value;
        if (multilineArg is { Kind: TypedConstantKind.Enum } namedArg && namedArg.Value is int mm)
        {
            multilineMode = mm;
        }

        // Phase 36 QUOTE-02: Extract QuoteUnescapeMode from DelimitedRecordAttribute
        int quoteUnescapeMode = 0; // Default to None = 0
        var unescapeArg = attr.NamedArguments.FirstOrDefault(
            kv => string.Equals(kv.Key, "QuoteUnescapeMode", System.StringComparison.Ordinal)).Value;
        if (unescapeArg is { Kind: TypedConstantKind.Enum } unescapeNamed && unescapeNamed.Value is int um)
        {
            quoteUnescapeMode = um;
        }

        // Phase 36 QUOTE-03: Extract TrimMode from DelimitedRecordAttribute
        int recordTrimMode = 0; // Default to None = 0
        var trimArg = attr.NamedArguments.FirstOrDefault(
            kv => string.Equals(kv.Key, "TrimMode", System.StringComparison.Ordinal)).Value;
        if (trimArg is { Kind: TypedConstantKind.Enum } trimNamed && trimNamed.Value is int recTrim)
        {
            recordTrimMode = recTrim;
        }

        // Phase 38 HDR-01: Extract HasHeader from DelimitedRecordAttribute
        bool hasHeader = false;
        var headerArg = attr.NamedArguments.FirstOrDefault(
            kv => string.Equals(kv.Key, "HasHeader", System.StringComparison.Ordinal)).Value;
        if (headerArg.Value is bool hdr)
        {
            hasHeader = hdr;
        }

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
            int? trimMode = null;
            bool isOptional = false;
            bool isHidden = false;
            bool isValueDiscarded = false;
            bool isNotEmpty = false;  // Phase 13 FAVL-06: FieldNotEmpty extraction
            bool isInNewLine = false; // Phase 14 MLIN-01: FieldInNewLine extraction
            GeneratorConverterKind converterKind = GeneratorConverterKind.None;
            string? converterTypeName = null;
            EquatableArray<string> converterArgs = default;

            bool isQuoted = false;
            char quoteChar = '"';
            string? fieldDelimiter = null;  // FDEL-01: per-field delimiter override from [FieldDelimiter]
            string? unrecognisedConverterType = null;
            bool directSpanConverter = false;
            int? alignMode = null;  // Phase 12: 0=Left, 1=Center, 2=Right
            char? alignChar = null; // Phase 12: custom pad char, null = space
            int fixedLength = -1;   // Phase 12: FieldFixedLength for alignment width in delimited records
            string? fieldName = null; // Phase 38 HDR-03: explicit column name from [FieldName]

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
                    case "ModernLibs.FileHelpers.FieldFixedLengthAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int fl:
                        // Phase 12: Extract FixedLength for delimited records (used with FieldAlign)
                        fixedLength = fl;
                        break;
                    case "ModernLibs.FileHelpers.FieldTrimAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int tm:
                        // WR-06: store TrimMode as int? matching the enum integer values
                        // (0=None, 1=Both, 2=Left, 3=Right). The emitter switches on the int
                        // for type safety — no ToString()/ReFromString() round-trip.
                        trimMode = tm;
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
                    case "ModernLibs.FileHelpers.FieldNotEmptyAttribute":
                        isNotEmpty = true;  // Phase 13 FAVL-06
                        break;
                    case "ModernLibs.FileHelpers.FieldInNewLineAttribute":
                        isInNewLine = true;  // Phase 14 MLIN-01
                        break;
                    case "ModernLibs.FileHelpers.FieldQuotedAttribute":
                    {
                        // Phase 7 D-06: always-quote semantics (generator emits quote-wrap unconditionally).
                        isQuoted = true;
                        // Scan constructor args for a char (quote character). Overloads:
                        //   ()                           — no args
                        //   (char)                       — quote char
                        //   (QuoteMode)                  — mode only; keep default quote
                        //   (char, QuoteMode)            — quote + mode
                        foreach (var ca in mAttr.ConstructorArguments)
                        {
                            if (ca.Value is char qc)
                            {
                                quoteChar = qc;
                                break;
                            }
                        }
                        break;
                    }
                    case "ModernLibs.FileHelpers.FieldDelimiterAttribute"
                             when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is string del:
                        // FDEL-01: extract per-field delimiter override
                        fieldDelimiter = del;
                        break;
                    case "ModernLibs.FileHelpers.FieldNameAttribute"
                             when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is string fn:
                        // Phase 38 HDR-03: extract explicit column name mapping
                        fieldName = fn;
                        break;
                    case "ModernLibs.FileHelpers.FieldAlignAttribute":
                        // Phase 12: Extract alignment mode and optional pad character
                        // First constructor arg is AlignMode (int): 0=Left, 1=Center, 2=Right
                        if (mAttr.ConstructorArguments.Length > 0
                            && mAttr.ConstructorArguments[0].Value is int am)
                        {
                            alignMode = am;
                        }
                        // Second constructor arg is AlignChar (char), if present
                        if (mAttr.ConstructorArguments.Length > 1
                            && mAttr.ConstructorArguments[1].Value is char ac)
                        {
                            alignChar = ac;
                        }
                        break;
                    case "ModernLibs.FileHelpers.FieldConverterAttribute":
                    {
                        if (mAttr.ConstructorArguments.Length == 0) break;
                        var firstArg = mAttr.ConstructorArguments[0];
                        if (firstArg.Kind == Microsoft.CodeAnalysis.TypedConstantKind.Type
                            && firstArg.Value is INamedTypeSymbol typeSym)
                        {
                            if (IsConverterBaseSubtype(typeSym))
                            {
                                converterTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                converterKind = GeneratorConverterKind.None;
                            }
                            else if (memberType is not null
                                     && ImplementsSpanConverterForField(typeSym, memberType, ctx.SemanticModel.Compilation)
                                     && HasPublicParameterlessConstructor(typeSym))
                            {
                                converterTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                converterKind = GeneratorConverterKind.None;
                                directSpanConverter = true;
                            }
                            else
                            {
                                // IN-02: unrecognised type — capture its name so the orchestrator
                                // can emit MLFH004 at compile time instead of silently falling
                                // through to default(T).
                                unrecognisedConverterType = typeSym.ToDisplayString();
                            }
                        }
                        else if (firstArg.Kind == Microsoft.CodeAnalysis.TypedConstantKind.Enum
                                 && firstArg.Value is int kindInt)
                        {
                            converterKind = (GeneratorConverterKind)kindInt;
                        }
                        if (mAttr.ConstructorArguments.Length > 1
                            && mAttr.ConstructorArguments[1].Kind == Microsoft.CodeAnalysis.TypedConstantKind.Array)
                        {
                            var argsArr = mAttr.ConstructorArguments[1].Values;
                            var strArgs = new string[argsArr.Length];
                            for (int ai = 0; ai < argsArr.Length; ai++)
                                strArgs[ai] = argsArr[ai].Value as string ?? string.Empty;
                            var immutable = System.Collections.Immutable.ImmutableArray.Create(strArgs);
                            converterArgs = new EquatableArray<string>(immutable);
                        }
                        break;
                    }
                }
            }

            fields.Add(new FieldModel(memberName, memberTypeName, fullTypeName,
                declarationIndex, order, trimMode, isOptional, isHidden, isValueDiscarded,
                FixedLength: fixedLength, IsInitOnly: isInitOnly,
                ConverterKind: converterKind, ConverterTypeName: converterTypeName,
                DirectSpanConverter: directSpanConverter,
                ConverterArgs: converterArgs,
                IsQuoted: isQuoted, QuoteChar: quoteChar,
                Delimiter: fieldDelimiter,
                UnrecognisedConverterType: unrecognisedConverterType,
                AlignMode: alignMode,
                AlignChar: alignChar,
                IsNotEmpty: isNotEmpty,  // Phase 13 FAVL-06
                IsInNewLine: isInNewLine,  // Phase 14 MLIN-01
                FieldName: fieldName));  // Phase 38 HDR-03
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
            new EquatableArray<FieldModel>(sortedArray),
            IgnoreFirst: igFirst,
            IgnoreLast: igLast,
            IgnoreEmptyLines: igEmpty,
            CommentMarker: commentMarker,
            Condition: condition,
            ConditionPattern: conditionPattern,
            MultilineMode: multilineMode,
            QuoteUnescapeMode: quoteUnescapeMode,
            TrimMode: recordTrimMode,
            HasHeader: hasHeader);
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

        // Phase 7 + Phase 15: extract record-level filtering attributes for fixed-length records too.
        var (igFirst, igLast, igEmpty, commentMarker, condition, conditionPattern) = ExtractRecordLevelFilters(typeSymbol, ct);

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
            int? trimMode = null;
            bool isOptional = false;
            bool isHidden = false;
            bool isValueDiscarded = false;
            bool isNotEmpty = false;  // Phase 13 FAVL-06: FieldNotEmpty extraction
            bool isInNewLine = false; // Phase 14 MLIN-01: FieldInNewLine extraction
            int fixedLength = -1; // sentinel: [FieldFixedLength] not found
            GeneratorConverterKind converterKind = GeneratorConverterKind.None;
            string? converterTypeName = null;
            EquatableArray<string> converterArgs = default;
            bool isQuoted = false;
            char quoteChar = '"';
            string? fieldDelimiter = null;  // FDEL-01: per-field delimiter override from [FieldDelimiter]
            string? unrecognisedConverterType = null;
            bool directSpanConverter = false;
            int? alignMode = null;  // Phase 12: 0=Left, 1=Center, 2=Right
            char? alignChar = null; // Phase 12: custom pad char, null = space
            int overflowMode = 0;   // Phase 20: OverflowMode.DiscardEnd by default

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
                        foreach (var namedArg in mAttr.NamedArguments)
                        {
                            if (string.Equals(namedArg.Key, "OverflowMode", StringComparison.Ordinal) && namedArg.Value.Value is int om)
                            {
                                overflowMode = om;
                                break;
                            }
                        }
                        break;
                    case "ModernLibs.FileHelpers.FieldTrimAttribute"
                        when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is int tm:
                        // WR-06: store TrimMode as int? (see ExtractDelimited for rationale).
                        trimMode = tm;
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
                    case "ModernLibs.FileHelpers.FieldNotEmptyAttribute":
                        isNotEmpty = true;  // Phase 13 FAVL-06
                        break;
                    case "ModernLibs.FileHelpers.FieldInNewLineAttribute":
                        isInNewLine = true;  // Phase 14 MLIN-01
                        break;
                    case "ModernLibs.FileHelpers.FieldQuotedAttribute":
                    {
                        isQuoted = true;
                        foreach (var ca in mAttr.ConstructorArguments)
                        {
                            if (ca.Value is char qc)
                            {
                                quoteChar = qc;
                                break;
                            }
                        }
                        break;
                    }
                    case "ModernLibs.FileHelpers.FieldDelimiterAttribute"
                             when mAttr.ConstructorArguments.Length > 0
                             && mAttr.ConstructorArguments[0].Value is string del:
                        // FDEL-01: extract per-field delimiter override (extracted for consistency even for fixed-length)
                        fieldDelimiter = del;
                        break;
                    case "ModernLibs.FileHelpers.FieldAlignAttribute":
                        // Phase 12: Extract alignment mode and optional pad character
                        // First constructor arg is AlignMode (int): 0=Left, 1=Center, 2=Right
                        if (mAttr.ConstructorArguments.Length > 0
                            && mAttr.ConstructorArguments[0].Value is int am)
                        {
                            alignMode = am;
                        }
                        // Second constructor arg is AlignChar (char), if present
                        if (mAttr.ConstructorArguments.Length > 1
                            && mAttr.ConstructorArguments[1].Value is char ac)
                        {
                            alignChar = ac;
                        }
                        break;
                    case "ModernLibs.FileHelpers.FieldConverterAttribute":
                    {
                        if (mAttr.ConstructorArguments.Length == 0) break;
                        var firstArg = mAttr.ConstructorArguments[0];
                        if (firstArg.Kind == Microsoft.CodeAnalysis.TypedConstantKind.Type
                            && firstArg.Value is INamedTypeSymbol typeSym)
                        {
                            if (IsConverterBaseSubtype(typeSym))
                            {
                                converterTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                converterKind = GeneratorConverterKind.None;
                            }
                            else if (memberType is not null
                                     && ImplementsSpanConverterForField(typeSym, memberType, ctx.SemanticModel.Compilation)
                                     && HasPublicParameterlessConstructor(typeSym))
                            {
                                converterTypeName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                converterKind = GeneratorConverterKind.None;
                                directSpanConverter = true;
                            }
                            else
                            {
                                // IN-02: unrecognised type — capture its name so MLFH004 fires.
                                unrecognisedConverterType = typeSym.ToDisplayString();
                            }
                        }
                        else if (firstArg.Kind == Microsoft.CodeAnalysis.TypedConstantKind.Enum
                                 && firstArg.Value is int kindInt)
                        {
                            converterKind = (GeneratorConverterKind)kindInt;
                        }
                        if (mAttr.ConstructorArguments.Length > 1
                            && mAttr.ConstructorArguments[1].Kind == Microsoft.CodeAnalysis.TypedConstantKind.Array)
                        {
                            var argsArr = mAttr.ConstructorArguments[1].Values;
                            var strArgs = new string[argsArr.Length];
                            for (int ai = 0; ai < argsArr.Length; ai++)
                                strArgs[ai] = argsArr[ai].Value as string ?? string.Empty;
                            var immutable = System.Collections.Immutable.ImmutableArray.Create(strArgs);
                            converterArgs = new EquatableArray<string>(immutable);
                        }
                        break;
                    }
                }
            }

            fields.Add(new FieldModel(memberName, memberTypeName, fullTypeName,
                declarationIndex, order, trimMode, isOptional, isHidden, isValueDiscarded,
                FixedLength: fixedLength, IsInitOnly: isInitOnly,
                ConverterKind: converterKind, ConverterTypeName: converterTypeName,
                DirectSpanConverter: directSpanConverter,
                ConverterArgs: converterArgs,
                IsQuoted: isQuoted, QuoteChar: quoteChar,
                Delimiter: fieldDelimiter,
                UnrecognisedConverterType: unrecognisedConverterType,
                AlignMode: alignMode,
                AlignChar: alignChar,
                OverflowMode: overflowMode,
                IsNotEmpty: isNotEmpty,  // Phase 13 FAVL-06
                IsInNewLine: isInNewLine,  // Phase 14 MLIN-01
                FieldName: null));  // Phase 38: FieldName not applicable to fixed-length
        }

        var sortedArray = fields
            .OrderBy(static f => f.Order)
            .ThenBy(static f => f.DeclarationIndex)
            .ToImmutableArray();

        return new RecordModel(
            ns, typeName, typeKind, isReadOnly, "FixedLength", Delimiter: string.Empty,
            new EquatableArray<FieldModel>(sortedArray),
            FixedMode: fixedMode,
            IgnoreFirst: igFirst,
            IgnoreLast: igLast,
            IgnoreEmptyLines: igEmpty,
            CommentMarker: commentMarker,
            Condition: condition,
            ConditionPattern: conditionPattern);
    }

    /// <summary>
    /// Extracts record-level filtering attributes (D-07, CREC-01) from the annotated type symbol.
    /// Scans the type's own attributes (not field attributes) for:
    ///   [IgnoreFirst(N)]             → IgnoreFirst count
    ///   [IgnoreLast(N)]              → IgnoreLast count
    ///   [IgnoreEmptyLines]           → IgnoreEmptyLines flag
    ///   [IgnoreCommentedLines(char)] → CommentMarker char
    ///   [ConditionalRecord(condition, pattern)] → Condition and ConditionPattern
    /// Returns defaults (0, 0, false, '\0', 0, "") when the attribute is absent.
    /// </summary>
    private static (int IgnoreFirst, int IgnoreLast, bool IgnoreEmptyLines, char CommentMarker, int Condition, string ConditionPattern)
        ExtractRecordLevelFilters(INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        int igFirst = 0;
        int igLast = 0;
        bool igEmpty = false;
        char commentMarker = '\0';
        int condition = 0; // RecordCondition.None = 0
        string conditionPattern = string.Empty;

        foreach (var ta in typeSymbol.GetAttributes())
        {
            // WR-07: match the cancellation discipline of ExtractDelimited / ExtractFixedLength.
            ct.ThrowIfCancellationRequested();
            string? attrName = ta.AttributeClass?.ToDisplayString();
            if (attrName is null) continue;
            switch (attrName)
            {
                case "ModernLibs.FileHelpers.IgnoreFirstAttribute":
                    // (): default 1. (int): explicit count.
                    igFirst = ta.ConstructorArguments.Length > 0
                              && ta.ConstructorArguments[0].Value is int n1
                        ? n1 : 1;
                    break;
                case "ModernLibs.FileHelpers.IgnoreLastAttribute":
                    igLast = ta.ConstructorArguments.Length > 0
                             && ta.ConstructorArguments[0].Value is int n2
                        ? n2 : 1;
                    break;
                case "ModernLibs.FileHelpers.IgnoreEmptyLinesAttribute":
                    igEmpty = true;
                    break;
                case "ModernLibs.FileHelpers.IgnoreCommentedLinesAttribute":
                    if (ta.ConstructorArguments.Length > 0
                        && ta.ConstructorArguments[0].Value is char cm)
                    {
                        commentMarker = cm;
                    }
                    break;
                case "ModernLibs.FileHelpers.ConditionalRecordAttribute":
                    // CREC-01: Extract condition type and pattern from constructor args
                    if (ta.ConstructorArguments.Length >= 2)
                    {
                        if (ta.ConstructorArguments[0].Value is int condValue)
                        {
                            condition = condValue;
                        }
                        if (ta.ConstructorArguments[1].Value is string pattern)
                        {
                            conditionPattern = pattern ?? string.Empty;
                        }
                    }
                    break;
            }
        }

        return (igFirst, igLast, igEmpty, commentMarker, condition, conditionPattern);
    }

    /// <summary>
    /// Returns true if <paramref name="typeSym"/> inherits from
    /// <c>ModernLibs.FileHelpers.Converters.ConverterBase</c>.
    /// Used to gate <c>ConverterBaseAdapter&lt;T&gt;</c> emission in Phase 6.
    /// Phase 7 will add <c>ISpanConverter&lt;T&gt;</c> detection.
    /// </summary>
    private static bool IsConverterBaseSubtype(INamedTypeSymbol typeSym)
    {
        const string converterBaseFqn = "ModernLibs.FileHelpers.Converters.ConverterBase";
        INamedTypeSymbol? current = typeSym.BaseType;
        while (current is not null)
        {
            if (string.Equals(current.ToDisplayString(), converterBaseFqn, System.StringComparison.Ordinal))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static ITypeSymbol UnwrapNullableFieldType(ITypeSymbol fieldType)
    {
        if (fieldType is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length > 0)
        {
            return named.TypeArguments[0];
        }

        return fieldType;
    }

    /// <summary>
    /// True when <paramref name="converterType"/> implements
    /// <c>ISpanConverter&lt;T&gt;</c> for <paramref name="fieldType"/> (nullable fields use the underlying type).
    /// </summary>
    private static bool ImplementsSpanConverterForField(
        INamedTypeSymbol converterType,
        ITypeSymbol fieldType,
        Compilation compilation)
    {
        INamedTypeSymbol? ifaceTemplate = compilation.GetTypeByMetadataName("ModernLibs.FileHelpers.Converters.ISpanConverter`1");
        if (ifaceTemplate is null)
            return false;

        ITypeSymbol expected = UnwrapNullableFieldType(fieldType);
        foreach (INamedTypeSymbol iface in converterType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, ifaceTemplate))
                continue;
            if (iface.TypeArguments.Length != 1)
                continue;
            ITypeSymbol typeArg = iface.TypeArguments[0];
            if (SymbolEqualityComparer.Default.Equals(typeArg, expected))
                return true;
            if (SymbolEqualityComparer.Default.Equals(UnwrapNullableFieldType(typeArg), expected))
                return true;
        }

        return false;
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
}
