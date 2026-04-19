// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related engine property stubs.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Text;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// GREEN acceptance tests for <see cref="FileHelperEngine{T}"/> properties
/// (ENGI-08 LineNumber/TotalRecords, ENGI-16 Encoding, D-09 NewLineForWrite default).
/// </summary>
[Category("Phase07")]
[Category("Engine")]
public sealed class EnginePropertiesTests
{
    /// <summary>ENGI-08: LineNumber reflects the 1-based index of the last successfully parsed line.</summary>
    [Test]
    public async Task LineNumber_AfterRead_ReflectsLastParsedLine()
    {
        // CsvOrder has [IgnoreFirst(1)] — header on line 1, data on lines 2 and 3.
        // Last parsed line is the second data line → original line number 3.
        var engine = new FileHelperEngine<CsvOrder>();
        var input = "HEADER\r\n1,Alice,10\r\n2,Bob,20";
        _ = engine.ReadString(input);

        await Assert.That(engine.LineNumber).IsEqualTo(3);
    }

    /// <summary>ENGI-08: TotalRecords equals the count of records returned from the last Read call.</summary>
    [Test]
    public async Task TotalRecords_AfterRead_ReflectsCount()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var input = "HEADER\r\n1,Alice,10\r\n2,Bob,20\r\n3,Carol,30";
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(engine.TotalRecords).IsEqualTo(3);
    }

    /// <summary>
    /// ENGI-08: TotalRecords stays coherent with the last call — verified here by calling
    /// Read twice with different counts and confirming the second call overwrites the first.
    /// (Write tests live in Wave 4; this placeholder proves the read-side coherence only.)
    /// </summary>
    [Test]
    public async Task TotalRecords_AfterWrite_ReflectsCount()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        _ = engine.ReadString("HEADER\r\n1,Alice,10\r\n2,Bob,20\r\n3,Carol,30");
        await Assert.That(engine.TotalRecords).IsEqualTo(3);

        // Second read with a different count must replace, not accumulate.
        _ = engine.ReadString("HEADER\r\n1,Alice,10");
        await Assert.That(engine.TotalRecords).IsEqualTo(1);
    }

    /// <summary>D-09: NewLineForWrite defaults to "\r\n" (FileHelpers 3.x compat).</summary>
    [Test]
    public async Task NewLineForWrite_DefaultIsCRLF()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        await Assert.That(engine.NewLineForWrite).IsEqualTo("\r\n");
    }

    /// <summary>ENGI-16: Encoding defaults to UTF-8.</summary>
    [Test]
    public async Task Encoding_DefaultIsUtf8()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        await Assert.That(engine.Encoding).IsEqualTo(Encoding.UTF8);
    }

    /// <summary>ENGI-16: Encoding can be changed (e.g., Latin1) and is honored on ReadFile.</summary>
    [Test]
    public async Task Encoding_CanBeChanged()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        engine.Encoding = Encoding.Latin1;
        await Assert.That(engine.Encoding).IsEqualTo(Encoding.Latin1);

        // Round-trip a plain-ASCII payload through a Latin1-encoded file: ReadFile must use
        // engine.Encoding to open the StreamReader. If the engine ignores the property
        // and falls back to UTF-8, plain ASCII still parses — but if the engine uses the
        // property, the same path still works and we have demonstrated the setter is honored.
        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_enc_{Guid.NewGuid():N}.csv");
        try
        {
            // Write with Latin1 (ASCII-only payload is byte-identical to UTF-8 here).
            await File.WriteAllTextAsync(tempPath, "HEADER\r\n99,Latin1Record,42.00", Encoding.Latin1);
            var records = engine.ReadFile(tempPath);

            await Assert.That(records.Length).IsEqualTo(1);
            await Assert.That(records[0].OrderId).IsEqualTo(99);
            await Assert.That(records[0].CustomerName).IsEqualTo("Latin1Record");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>
    /// IN-05: Real regression guard for <see cref="FileHelperEngine{T}.Encoding"/>. Writes a
    /// file with a non-ASCII payload using Latin1, then reads it back with the engine's
    /// Encoding set to Latin1. If the engine silently falls back to UTF-8 the decode will
    /// produce replacement / mojibake characters and the assertion fails — unlike the
    /// ASCII-only variant above which would pass under a faulty UTF-8 fallback.
    /// </summary>
    [Test]
    public async Task Encoding_NonAscii_IsHonoredOnReadFile()
    {
        var engine = new FileHelperEngine<CsvOrder>
        {
            Encoding = Encoding.Latin1,
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_enc_nonascii_{Guid.NewGuid():N}.csv");
        try
        {
            // "Résumé" encoded in Latin1 is two single-byte non-ASCII chars (0xE9). UTF-8
            // would encode the same string as two-byte sequences — decoding the Latin1 bytes
            // as UTF-8 produces replacement chars, so a successful match here proves the
            // Encoding setter is actually honored by ReadFile's StreamReader construction.
            await File.WriteAllTextAsync(tempPath, "HEADER\r\n99,Résumé,42.00", Encoding.Latin1);

            var records = engine.ReadFile(tempPath);

            await Assert.That(records.Length).IsEqualTo(1);
            await Assert.That(records[0].OrderId).IsEqualTo(99);
            await Assert.That(records[0].CustomerName).IsEqualTo("Résumé");
            await Assert.That(records[0].Amount).IsEqualTo(42.00m);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
