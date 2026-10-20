; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 4.8.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MLFH001 | FileHelpers.Generator | Error | Missing record attribute
MLFH002 | FileHelpers.Generator | Error | Missing or non-positive [FieldFixedLength]
MLFH003 | FileHelpers.Generator | Error | Ambiguous field order
MLFH004 | FileHelpers.Generator | Error | Unrecognised [FieldConverter(typeof(X))] type
MLFH005 | FileHelpers.Generator | Error | Invalid ConditionalRecord regex pattern
MLFH006 | FileHelpers.Generator | Error | FieldConverter target type lacks parameterless constructor
MLFH007 | FileHelpers.Generator | Warning | FieldFixedLength exceeds configured maximum width
MLFH008 | FileHelpers.Generator | Warning | FieldOrder has gaps in sequence
MLFH009 | FileHelpers.Generator | Warning | Record type has no public fields or properties
MLFH010 | FileHelpers.Generator | Error | FieldNullValue type is incompatible with the field type
MLFH011 | FileHelpers.Generator | Error | Conflicting record attributes
MLFH012 | FileHelpers.Generator | Error | Empty delimiter
MLFH013 | FileHelpers.Generator | Error | Unsupported field type
MLFH014 | FileHelpers.Generator | Error | Record type cannot be constructed
MLFH015 | FileHelpers.Generator | Error | Annotated member cannot be assigned
MLFH016 | FileHelpers.Generator | Error | Generic record types are not supported
MLFH017 | FileHelpers.Generator | Error | Record type is not accessible
MLFH018 | FileHelpers.Generator | Error | Required field after an optional field
MLFH019 | FileHelpers.Generator | Error | ConverterKind does not match the field type
MLFH020 | FileHelpers.Generator | Error | Descriptor generation failed
MLFH021 | FileHelpers.Generator | Error | Invalid converter arguments
MLFH022 | FileHelpers.Generator | Error | No converter constructor for the converter arguments
MLFH023 | FileHelpers.Generator | Error | Annotated member is not accessible
MLFH024 | FileHelpers.Generator | Error | [FieldOrder] must be used on all fields or none
MLFH025 | FileHelpers.Generator | Error | [FieldQuoted] is only valid in delimited records
MLFH026 | FileHelpers.Generator | Error | C# 11 or later is required
MLFH027 | FileHelpers.Generator | Error | ref struct records are not supported
MLFH028 | FileHelpers.Generator | Warning | Member mapped by FileHelpers 3.x is not part of the layout
MLFH029 | FileHelpers.Generator | Warning | [FieldInNewLine] first field with [IgnoreEmptyLines]
