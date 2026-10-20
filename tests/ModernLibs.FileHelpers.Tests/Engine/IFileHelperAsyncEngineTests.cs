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
/// Tests verifying interface-based usage patterns for <see cref="IFileHelperAsyncEngine{T}"/>.
/// Confirms FileHelperAsyncEngine{T} can be used through its interface for DI and mocking scenarios.
/// </summary>
[Category("Engine")]
[Category("Interfaces")]
public sealed class IFileHelperAsyncEngineTests
{
    /// <summary>Verify FileHelperAsyncEngine{T} can be assigned to IFileHelperAsyncEngine{T}.</summary>
    [Test]
    [DisplayName("Async engine is assignable to IFileHelperAsyncEngine interface")]
    public async Task Engine_IsAssignable_ToInterface()
    {
        // Arrange: Create concrete engine instance
        var engine = new FileHelperAsyncEngine<CsvOrder>();

        // Act: Assign to interface variable
        IFileHelperAsyncEngine<CsvOrder> interfaceRef = engine;

        // Assert: Interface reference is not null
        await Assert.That(interfaceRef).IsNotNull();
    }

    /// <summary>BeginRead/ReadNext/Close works through the IFileHelperAsyncEngine interface.</summary>
    [Test]
    [DisplayName("BeginRead/ReadNext/Close works through IFileHelperAsyncEngine interface")]
    public async Task BeginRead_ReadNext_Close_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperAsyncEngine<CsvOrder> engine = new FileHelperAsyncEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Begin read, read records, close
        engine.BeginReadString(input);
        var record1 = engine.ReadNext();
        var record2 = engine.ReadNext();
        var record3 = engine.ReadNext();
        engine.Close();

        // Assert: First two calls return records, third returns null (EOF)
        await Assert.That(record1).IsNotNull();
        await Assert.That(record1!.OrderId).IsEqualTo(1);
        await Assert.That(record2).IsNotNull();
        await Assert.That(record2!.OrderId).IsEqualTo(2);
        await Assert.That(record3).IsNull();
    }

    /// <summary>BeginWrite/WriteNext/EndWrite works through the IFileHelperAsyncEngine interface.</summary>
    [Test]
    [DisplayName("BeginWrite/WriteNext/EndWrite works through IFileHelperAsyncEngine interface")]
    public async Task BeginWrite_WriteNext_EndWrite_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperAsyncEngine<CsvOrder> engine = new FileHelperAsyncEngine<CsvOrder>();
        var records = new CsvOrder[]
        {
            new() { OrderId = 1, CustomerName = "Alice", Amount = 100.50m },
            new() { OrderId = 2, CustomerName = "Bob", Amount = 200.00m },
        };

        // Act: Begin write, write records, end write
        engine.BeginWriteString();
        engine.WriteNext(records[0]);
        engine.WriteNext(records[1]);
        var result = engine.EndWriteString();

        // Assert: Result contains both records
        await Assert.That(result).Contains("Alice");
        await Assert.That(result).Contains("Bob");
    }

    /// <summary>foreach works through the IFileHelperAsyncEngine interface (IEnumerable{T}).</summary>
    [Test]
    [DisplayName("foreach works through IFileHelperAsyncEngine interface")]
    public async Task Foreach_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperAsyncEngine<CsvOrder> engine = new FileHelperAsyncEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Begin read and iterate via foreach
        engine.BeginReadString(input);
        var list = new List<CsvOrder>();
        foreach (var record in engine)
        {
            list.Add(record);
        }
        engine.Close();

        // Assert: Correct record count
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0].CustomerName).IsEqualTo("Alice");
        await Assert.That(list[1].CustomerName).IsEqualTo("Bob");
    }

    /// <summary>Dispose works through the IFileHelperAsyncEngine interface.</summary>
    [Test]
    [DisplayName("Dispose works through IFileHelperAsyncEngine interface")]
    public async Task Dispose_Works_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperAsyncEngine<CsvOrder> engine = new FileHelperAsyncEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50";

        // Act: Begin read, then dispose through interface
        engine.BeginReadString(input);
        ((IDisposable)engine).Dispose();

        // Assert: Engine can be reopened after dispose (proves dispose didn't corrupt state)
        engine.BeginReadString(input);
        var record = engine.ReadNext();
        engine.Close();
        await Assert.That(record).IsNotNull();
    }

    /// <summary>Properties accessible through the IFileHelperAsyncEngine interface.</summary>
    [Test]
    [DisplayName("Properties accessible through IFileHelperAsyncEngine interface")]
    public async Task Properties_Accessible_ThroughInterface()
    {
        // Arrange: Create engine via interface
        IFileHelperAsyncEngine<CsvOrder> engine = new FileHelperAsyncEngine<CsvOrder>();

        // Assert: Initial property values
        await Assert.That(engine.HeaderText).IsNull();
        await Assert.That(engine.LastRecord).IsNull();
        await Assert.That(engine.Encoding).IsEqualTo(Encoding.UTF8);
        await Assert.That(engine.ErrorManager).IsNotNull();
    }

    /// <summary>Events fire through the IFileHelperAsyncEngine interface.</summary>
    [Test]
    [DisplayName("Events fire through IFileHelperAsyncEngine interface")]
    public async Task Events_Fire_ThroughInterface()
    {
        // Arrange: Create engine via interface and subscribe to event
        IFileHelperAsyncEngine<CsvOrder> engine = new FileHelperAsyncEngine<CsvOrder>();
        bool beforeReadFired = false;
        engine.BeforeReadRecord += (_, _) => beforeReadFired = true;

        var input = "Header,Header,Header\r\n1,Alice,100.50";

        // Act: Read through interface
        engine.BeginReadString(input);
        _ = engine.ReadNext();
        engine.Close();

        // Assert: Event was fired
        await Assert.That(beforeReadFired).IsTrue();
    }

    /// <summary>DI pattern works — method accepting IFileHelperAsyncEngine parameter.</summary>
    [Test]
    [DisplayName("DI pattern works with IFileHelperAsyncEngine parameter")]
    public async Task DI_Pattern_Works_InterfaceParameter()
    {
        // Arrange: Create concrete engine
        var engine = new FileHelperAsyncEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Pass to method accepting interface
        var result = await ProcessRecordsAsync(engine, input);

        // Assert: Method returned records through interface parameter
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].OrderId).IsEqualTo(1);
    }

    private static async Task<List<CsvOrder>> ProcessRecordsAsync(IFileHelperAsyncEngine<CsvOrder> engine, string content)
    {
        var list = new List<CsvOrder>();
        engine.BeginReadString(content);
        while (engine.ReadNext() is { } record)
        {
            list.Add(record);
            await Task.CompletedTask;
        }
        engine.Close();
        return list;
    }
}
