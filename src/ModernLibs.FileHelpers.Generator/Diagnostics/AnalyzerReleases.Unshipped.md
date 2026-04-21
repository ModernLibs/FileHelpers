; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MLFH001 | FileHelpers.Generator | Error | Missing record attribute
MLFH002 | FileHelpers.Generator | Error | Missing or non-positive [FieldFixedLength]
MLFH003 | FileHelpers.Generator | Error | Ambiguous field order
MLFH004 | FileHelpers.Generator | Warning | Unrecognised [FieldConverter(typeof(X))] type
MLFH005 | FileHelpers.Generator | Error | Invalid ConditionalRecord regex pattern
