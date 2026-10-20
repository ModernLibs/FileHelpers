// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture type kept next to the test that uses it.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>Three quoted fields whose values may not span lines.</summary>
[DelimitedRecord(",")]
public sealed class SingleLineQuoted
{
    [FieldOrder(1)] [FieldQuoted(QuoteMode.OptionalForRead, MultilineMode.NotAllow)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldQuoted(QuoteMode.OptionalForRead, MultilineMode.NotAllow)] public string B { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldQuoted(QuoteMode.OptionalForRead, MultilineMode.NotAllow)] public string C { get; set; } = string.Empty;
}

/// <summary>An unclosed quote in a single-line quoted field is the same 3.x error in any position.</summary>
[Category("Errors")]
public sealed class UnclosedQuoteTests
{
    [Test]
    [Arguments("\"abc,x,y", "A")]
    [Arguments("x,\"abc,y", "B")]
    [Arguments("x,y,\"abc", "C")]
    [DisplayName("Unclosed quote in a single-line field throws BadUsageException naming the field")]
    public async Task UnclosedQuote_AnyField_ThrowsBadUsage(string input, string field)
    {
        // Arrange
        var engine = new FileHelperEngine<SingleLineQuoted>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString(input));

        // Assert: BadUsageException (not a field-count FileHelpersException) for the open field
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(ex!.Message).Contains("unclosed quoted string");
        await Assert.That(ex.Message).Contains($"'{field}'");
    }
}
