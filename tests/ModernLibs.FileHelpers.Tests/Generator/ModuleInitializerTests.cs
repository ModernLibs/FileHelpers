// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Generator.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// Integration test: verifies the generated [ModuleInitializer] registers the
/// descriptor in FileHelperRegistry&lt;T&gt; before any user code runs.
/// Requires SimpleDelimitedRecord to be compiled with the generator active (done at build time).
/// </summary>
[Category("Generator")]
public sealed class ModuleInitializerTests
{
    /// <summary>FileHelperRegistry&lt;SimpleDelimitedRecord&gt;.Descriptor is non-null after module load.</summary>
    [Test]
    [DisplayName("FileHelperRegistry<SimpleDelimitedRecord>.Descriptor is non-null after module init")]
    public async Task Registry_AfterModuleInit_DescriptorIsNonNull()
    {
        // The generator processed SimpleDelimitedRecord at compile time.
        // The emitted [ModuleInitializer] ran before this test method executed.
        // FileHelperRegistry<SimpleDelimitedRecord>.Descriptor must therefore be non-null.
        // Arrange: Get descriptor from registry
        var descriptor = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;

        // Assert: Descriptor is non-null
        await Assert.That(descriptor).IsNotNull();

        // Snapshot Fields into an array before any await — ReadOnlySpan<T> cannot cross await boundaries.
        int fieldCount = descriptor.Fields.Length;
        // Copy individual field values before awaiting assertions.
        string field0Name = descriptor.Fields[0].Name;
        string field1Name = descriptor.Fields[1].Name;
        string field2Name = descriptor.Fields[2].Name;

        await Assert.That(fieldCount).IsEqualTo(3);

        // Fields are sorted by Order (1, 2, 3 from [FieldOrder] attributes)
        await Assert.That(field0Name).IsEqualTo(nameof(SimpleDelimitedRecord.Symbol));
        await Assert.That(field1Name).IsEqualTo(nameof(SimpleDelimitedRecord.Quantity));
        await Assert.That(field2Name).IsEqualTo(nameof(SimpleDelimitedRecord.Price));
    }

    /// <summary>FileHelperRegistry&lt;RecordClassFixture&gt;.Descriptor is non-null after module load.</summary>
    [Test]
    [DisplayName("FileHelperRegistry<RecordClassFixture>.Descriptor is non-null after module init")]
    public async Task Registry_AfterModuleInit_RecordClassDescriptorIsNonNull()
    {
        // RecordClassFixture has [DelimitedRecord] and was compiled with the generator active.
        // The emitted [ModuleInitializer] must have registered the descriptor before this test runs.
        // Arrange: Get descriptor from registry
        var descriptor = FileHelperRegistry<RecordClassFixture>.Descriptor;

        // Assert: Descriptor is non-null
        await Assert.That(descriptor).IsNotNull();

        // Snapshot fields before any await — ReadOnlySpan<T> cannot cross await boundaries.
        int fieldCount = descriptor.Fields.Length;
        string field0Name = descriptor.Fields[0].Name;
        string field1Name = descriptor.Fields[1].Name;

        await Assert.That(fieldCount).IsEqualTo(2);
        await Assert.That(field0Name).IsEqualTo(nameof(RecordClassFixture.Name));
        await Assert.That(field1Name).IsEqualTo(nameof(RecordClassFixture.Age));
    }

    /// <summary>FileHelperRegistry&lt;ReadonlyRecordStructFixture&gt;.Descriptor is non-null after module load.</summary>
    [Test]
    [DisplayName("FileHelperRegistry<ReadonlyRecordStructFixture>.Descriptor is non-null after module init")]
    public async Task Registry_AfterModuleInit_ReadonlyRecordStructDescriptorIsNonNull()
    {
        // ReadonlyRecordStructFixture has [DelimitedRecord] and was compiled with the generator active.
        // Arrange: Get descriptor from registry
        var descriptor = FileHelperRegistry<ReadonlyRecordStructFixture>.Descriptor;

        // Assert: Descriptor is non-null
        await Assert.That(descriptor).IsNotNull();

        int fieldCount = descriptor.Fields.Length;
        string field0Name = descriptor.Fields[0].Name;
        string field1Name = descriptor.Fields[1].Name;

        await Assert.That(fieldCount).IsEqualTo(2);
        await Assert.That(field0Name).IsEqualTo(nameof(ReadonlyRecordStructFixture.Name));
        await Assert.That(field1Name).IsEqualTo(nameof(ReadonlyRecordStructFixture.Score));
    }
}
