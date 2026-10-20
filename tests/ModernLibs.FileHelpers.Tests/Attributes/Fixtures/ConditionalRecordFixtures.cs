// CS0649: Fields are assigned by the FileHelpers engine via source-generated code, not in user code.
#pragma warning disable CS0649
// CS8618: Non-nullable fields are initialized by the source-generated parser, not constructors.
#pragma warning disable CS8618
// CA1852: Types are sealed where appropriate; internal test fixtures don't need sealing.
#pragma warning disable CA1852
// MA0048: Multiple test types per file — intentional for related test fixtures.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Attributes.Fixtures;

// IncludeIfBegins test record - only lines starting with "HDR"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfBegins, "HDR")]
internal class IncludeIfBeginsRecord
{
    [FieldOrder(1)]
    public string Prefix;

    [FieldOrder(2)]
    public int Id;

    [FieldOrder(3)]
    public string Name;
}

// IncludeIfContains test record - only lines containing "Error"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfContains, "Error")]
internal class IncludeIfContainsRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    public string Name;

    [FieldOrder(3)]
    public string Status;
}

// IncludeIfEnds test record - only lines ending with ".txt"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfEnds, ".txt")]
internal class IncludeIfEndsRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    public string Filename;
}

// IncludeIfRegex test record - only lines matching "^\d{4}-"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfRegex, @"^\d{4}-")]
internal class IncludeIfRegexRecord
{
    [FieldOrder(1)]
    public string YearPrefix;

    [FieldOrder(2)]
    public string Name;

    [FieldOrder(3)]
    public string Extra;
}

// ExcludeIfBegins test record - exclude lines starting with "#"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.ExcludeIfBegins, "#")]
internal class ExcludeIfBeginsRecord
{
    [FieldOrder(1)]
    public string Data;

    [FieldOrder(2)]
    public int Id;

    [FieldOrder(3)]
    public string Extra;
}

// ExcludeIfContains test record - exclude lines containing "Skip"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.ExcludeIfContains, "Skip")]
internal class ExcludeIfContainsRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    public string Name;

    [FieldOrder(3)]
    public string Extra;
}

// ExcludeIfEnds test record - exclude lines ending with ".tmp"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.ExcludeIfEnds, ".tmp")]
internal class ExcludeIfEndsRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    public string Filename;
}

// ExcludeIfRegex test record - exclude lines matching "^X-"
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.ExcludeIfRegex, "^X-")]
internal class ExcludeIfRegexRecord
{
    [FieldOrder(1)]
    public string Data;

    [FieldOrder(2)]
    public int Id;

    [FieldOrder(3)]
    public string Extra;
}

// Record without ConditionalRecord for parity testing
[DelimitedRecord(",")]
internal class NoConditionalRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    public string Name;

    [FieldOrder(3)]
    public string Extra;
}

// Record for pre-parse filtering test - malformed skipped lines should not throw
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.ExcludeIfBegins, "SKIP")]
internal class PreParseFilterRecord
{
    [FieldOrder(1)]
    public string Data;

    [FieldOrder(2)]
    public int Id;

    [FieldOrder(3)]
    public string Extra;
}

#pragma warning restore CS0649
#pragma warning restore CA1852
