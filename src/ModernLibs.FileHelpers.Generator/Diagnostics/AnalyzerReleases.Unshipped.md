; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MLFH030 | FileHelpers.Generator | Warning | Quote character is part of the delimiter
MLFH031 | FileHelpers.Generator | Warning | Delimiter contains a line break
MLFH032 | FileHelpers.Generator | Info | Trim characters contain the delimiter or the quote character
