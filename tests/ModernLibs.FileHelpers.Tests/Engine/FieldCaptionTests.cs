// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Tests verifying FieldCaption attribute functionality (GAP-02).
/// </summary>
[Category("Phase50")]
[Category("Engine")]
[Category("FieldCaption")]
public sealed class FieldCaptionTests
{
    /// <summary>FieldDescriptor contains caption from FieldCaption attribute.</summary>
    [Test]
    [DisplayName("FieldDescriptor contains caption from FieldCaption attribute")]
    public async Task FieldDescriptor_ContainsCaption_WhenAttributePresent()
    {
        // Arrange: Get descriptor for captioned record type
        var descriptor = FileHelperRegistry<CaptionedRecord>.Descriptor;

        // Act: Access field captions
        var field0 = descriptor.Fields[0];
        var field1 = descriptor.Fields[1];

        // Assert: First two fields have captions
        await Assert.That(field0.Caption).IsEqualTo("Order ID");
        await Assert.That(field1.Caption).IsEqualTo("Customer Name");
    }

    /// <summary>FieldDescriptor caption is null when FieldCaption attribute absent.</summary>
    [Test]
    [DisplayName("FieldDescriptor caption is null when FieldCaption attribute absent")]
    public async Task FieldDescriptor_CaptionIsNull_WhenAttributeAbsent()
    {
        // Arrange: Get descriptor for captioned record type
        var descriptor = FileHelperRegistry<CaptionedRecord>.Descriptor;

        // Act: Access third field (Amount has no [FieldCaption])
        var field = descriptor.Fields[2];

        // Assert: Caption is null
        await Assert.That(field.Caption).IsNull();
    }

    /// <summary>Engine reads correctly with captioned fields through interface.</summary>
    [Test]
    [DisplayName("Engine reads correctly with captioned fields through IFileHelperEngine interface")]
    public async Task Engine_Reads_CorrectlyWithCaptionedFields()
    {
        // Arrange: Create engine and assign to interface
        IFileHelperEngine<CaptionedRecord> engine = new FileHelperEngine<CaptionedRecord>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Read through interface
        var records = engine.ReadString(input);

        // Assert: Records parsed correctly
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[0].Name).IsEqualTo("Alice");
        await Assert.That(records[1].Id).IsEqualTo(2);
    }

    /// <summary>WriteString works correctly with captioned fields.</summary>
    [Test]
    [DisplayName("WriteString works correctly with captioned fields")]
    public async Task WriteString_Works_WithCaptionedFields()
    {
        // Arrange: Create engine with captioned records
        IFileHelperEngine<CaptionedRecord> engine = new FileHelperEngine<CaptionedRecord>();
        var records = new CaptionedRecord[]
        {
            new() { Id = 1, Name = "Alice", Amount = 100.50m },
            new() { Id = 2, Name = "Bob", Amount = 200.00m },
        };

        // Act: Write string through interface
        var result = engine.WriteString(records);

        // Assert: Result contains both records
        await Assert.That(result).Contains("1,Alice,100.50");
        await Assert.That(result).Contains("2,Bob,200.00");
    }
}
