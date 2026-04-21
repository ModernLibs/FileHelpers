// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using ModernLibs.FileHelpers.Tests.Generator.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// GREEN tests for generator-emitted [FieldTrim] handling (D-05).
/// Exercises the runtime Parse path on fixtures annotated with each TrimMode variant.
/// </summary>
[Category("Phase07")]
[Category("Generator")]
public sealed class GeneratorFieldTrimTests
{
    /// <summary>D-05: TrimMode.Both trims leading AND trailing whitespace after field slicing.</summary>
    [Test]
    [DisplayName("Parse applies FieldTrim(Both) trimming leading and trailing whitespace")]
    public async Task Parse_FieldTrimBoth_TrimsWhitespace()
    {
        // TrimmedRecord (Engine/Fixtures) uses [FieldTrim(TrimMode.Both)] on Name.
        // Arrange: Get descriptor for TrimmedRecord and prepare input with surrounding spaces
        var desc = FileHelperRegistry<TrimmedRecord>.Descriptor;

        // Act: Parse input with leading/trailing spaces
        var record = desc.Parse(" Alice ".AsSpan());

        // Assert: Spaces trimmed from both ends, result is "Alice"
        await Assert.That(record.Name).IsEqualTo("Alice");
    }

    /// <summary>D-05: TrimMode.Left trims only leading whitespace.</summary>
    [Test]
    [DisplayName("Parse applies FieldTrim(Left) trimming only leading whitespace")]
    public async Task Parse_FieldTrimStart_TrimsLeading()
    {
        // Arrange: Get descriptor for LeftTrimmedRecord
        var desc = FileHelperRegistry<LeftTrimmedRecord>.Descriptor;

        // Act: Parse input with leading spaces
        var record = desc.Parse("  Bob".AsSpan());

        // Assert: Leading spaces trimmed, result is "Bob"
        await Assert.That(record.Name).IsEqualTo("Bob");
    }

    /// <summary>D-05: TrimMode.Right trims only trailing whitespace.</summary>
    [Test]
    [DisplayName("Parse applies FieldTrim(Right) trimming only trailing whitespace")]
    public async Task Parse_FieldTrimEnd_TrimsTrailing()
    {
        // Arrange: Get descriptor for RightTrimmedRecord
        var desc = FileHelperRegistry<RightTrimmedRecord>.Descriptor;

        // Act: Parse input with trailing spaces
        var record = desc.Parse("Carol  ".AsSpan());

        // Assert: Trailing spaces trimmed, result is "Carol"
        await Assert.That(record.Name).IsEqualTo("Carol");
    }

    /// <summary>D-05: TrimMode.Both trims edges only — internal spaces are preserved.</summary>
    [Test]
    [DisplayName("Parse FieldTrim(Both) preserves internal spaces within field")]
    public async Task Parse_FieldTrim_PreservesInternalSpaces()
    {
        // Arrange: Get descriptor for TrimmedRecord
        var desc = FileHelperRegistry<TrimmedRecord>.Descriptor;

        // Act: Parse input with internal spaces
        var record = desc.Parse("Hello World".AsSpan());

        // Assert: Internal space preserved, result is "Hello World"
        await Assert.That(record.Name).IsEqualTo("Hello World");
    }
}
