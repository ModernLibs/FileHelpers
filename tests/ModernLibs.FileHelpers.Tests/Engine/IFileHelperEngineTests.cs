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

using System.Text;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Tests verifying interface-based usage patterns for <see cref="IFileHelperEngine{T}"/>.
/// Confirms FileHelperEngine{T} can be used through its interface for DI and mocking scenarios.
/// </summary>
[Category("Phase49")]
[Category("Engine")]
[Category("Interfaces")]
public sealed class IFileHelperEngineTests
{
    /// <summary>Verify FileHelperEngine{T} can be assigned to IFileHelperEngine{T}.</summary>
    [Test]
    [DisplayName("Sync engine is assignable to IFileHelperEngine interface")]
    public async Task Engine_IsAssignable_ToInterface()
    {
        // Arrange: Create concrete engine instance
        var engine = new FileHelperEngine<CsvOrder>();

        // Act: Assign to interface variable
        IFileHelperEngine<CsvOrder> interfaceRef = engine;

        // Assert: Interface reference is not null
        await Assert.That(interfaceRef).IsNotNull();
    }

    /// <summary>ReadString works through the IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("ReadString works through IFileHelperEngine interface")]
    public async Task ReadString_Works_ThroughInterface()
    {
        // Arrange: Create engine and assign to interface
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Call ReadString through interface
        var records = engine.ReadString(input);

        // Assert: Records parsed correctly
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].OrderId).IsEqualTo(1);
        await Assert.That(records[0].CustomerName).IsEqualTo("Alice");
        await Assert.That(records[1].OrderId).IsEqualTo(2);
    }

    /// <summary>WriteString works through the IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("WriteString works through IFileHelperEngine interface")]
    public async Task WriteString_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface with sample records
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();
        var records = new CsvOrder[]
        {
            new() { OrderId = 1, CustomerName = "Alice", Amount = 100.50m },
            new() { OrderId = 2, CustomerName = "Bob", Amount = 200.00m },
        };

        // Act: Write string through interface
        var result = engine.WriteString(records);

        // Assert: Result contains both records
        await Assert.That(result).Contains("Alice");
        await Assert.That(result).Contains("Bob");
    }

    /// <summary>Properties are accessible through the IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("Properties accessible through IFileHelperEngine interface")]
    public async Task Properties_Accessible_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();

        // Act: Set and read properties through interface
        engine.HeaderText = "header";

        // Assert: Properties return expected values
        await Assert.That(engine.HeaderText).IsEqualTo("header");
        await Assert.That(engine.Encoding).IsEqualTo(Encoding.UTF8);
        await Assert.That(engine.NewLineForWrite).IsEqualTo("\r\n");
        await Assert.That(engine.ErrorManager).IsNotNull();
    }

    /// <summary>ErrorMode is settable through the IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("ErrorMode settable through IFileHelperEngine interface")]
    public async Task ErrorMode_Settable_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();

        // Act: Set ErrorMode through interface
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;

        // Assert: ErrorMode reflects the set value
        await Assert.That(engine.ErrorMode).IsEqualTo(ErrorMode.IgnoreAndContinue);
    }

    /// <summary>Events fire through the IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("Events fire through IFileHelperEngine interface")]
    public async Task Events_Fire_ThroughInterface()
    {
        // Arrange: Create engine via interface and subscribe to event
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();
        bool beforeReadFired = false;
        engine.BeforeReadRecord += (_, _) => beforeReadFired = true;

        var input = "Header,Header,Header\r\n1,Alice,100.50";

        // Act: Read through interface
        _ = engine.ReadString(input);

        // Assert: Event was fired
        await Assert.That(beforeReadFired).IsTrue();
    }

    /// <summary>ReadSync streaming works through the IFileHelperEngine interface.</summary>
    [Test]
    [DisplayName("ReadSync streaming works through IFileHelperEngine interface")]
    public async Task ReadSync_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Call ReadSync through interface and iterate
        var records = engine.ReadString(input);

        // Assert: Correct records returned
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].CustomerName).IsEqualTo("Alice");
        await Assert.That(records[1].CustomerName).IsEqualTo("Bob");
    }

    /// <summary>DI pattern works — method accepting IFileHelperEngine parameter.</summary>
    [Test]
    [DisplayName("DI pattern works with IFileHelperEngine parameter")]
    public async Task DI_Pattern_Works_InterfaceParameter()
    {
        // Arrange: Create concrete engine
        var engine = new FileHelperEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Pass to method accepting interface
        var result = ProcessRecords(engine, input);

        // Assert: Method returned records through interface parameter
        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result[0].OrderId).IsEqualTo(1);
    }

    private static T[] ProcessRecords<T>(IFileHelperEngine<T> engine, string content)
    {
        return engine.ReadString(content);
    }
}
