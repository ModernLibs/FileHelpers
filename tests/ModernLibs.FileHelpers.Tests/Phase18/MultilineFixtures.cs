// MULT-01: Test fixtures for multiline quoted field support
// TDD scaffold - these records test MultilineMode.AllowForRead

// MA0048: Multiple record types in single file for test fixtures - intentional grouping.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Tests.Phase18;

/// <summary>
/// Record type with multiline mode enabled for reading.
/// Used to test RFC 4180 multiline quoted field parsing.
/// </summary>
[DelimitedRecord(",")]
public class MultilineCustomer
{
    [FieldOrder(1)]
    public string Name { get; set; } = string.Empty;

    [FieldOrder(2)]
    public string Address { get; set; } = string.Empty;  // May contain newlines

    [FieldOrder(3)]
    public string City { get; set; } = string.Empty;
}

/// <summary>
/// Record type with default multiline mode (NotAllow).
/// Used for comparison testing - unclosed quotes should trigger ErrorMode.
/// </summary>
[DelimitedRecord(",")]
public class SingleLineCustomer
{
    [FieldOrder(1)]
    public string Name { get; set; } = string.Empty;

    [FieldOrder(2)]
    public string Address { get; set; } = string.Empty;

    [FieldOrder(3)]
    public string City { get; set; } = string.Empty;
}

/// <summary>
/// Record for testing delimiters inside multiline quoted fields.
/// </summary>
[DelimitedRecord(",")]
public class MixedDelimiterMultiline
{
    [FieldOrder(1)]
    public string Field1 { get; set; } = string.Empty;

    [FieldOrder(2)]
    public string Field2 { get; set; } = string.Empty;  // May contain commas and newlines

    [FieldOrder(3)]
    public string Field3 { get; set; } = string.Empty;
}

/// <summary>
/// Record specifically for testing 3+ line quoted fields.
/// </summary>
[DelimitedRecord(",")]
public class ThreeLineField
{
    [FieldOrder(1)]
    public string Id { get; set; } = string.Empty;

    [FieldOrder(2)]
    public string Description { get; set; } = string.Empty;  // May span 3+ lines

    [FieldOrder(3)]
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Record for testing escaped quotes inside multiline fields.
/// </summary>
[DelimitedRecord(",")]
public class EscapedQuotesMultiline
{
    [FieldOrder(1)]
    public string Title { get; set; } = string.Empty;

    [FieldOrder(2)]
    public string Content { get; set; } = string.Empty;  // May contain "" escapes

    [FieldOrder(3)]
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// Simple record for async streaming tests.
/// </summary>
[DelimitedRecord(",")]
public class AsyncMultilineRecord
{
    [FieldOrder(1)]
    public string Header { get; set; } = string.Empty;

    [FieldOrder(2)]
    public string Body { get; set; } = string.Empty;  // Multiline content
}
