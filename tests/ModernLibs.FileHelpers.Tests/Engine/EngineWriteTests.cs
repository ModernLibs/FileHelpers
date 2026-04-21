// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related engine write tests.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests (small fixtures).
#pragma warning disable CA1849

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// GREEN acceptance tests for <see cref="FileHelperEngine{T}"/> write / append operations
/// (ENGI-04, ENGI-05, ENGI-06, ENGI-07, D-09, D-10, D-15).
/// </summary>
[Category("Phase07")]
[Category("Engine")]
public sealed class EngineWriteTests
{
    /// <summary>ENGI-06: WriteString formats a sequence of records into a single string.</summary>
    [Test]
    [DisplayName("WriteString formats single CsvOrder record into CSV string with CRLF terminator")]
    public async Task WriteString_DelimitedRecord_FormatsCorrectly()
    {
        // Arrange: Create engine and single CsvOrder record with OrderId=1, CustomerName=Alice, Amount=100.50
        var engine = new FileHelperEngine<CsvOrder>();
        var records = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100.50m },
        };

        // Act: Format records to CSV string
        var result = engine.WriteString(records);

        // Assert: Output matches expected CSV format with decimal scale preserved
        // Decimal preserves scale — "100.50m" renders as "100.50" via TryFormat invariant culture.
        await Assert.That(result).IsEqualTo("1,Alice,100.50\r\n");
    }

    /// <summary>D-10: HeaderText is written as the first line before any records.</summary>
    [Test]
    [DisplayName("WriteString writes HeaderText as first line before record data")]
    public async Task WriteString_WithHeader_WritesHeaderFirst()
    {
        // Arrange: Create engine with HeaderText set, and single CsvOrder record
        var engine = new FileHelperEngine<CsvOrder>
        {
            HeaderText = "Id,Name,Amount",
        };
        var records = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100m },
        };

        // Act: Format records with header to CSV string
        var result = engine.WriteString(records);

        // Assert: Header line appears first, followed by record data line
        await Assert.That(result).IsEqualTo("Id,Name,Amount\r\n1,Alice,100\r\n");
    }

    /// <summary>D-10: FooterText is written as the last line after all records.</summary>
    [Test]
    [DisplayName("WriteString writes FooterText as last line after record data")]
    public async Task WriteString_WithFooter_WritesFooterLast()
    {
        // Arrange: Create engine with FooterText set, and single CsvOrder record
        var engine = new FileHelperEngine<CsvOrder>
        {
            FooterText = "END-OF-FILE",
        };
        var records = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100m },
        };

        // Act: Format records with footer to CSV string
        var result = engine.WriteString(records);

        // Assert: Record line appears first, followed by footer line
        await Assert.That(result).IsEqualTo("1,Alice,100\r\nEND-OF-FILE\r\n");
    }

    /// <summary>ENGI-05: WriteStream writes all records to the supplied TextWriter.</summary>
    [Test]
    [DisplayName("WriteStream writes multiple records to TextWriter with CRLF line endings")]
    public async Task WriteStream_WritesToTextWriter()
    {
        // Arrange: Create engine, two CsvOrder records, and StringWriter for output capture
        var engine = new FileHelperEngine<CsvOrder>();
        var records = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 10m },
            new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 20m },
        };

        using var sw = new StringWriter();

        // Act: Write records to StringWriter via WriteStream
        engine.WriteStream(sw, records);

        // Assert: StringWriter contains both records with CRLF endings, TotalRecords=2
        await Assert.That(sw.ToString()).IsEqualTo("1,Alice,10\r\n2,Bob,20\r\n");
        await Assert.That(engine.TotalRecords).IsEqualTo(2);
    }

    /// <summary>ENGI-04: WriteFile creates a file on disk containing formatted records.</summary>
    [Test]
    [DisplayName("WriteFile creates disk file containing formatted CSV records")]
    public async Task WriteFile_CreatesFile()
    {
        // Arrange: Create temp file path, engine, and two CsvOrder records
        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_write_{Guid.NewGuid():N}.csv");
        try
        {
            var engine = new FileHelperEngine<CsvOrder>();
            var records = new[]
            {
                new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100m },
                new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 200m },
            };

            // Act: Write records to temp file via WriteFile
            engine.WriteFile(tempPath, records);

            // Assert: File exists and contains expected CSV content with CRLF endings
            await Assert.That(File.Exists(tempPath)).IsTrue();
            var content = await File.ReadAllTextAsync(tempPath);
            await Assert.That(content).IsEqualTo("1,Alice,100\r\n2,Bob,200\r\n");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>ENGI-07 + D-15: AppendToFile(path, record) appends a single record.</summary>
    [Test]
    [DisplayName("AppendToFile appends single record to existing file ignoring header/footer")]
    public async Task AppendToFile_SingleRecord_AppendsContent()
    {
        // Arrange: Create temp file with initial record, engine with header/footer (should be ignored)
        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_append_{Guid.NewGuid():N}.csv");
        try
        {
            // Seed file with an initial record.
            await File.WriteAllTextAsync(tempPath, "1,Alice,100\r\n");

            var engine = new FileHelperEngine<CsvOrder>
            {
                // HeaderText is set but MUST be ignored during append (D-15).
                HeaderText = "SHOULD-NOT-APPEAR",
                FooterText = "SHOULD-NOT-APPEAR-EITHER",
            };
            var record = new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 200m };

            engine.AppendToFile(tempPath, record);

            // Act: Read back file content after append operation
            var content = await File.ReadAllTextAsync(tempPath);

            // Assert: Original and appended records present, header/footer not added
            await Assert.That(content).IsEqualTo("1,Alice,100\r\n2,Bob,200\r\n");
            await Assert.That(engine.TotalRecords).IsEqualTo(1);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>ENGI-07 + D-15: AppendToFile(path, IEnumerable&lt;T&gt;) appends many records.</summary>
    [Test]
    [DisplayName("AppendToFile appends multiple records from enumerable to existing file")]
    public async Task AppendToFile_EnumerableRecords_AppendsAll()
    {
        // Arrange: Create temp file with initial record, engine, and three records to append
        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_append_many_{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(tempPath, "1,Alice,100\r\n");

            var engine = new FileHelperEngine<CsvOrder>();
            var records = new[]
            {
                new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 200m },
                new CsvOrder { OrderId = 3, CustomerName = "Carol", Amount = 300m },
                new CsvOrder { OrderId = 4, CustomerName = "Dave", Amount = 400m },
            };

            engine.AppendToFile(tempPath, records);

            // Act: Read back file content after appending three records
            var content = await File.ReadAllTextAsync(tempPath);

            // Assert: All four records present in correct order, TotalRecords=3
            await Assert.That(content).IsEqualTo(
                "1,Alice,100\r\n2,Bob,200\r\n3,Carol,300\r\n4,Dave,400\r\n");
            await Assert.That(engine.TotalRecords).IsEqualTo(3);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>ENGI-06: WriteString emits width-aligned fixed-length records.</summary>
    [Test]
    [DisplayName("WriteString formats fixed-length records with correct field widths and padding")]
    public async Task WriteString_FixedLength_FormatsCorrectly()
    {
        // Arrange: Create engine for FixedEmployee and single record
        var engine = new FileHelperEngine<FixedEmployee>();
        var records = new[]
        {
            new FixedEmployee { Id = 1, Name = "Alice", Salary = 1500.50m },
        };

        var result = engine.WriteString(records);

        // FixedEmployee: Id=6, Name=20, Salary=10 — total 36 chars per record.
        // Write then read back to confirm layout round-trips through the same descriptor.
        var readEngine = new FileHelperEngine<FixedEmployee>();

        // Act: Round-trip through write then read
        var roundTripped = readEngine.ReadString(result);

        // Assert: Record values preserved after round-trip, ends with CRLF
        await Assert.That(roundTripped.Length).IsEqualTo(1);
        await Assert.That(roundTripped[0].Id).IsEqualTo(1);
        await Assert.That(roundTripped[0].Name.Trim()).IsEqualTo("Alice");
        await Assert.That(roundTripped[0].Salary).IsEqualTo(1500.50m);
        await Assert.That(result.EndsWith("\r\n", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>D-09: NewLineForWrite="\n" causes writer to emit LF-only line endings.</summary>
    [Test]
    [DisplayName("WriteString uses LF-only line endings when NewLineForWrite is set to \\n")]
    public async Task WriteString_UsesNewLineForWrite()
    {
        // Arrange: Create engine with NewLineForWrite="\n", and two CsvOrder records
        var engine = new FileHelperEngine<CsvOrder>
        {
            NewLineForWrite = "\n",
        };
        var records = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 10m },
            new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 20m },
        };

        // Act: Format records with LF line endings
        var result = engine.WriteString(records);

        // Assert: Output uses LF only, no CR characters present
        await Assert.That(result).IsEqualTo("1,Alice,10\n2,Bob,20\n");
        await Assert.That(result.Contains('\r')).IsFalse();
    }

    /// <summary>Round-trip: records written via WriteString round-trip cleanly through ReadString.</summary>
    [Test]
    [DisplayName("Round-trip write then read preserves all field values through serialization")]
    public async Task RoundTrip_WriteRead_Delimited()
    {
        // Arrange: Create write engine with header, three CsvOrder records, and read engine
        var writeEngine = new FileHelperEngine<CsvOrder>
        {
            HeaderText = "HEADER",
        };
        var originals = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 10m },
            new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 20m },
            new CsvOrder { OrderId = 3, CustomerName = "Carol", Amount = 30m },
        };

        var serialized = writeEngine.WriteString(originals);

        // Read it back. CsvOrder has [IgnoreFirst(1)], so the HEADER line will be skipped.
        var readEngine = new FileHelperEngine<CsvOrder>();

        // Act: Parse serialized output back into records
        var roundTripped = readEngine.ReadString(serialized);

        // Assert: All three records round-trip with identical field values
        await Assert.That(roundTripped.Length).IsEqualTo(3);
        for (int i = 0; i < originals.Length; i++)
        {
            await Assert.That(roundTripped[i].OrderId).IsEqualTo(originals[i].OrderId);
            await Assert.That(roundTripped[i].CustomerName).IsEqualTo(originals[i].CustomerName);
            await Assert.That(roundTripped[i].Amount).IsEqualTo(originals[i].Amount);
        }
    }

    /// <summary>
    /// IN-01: End-to-end round-trip for a positional record struct through the engine.
    /// Exercises the positional-constructor Parse emission path (struct + all init-only →
    /// <c>new T(__v0, __v1, __v2)</c>) that was previously only covered by generator-only
    /// tests — this guards the engine-level boundary.
    /// </summary>
    [Test]
    [DisplayName("Round-trip positional record struct preserves all values through write and read")]
    public async Task RoundTrip_PositionalRecordStruct_WriteRead()
    {
        // Arrange: Create engine for PositionalOrder and three records
        var engine = new FileHelperEngine<PositionalOrder>();
        var originals = new[]
        {
            new PositionalOrder(1, "Alice", 10.50m),
            new PositionalOrder(2, "Bob", 20.75m),
            new PositionalOrder(3, "Carol", 30.00m),
        };

        var serialized = engine.WriteString(originals);

        // Act: Parse serialized output back into positional records
        var roundTripped = engine.ReadString(serialized);

        // Assert: All three records round-trip with identical values and TotalRecords=3
        await Assert.That(roundTripped.Length).IsEqualTo(3);
        await Assert.That(roundTripped[0]).IsEqualTo(originals[0]);
        await Assert.That(roundTripped[1]).IsEqualTo(originals[1]);
        await Assert.That(roundTripped[2]).IsEqualTo(originals[2]);
        await Assert.That(engine.TotalRecords).IsEqualTo(3);
    }

    /// <summary>
    /// IN-07: Decimal preserves scale across the Format path. `100m` and `100.00m` are
    /// numerically equal but produce different string output ("100" vs "100.00") and users
    /// migrating from FileHelpers 3.x rely on the scale being preserved.
    /// </summary>
    [Test]
    [DisplayName("WriteString preserves decimal scale rendering 100m as 100 and 100.00m as 100.00")]
    public async Task WriteString_Decimal_PreservesScale()
    {
        // Arrange: Create engine for CsvOrder
        var engine = new FileHelperEngine<CsvOrder>();

        // Act: Format records with different decimal scales (100m vs 100.00m)
        var r1 = engine.WriteString(new[] { new CsvOrder { OrderId = 1, CustomerName = "X", Amount = 100m } });
        var r2 = engine.WriteString(new[] { new CsvOrder { OrderId = 1, CustomerName = "X", Amount = 100.00m } });

        // Assert: Different string representations for different decimal scales
        await Assert.That(r1).IsEqualTo("1,X,100\r\n");
        await Assert.That(r2).IsEqualTo("1,X,100.00\r\n");
    }
}
