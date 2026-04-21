// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test fixtures per file — intentional for per-field delimiter tests.
#pragma warning disable MA0048
// MA0025: Fixture bodies intentionally empty — engine populates fields via source-generated code.
#pragma warning disable MA0025
// CS0649: Public fields/properties are assigned by the source-generated descriptor.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

// ----- Test Fixtures for Per-Field Delimiter Tests -----

/// <summary>
/// Mixed-delimiter record: comma at record level, semicolon override for Field2.
/// Input format: "value1;value2,value3"
/// </summary>
[DelimitedRecord(",")]
internal sealed class MixedDelimiterRecord
{
    [FieldOrder(1)] public string Field1 = "";
    [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
    [FieldOrder(3)] public string Field3 = "";
}

/// <summary>
/// Record with single field delimiter override on second field.
/// Input format: "value1;value2"
/// </summary>
[DelimitedRecord(",")]
internal sealed class SingleFieldDelimiterOverride
{
    [FieldOrder(1)] public string Field1 = "";
    [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
}

/// <summary>
/// Record with all fields having unique delimiters.
/// </summary>
[DelimitedRecord(",")]
internal sealed class AllUniqueDelimitersRecord
{
    [FieldOrder(1)] [FieldDelimiter(";")] public string Field1 = "";
    [FieldOrder(2)] [FieldDelimiter("|")] public string Field2 = "";
    [FieldOrder(3)] [FieldDelimiter("\t")] public string Field3 = "";
}

/// <summary>
/// TDD RED tests for per-field delimiter end-to-end engine integration (FDEL-02).
/// These tests verify that FileHelperEngine and FileHelperAsyncEngine correctly
/// parse and write mixed-delimiter records.
/// </summary>
[Category("Phase17")]
[Category("Engine")]
public sealed class PerFieldDelimiterEngineTests
{
    /// <summary>
    /// FDEL-02: FileHelperEngine parses mixed-delimiter record correctly.
    /// Input: "a;b,c" → Field1="a", Field2="b", Field3="c"
    /// </summary>
    [Test]
    [DisplayName("FileHelperEngine parses mixed comma-semicolon delimiter record correctly")]
    public async Task ReadString_MixedDelimiters_ParsesCorrectly()
    {
        // Arrange: Create engine for MixedDelimiterRecord
        var engine = new FileHelperEngine<MixedDelimiterRecord>();
        var input = "a;b,c"; // Field1 uses default ",", Field2 uses ";", Field3 uses default ","

        // Act: Parse the mixed-delimiter string
        var records = engine.ReadString(input);

        // Assert: All fields parsed correctly
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Field1).IsEqualTo("a");
        await Assert.That(records[0].Field2).IsEqualTo("b");
        await Assert.That(records[0].Field3).IsEqualTo("c");
    }

    /// <summary>
    /// FDEL-02: FileHelperEngine parses multiple mixed-delimiter records.
    /// </summary>
    [Test]
    [DisplayName("FileHelperEngine parses multiple mixed-delimiter records")]
    public async Task ReadString_MultipleMixedDelimiters_ParsesCorrectly()
    {
        // Arrange: Create engine for MixedDelimiterRecord
        var engine = new FileHelperEngine<MixedDelimiterRecord>();
        var input = "a;b,c\nd;e,f"; // Two records with mixed delimiters

        // Act: Parse the multi-line input
        var records = engine.ReadString(input);

        // Assert: Both records parsed correctly
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Field1).IsEqualTo("a");
        await Assert.That(records[0].Field2).IsEqualTo("b");
        await Assert.That(records[0].Field3).IsEqualTo("c");
        await Assert.That(records[1].Field1).IsEqualTo("d");
        await Assert.That(records[1].Field2).IsEqualTo("e");
        await Assert.That(records[1].Field3).IsEqualTo("f");
    }

    /// <summary>
    /// FDEL-02: Write operation uses correct delimiters per field.
    /// Record: a="x", b="y", c="z" → Output: "x;y,z"
    /// </summary>
    [Test]
    [DisplayName("WriteString uses correct mixed delimiters per field")]
    public async Task WriteString_MixedDelimiters_FormatsCorrectly()
    {
        // Arrange: Create engine and record
        var engine = new FileHelperEngine<MixedDelimiterRecord>();
        var records = new[]
        {
            new MixedDelimiterRecord { Field1 = "x", Field2 = "y", Field3 = "z" }
        };

        // Act: Format the record
        string output = engine.WriteString(records);

        // Assert: Output uses correct delimiters (semicolon before Field2, comma before Field3)
        await Assert.That(output).IsEqualTo("x;y,z");
    }

