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
    public async Task Parse_FieldTrimBoth_TrimsWhitespace()
    {
        // TrimmedRecord (Engine/Fixtures) uses [FieldTrim(TrimMode.Both)] on Name.
        var desc = FileHelperRegistry<TrimmedRecord>.Descriptor;
        var record = desc.Parse(" Alice ".AsSpan());
        await Assert.That(record.Name).IsEqualTo("Alice");
    }

    /// <summary>D-05: TrimMode.Left trims only leading whitespace.</summary>
    [Test]
    public async Task Parse_FieldTrimStart_TrimsLeading()
    {
        var desc = FileHelperRegistry<LeftTrimmedRecord>.Descriptor;
        var record = desc.Parse("  Bob".AsSpan());
        await Assert.That(record.Name).IsEqualTo("Bob");
    }

    /// <summary>D-05: TrimMode.Right trims only trailing whitespace.</summary>
    [Test]
    public async Task Parse_FieldTrimEnd_TrimsTrailing()
    {
        var desc = FileHelperRegistry<RightTrimmedRecord>.Descriptor;
        var record = desc.Parse("Carol  ".AsSpan());
        await Assert.That(record.Name).IsEqualTo("Carol");
    }

    /// <summary>D-05: TrimMode.Both trims edges only — internal spaces are preserved.</summary>
    [Test]
    public async Task Parse_FieldTrim_PreservesInternalSpaces()
    {
        var desc = FileHelperRegistry<TrimmedRecord>.Descriptor;
        var record = desc.Parse("Hello World".AsSpan());
        await Assert.That(record.Name).IsEqualTo("Hello World");
    }
}
