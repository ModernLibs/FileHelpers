// Test fixtures for multiline quoted field support
// These records test MultilineMode.AllowForRead

// MA0048: Multiple record types in single file for test fixtures - intentional grouping.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Record type with multiline mode enabled for reading.
/// Used to test RFC 4180 multiline quoted field parsing.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
[IgnoreCommentedLines('#', anyPlace: false)]
public class MultilineCustomer
{
    [FieldOrder(1)]
    [FieldQuoted]
    public string Name { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted]
    public string Address { get; set; } = string.Empty;  // May contain newlines

    [FieldOrder(3)]
    [FieldQuoted]
    public string City { get; set; } = string.Empty;
}

/// <summary>
/// Record type with multiline values disabled (record-level NotAllow overrides the [FieldQuoted]
/// default AllowForBoth). Used for comparison testing - unclosed quotes should trigger ErrorMode.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.NotAllow)]
public class SingleLineCustomer
{
    [FieldOrder(1)]
    [FieldQuoted]
    public string Name { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted]
    public string Address { get; set; } = string.Empty;

    [FieldOrder(3)]
    [FieldQuoted]
    public string City { get; set; } = string.Empty;
}

/// <summary>
/// Record for testing delimiters inside multiline quoted fields.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public class MixedDelimiterMultiline
{
    [FieldOrder(1)]
    [FieldQuoted]
    public string Field1 { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted]
    public string Field2 { get; set; } = string.Empty;  // May contain commas and newlines

    [FieldOrder(3)]
    [FieldQuoted]
    public string Field3 { get; set; } = string.Empty;
}

/// <summary>
/// Record specifically for testing 3+ line quoted fields.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public class ThreeLineField
{
    [FieldOrder(1)]
    public string Id { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted]
    public string Description { get; set; } = string.Empty;  // May span 3+ lines

    [FieldOrder(3)]
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Record for testing escaped quotes inside multiline fields.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public class EscapedQuotesMultiline
{
    [FieldOrder(1)]
    public string Title { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted]
    public string Content { get; set; } = string.Empty;  // May contain "" escapes

    [FieldOrder(3)]
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// Simple record for async streaming tests.
/// </summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public class AsyncMultilineRecord
{
    [FieldOrder(1)]
    public string Header { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted]
    public string Body { get; set; } = string.Empty;  // Multiline content
}