    /// <summary>
    /// FDEL-02: Single field delimiter override works correctly.
    /// </summary>
    [Test]
    [DisplayName("Engine with single field delimiter override works for read and write")]
    public async Task SingleFieldDelimiter_OverrideWorks()
    {
        // Arrange: Create engine with single field override
        var engine = new FileHelperEngine<SingleFieldDelimiterOverride>();
        var input = "first;second";

        // Act: Parse the input
        var records = engine.ReadString(input);

        // Assert: Fields parsed correctly
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Field1).IsEqualTo("first");
        await Assert.That(records[0].Field2).IsEqualTo("second");

        // Act: Write back
        string output = engine.WriteString(records);

        // Assert: Output preserves semicolon delimiter
        await Assert.That(output).IsEqualTo("first;second");
    }

    /// <summary>
    /// FDEL-02: Round-trip read then write preserves mixed delimiters.
    /// </summary>
    [Test]
    [DisplayName("Round-trip read-write preserves mixed delimiter format")]
    public async Task RoundTrip_MixedDelimiters_PreservesDataAndFormat()
    {
        // Arrange: Create engine and original input
        var engine = new FileHelperEngine<MixedDelimiterRecord>();
        var originalInput = "hello;world,test";

        // Act: Read and then write
        var records = engine.ReadString(originalInput);
        string output = engine.WriteString(records);

        // Assert: Output matches original format
        await Assert.That(output).IsEqualTo(originalInput);

        // Act: Read the output again to verify data integrity
        var roundTripped = engine.ReadString(output);

        // Assert: Data preserved correctly
        await Assert.That(roundTripped.Length).IsEqualTo(1);
        await Assert.That(roundTripped[0].Field1).IsEqualTo("hello");
        await Assert.That(roundTripped[0].Field2).IsEqualTo("world");
        await Assert.That(roundTripped[0].Field3).IsEqualTo("test");
    }

    /// <summary>
    /// FDEL-02: FileHelperAsyncEngine parses mixed-delimiter records correctly.
    /// </summary>
    [Test]
    [DisplayName("FileHelperAsyncEngine parses mixed-delimiter records via ReadNext")]
    public async Task AsyncEngine_ReadNext_MixedDelimiters_Works()
    {
        // Arrange: Create async engine and use BeginReadString
        using var engine = new FileHelperAsyncEngine<MixedDelimiterRecord>();
        var input = "a;b,c\nd;e,f";

        // Act: Begin read and iterate using ReadNext
        engine.BeginReadString(input);
        var records = new System.Collections.Generic.List<MixedDelimiterRecord>();
        MixedDelimiterRecord? record;
        while ((record = engine.ReadNext()) != null)
        {
            records.Add(record);
        }

        // Assert: Both records parsed correctly
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].Field1).IsEqualTo("a");
        await Assert.That(records[0].Field2).IsEqualTo("b");
        await Assert.That(records[0].Field3).IsEqualTo("c");
        await Assert.That(records[1].Field1).IsEqualTo("d");
        await Assert.That(records[1].Field2).IsEqualTo("e");
        await Assert.That(records[1].Field3).IsEqualTo("f");
    }

    /// <summary>
    /// FDEL-02: Record with all fields having unique delimiters works correctly.
    /// </summary>
    [Test]
    [DisplayName("Engine handles all fields with unique delimiters")]
    public async Task AllUniqueDelimiters_Works()
    {
        // Arrange: Create engine with all unique delimiters
        var engine = new FileHelperEngine<AllUniqueDelimitersRecord>();
        var input = "a;b|c\td"; // semicolon, pipe, tab

        // Act: Parse the input
        var records = engine.ReadString(input);

        // Assert: All fields parsed correctly
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Field1).IsEqualTo("a");
        await Assert.That(records[0].Field2).IsEqualTo("b");
        await Assert.That(records[0].Field3).IsEqualTo("c");
    }
}
