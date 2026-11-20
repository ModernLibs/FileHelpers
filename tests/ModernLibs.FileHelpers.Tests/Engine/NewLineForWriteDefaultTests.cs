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
// MA0048: Fixture types for these tests are grouped in this file.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers.Tests.Async.MasterDetail;
using ModernLibs.FileHelpers.Tests.Async.MultiRecord;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>Two-field record for the newline default tests.</summary>
[DelimitedRecord(",")]
public sealed class NlRecord
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Two header and two footer lines, captured into HeaderText/FooterText on read.</summary>
[DelimitedRecord(",")]
[IgnoreFirst(2)]
[IgnoreLast(2)]
public sealed class NlFramed
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>A record whose second field starts on a new physical line.</summary>
[DelimitedRecord(",")]
public sealed class NlInNewLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldInNewLine] public string Name { get; set; } = string.Empty;
}

/// <summary>
/// T49 (owner decision, Rulings R39/R41): <c>NewLineForWrite</c> defaults to <see cref="Environment.NewLine"/>
/// on every engine, as in FileHelpers 3.5.2, so every write path uses it by default; an explicit <c>"\r\n"</c>
/// still gives the 4.8-4.9.1 bytes. Header and footer text on read: <see cref="HeaderFooterText3xTests"/>.
/// </summary>
[Category("Write")]
public sealed class NewLineForWriteDefaultTests
{
    private static readonly string NL = Environment.NewLine;

    private static readonly NlRecord[] s_records =
    [
        new() { Id = 1, Name = "a" },
        new() { Id = 2, Name = "b" },
    ];

    private static readonly object[] s_mixed =
    [
        new MultiAsyncHeader { Kind = "H", Batch = "B1" },
        new MultiAsyncDetail { Kind = "D", Id = 1, Amount = 2.5m },
    ];

    private static MultiRecordEngine NewMultiRecordEngine() => new(
        MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
        MultiRecordType.For<MultiAsyncDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));

    private static MasterDetailEngine<MdAsyncMaster, MdAsyncDetail> NewMasterDetailEngine() =>
        new(line => line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master : RecordAction.Detail);

    private static MasterDetails<MdAsyncMaster, MdAsyncDetail>[] Groups() =>
    [
        new(new MdAsyncMaster { Kind = "M", Id = "A" }, [new MdAsyncDetail { Kind = "D", MasterId = "A", Quantity = 2 }]),
    ];

    private static FileHelperEngine<NlRecord> Framed(string? newLine = null)
    {
        var engine = new FileHelperEngine<NlRecord> { HeaderText = "H", FooterText = "F" };
        if (newLine is not null)
            engine.NewLineForWrite = newLine;
        return engine;
    }

    private static string Expected(string nl) => $"H{nl}1,a{nl}2,b{nl}F{nl}";

    private static T WithTempFile<T>(Func<string, T> action)
    {
        string path = Path.GetTempFileName();
        try
        {
            return action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- Default value -------------------------------------------------------------------

    [Test]
    [DisplayName("Every engine's NewLineForWrite defaults to Environment.NewLine (FileHelpers 3.5.2)")]
    public async Task Default_IsEnvironmentNewLine_OnEveryEngine()
    {
        // Arrange: one of each engine, plus the interface views
        var engine = new FileHelperEngine<NlRecord>();
        using var asyncEngine = new FileHelperAsyncEngine<NlRecord>();
        IFileHelperEngine<NlRecord> viaInterface = new FileHelperEngine<NlRecord>();
        using IFileHelperAsyncEngine<NlRecord> viaAsyncInterface = new FileHelperAsyncEngine<NlRecord>();

        // Act + Assert: every default is the platform newline
        await Assert.That(engine.NewLineForWrite).IsEqualTo(NL);
        await Assert.That(asyncEngine.NewLineForWrite).IsEqualTo(NL);
        await Assert.That(NewMultiRecordEngine().NewLineForWrite).IsEqualTo(NL);
        await Assert.That(NewMasterDetailEngine().NewLineForWrite).IsEqualTo(NL);
        await Assert.That(viaInterface.NewLineForWrite).IsEqualTo(NL);
        await Assert.That(viaAsyncInterface.NewLineForWrite).IsEqualTo(NL);
    }

    // ---- FileHelperEngine write paths ----------------------------------------------------

    [Test]
    [DisplayName("WriteString frames header, records and footer with Environment.NewLine by default")]
    public async Task WriteString_Default_UsesEnvironmentNewLine()
    {
        // Act
        string text = Framed().WriteString(s_records);

        // Assert
        await Assert.That(text).IsEqualTo(Expected(NL));
    }

    [Test]
    [DisplayName("WriteStream(TextWriter) uses Environment.NewLine by default")]
    public async Task WriteStream_Default_UsesEnvironmentNewLine()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        Framed().WriteStream(writer, s_records);

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo(Expected(NL));
    }

    [Test]
    [DisplayName("WriteFile and WriteStreamAsync(Stream/TextWriter) write the same Environment.NewLine bytes by default")]
    public async Task WriteFile_And_WriteStreamAsync_Default_SameBytes()
    {
        // Arrange
        var engine = Framed();

        // Act
        byte[] fileBytes = WithTempFile(path =>
        {
            engine.WriteFile(path, s_records);
            return File.ReadAllBytes(path);
        });
        using var stream = new MemoryStream();
        await engine.WriteStreamAsync(stream, s_records);
        using var writer = new StringWriter();
        await engine.WriteStreamAsync(writer, s_records);

        // Assert: WriteFile is the reference; the async stream overload matches it byte for byte
        await Assert.That(engine.Encoding.GetString(fileBytes).TrimStart('\uFEFF')).IsEqualTo(Expected(NL));
        await Assert.That(stream.ToArray().AsSpan().SequenceEqual(fileBytes)).IsTrue();
        await Assert.That(writer.ToString()).IsEqualTo(Expected(NL));
    }

    [Test]
    [DisplayName("AppendToFile terminates an unterminated file and each record with Environment.NewLine by default")]
    public async Task AppendToFile_Default_UsesEnvironmentNewLine()
    {
        // Arrange: a file that does not end with a line terminator
        var engine = new FileHelperEngine<NlRecord>();

        // Act
        string text = WithTempFile(path =>
        {
            File.WriteAllText(path, "X");
            engine.AppendToFile(path, s_records[0]);
            engine.AppendToFile(path, [s_records[1]]);
            return File.ReadAllText(path);
        });

        // Assert
        await Assert.That(text).IsEqualTo($"X{NL}1,a{NL}2,b{NL}");
    }

    [Test]
    [DisplayName("The line break inside a [FieldInNewLine] record is Environment.NewLine by default")]
    public async Task FieldInNewLine_Default_UsesEnvironmentNewLine()
    {
        // Act
        string text = new FileHelperEngine<NlInNewLine>().WriteString([new NlInNewLine { Id = 1, Name = "a" }]);

        // Assert: the delimiter stays at the end of the first line, as in 3.x
        await Assert.That(text).IsEqualTo($"1,{NL}a{NL}");
    }

    // ---- Other engines -------------------------------------------------------------------

    [Test]
    [DisplayName("FileHelperAsyncEngine write sessions use Environment.NewLine by default")]
    public async Task AsyncEngine_Default_UsesEnvironmentNewLine()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<NlRecord> { HeaderText = "H", FooterText = "F" };

        // Act: string session and file session
        engine.BeginWriteString();
        engine.WriteNext(s_records);
        string text = engine.EndWriteString();
        string fileText = WithTempFile(path =>
        {
            engine.BeginWriteFile(path);
            engine.WriteNext(s_records[0]);
            engine.WriteNext(s_records[1]);
            engine.Close();
            return File.ReadAllText(path);
        });

        // Assert
        await Assert.That(text).IsEqualTo(Expected(NL));
        await Assert.That(fileText).IsEqualTo(Expected(NL));
    }

    [Test]
    [DisplayName("MultiRecordEngine and MasterDetailEngine use Environment.NewLine by default")]
    public async Task MultiRecord_And_MasterDetail_Default_UseEnvironmentNewLine()
    {
        // Act
        string multi = NewMultiRecordEngine().WriteString(s_mixed);
        string masterDetail = NewMasterDetailEngine().WriteString(Groups());

        // Assert
        await Assert.That(multi).IsEqualTo($"H|B1{NL}D|1|2.5{NL}");
        await Assert.That(masterDetail).IsEqualTo($"M|A{NL}D|A|2{NL}");
    }

    // ---- Explicit "\r\n" keeps the 4.9.1 bytes -------------------------------------------

    [Test]
    [DisplayName("NewLineForWrite = \"\\r\\n\" gives the 4.9.1 bytes on every write path and engine")]
    public async Task ExplicitCrLf_GivesFourNineOneBytes()
    {
        // Arrange
        const string Crlf = "\r\n";
        var engine = Framed(Crlf);
        using var asyncEngine = new FileHelperAsyncEngine<NlRecord> { HeaderText = "H", FooterText = "F", NewLineForWrite = Crlf };
        var multi = NewMultiRecordEngine();
        multi.NewLineForWrite = Crlf;
        var masterDetail = NewMasterDetailEngine();
        masterDetail.NewLineForWrite = Crlf;

        // Act
        string text = engine.WriteString(s_records);
        byte[] fileBytes = WithTempFile(path =>
        {
            engine.WriteFile(path, s_records);
            return File.ReadAllBytes(path);
        });
        using var stream = new MemoryStream();
        await engine.WriteStreamAsync(stream, s_records);
        string appended = WithTempFile(path =>
        {
            File.WriteAllText(path, "X");
            engine.AppendToFile(path, s_records[0]);
            return File.ReadAllText(path);
        });
        asyncEngine.BeginWriteString();
        asyncEngine.WriteNext(s_records);
        string asyncText = asyncEngine.EndWriteString();
        string inNewLine = new FileHelperEngine<NlInNewLine> { NewLineForWrite = Crlf }.WriteString([new NlInNewLine { Id = 1, Name = "a" }]);

        // Assert
        await Assert.That(text).IsEqualTo("H\r\n1,a\r\n2,b\r\nF\r\n");
        await Assert.That(Encoding.UTF8.GetString(fileBytes).TrimStart('\uFEFF')).IsEqualTo("H\r\n1,a\r\n2,b\r\nF\r\n");
        await Assert.That(stream.ToArray().AsSpan().SequenceEqual(fileBytes)).IsTrue();
        await Assert.That(appended).IsEqualTo("X\r\n1,a\r\n");
        await Assert.That(asyncText).IsEqualTo("H\r\n1,a\r\n2,b\r\nF\r\n");
        await Assert.That(inNewLine).IsEqualTo("1,\r\na\r\n");
        await Assert.That(multi.WriteString(s_mixed)).IsEqualTo("H|B1\r\nD|1|2.5\r\n");
        await Assert.That(masterDetail.WriteString(Groups())).IsEqualTo("M|A\r\nD|A|2\r\n");
    }

    // ---- null, and no hard-coded "\r\n" in the output --------------------------------------

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [DisplayName("NewLineForWrite = null or \"\" throws ArgumentException with the 3.5.2 message on every engine")]
    public async Task NullOrEmpty_Throws_OnEveryEngine(string? value)
    {
        // Arrange
        var engine = new FileHelperEngine<NlRecord>();
        using var asyncEngine = new FileHelperAsyncEngine<NlRecord>();
        var multi = NewMultiRecordEngine();
        var masterDetail = NewMasterDetailEngine();

        // Act: 3.5.2 (probed) throws System.ArgumentException "NewLine string must not be null or empty" from the setter
        Exception?[] errors =
        [
            Errors.Capture.Exception(() => engine.NewLineForWrite = value!),
            Errors.Capture.Exception(() => asyncEngine.NewLineForWrite = value!),
            Errors.Capture.Exception(() => multi.NewLineForWrite = value!),
            Errors.Capture.Exception(() => masterDetail.NewLineForWrite = value!),
        ];

        // Assert: exact type and message; the previous value is kept
        foreach (var error in errors)
        {
            await Assert.That(error?.GetType()).IsEqualTo(typeof(ArgumentException));
            await Assert.That(error!.Message).IsEqualTo("NewLine string must not be null or empty");
        }

        await Assert.That(engine.NewLineForWrite).IsEqualTo(NL);
        await Assert.That(asyncEngine.NewLineForWrite).IsEqualTo(NL);
        await Assert.That(multi.NewLineForWrite).IsEqualTo(NL);
        await Assert.That(masterDetail.NewLineForWrite).IsEqualTo(NL);
    }

    [Test]
    [DisplayName("With NewLineForWrite = \"\\n\" no write path emits a CR (no hard-coded \"\\r\\n\" reaches the output)")]
    public async Task NoHardCodedCrLf_ReachesTheOutput()
    {
        // Arrange: framing, a [FieldInNewLine] record, every engine
        const string Lf = "\n";
        var inNewLine = new[] { new NlInNewLine { Id = 1, Name = "a" }, new NlInNewLine { Id = 2, Name = "b" } };
        var engine = new FileHelperEngine<NlInNewLine> { HeaderText = "H", FooterText = "F", NewLineForWrite = Lf };
        using var asyncEngine = new FileHelperAsyncEngine<NlInNewLine> { HeaderText = "H", FooterText = "F", NewLineForWrite = Lf };
        var multi = new MultiRecordEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<NlInNewLine>(line => !line.StartsWith("H|", StringComparison.Ordinal)))
        {
            NewLineForWrite = Lf,
        };
        var masterDetail = new MasterDetailEngine<MdAsyncMaster, NlInNewLine>(
            line => line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master : RecordAction.Detail)
        {
            NewLineForWrite = Lf,
        };

        // Act
        var outputs = new List<string> { engine.WriteString(inNewLine) };
        using (var writer = new StringWriter())
        {
            engine.WriteStream(writer, inNewLine);
            outputs.Add(writer.ToString());
        }

        using (var writer = new StringWriter())
        {
            await engine.WriteStreamAsync(writer, inNewLine);
            outputs.Add(writer.ToString());
        }

        using (var stream = new MemoryStream())
        {
            await engine.WriteStreamAsync(stream, inNewLine);
            outputs.Add(Encoding.UTF8.GetString(stream.ToArray()));
        }

        outputs.Add(WithTempFile(path =>
        {
            engine.WriteFile(path, inNewLine);
            File.AppendAllText(path, "X");
            engine.AppendToFile(path, inNewLine);
            return File.ReadAllText(path);
        }));
        asyncEngine.BeginWriteString();
        asyncEngine.WriteNext(inNewLine);
        outputs.Add(asyncEngine.EndWriteString());
        outputs.Add(multi.WriteString([new MultiAsyncHeader { Kind = "H", Batch = "B1" }, inNewLine[0]]));
        outputs.Add(masterDetail.WriteString([new MasterDetails<MdAsyncMaster, NlInNewLine>(new MdAsyncMaster { Kind = "M", Id = "A" }, inNewLine)]));

        // Assert: every output has line breaks, none has a CR
        foreach (string output in outputs)
        {
            await Assert.That(output.Contains('\n', StringComparison.Ordinal)).IsTrue();
            await Assert.That(output.Contains('\r', StringComparison.Ordinal)).IsFalse().Because(output);
        }
    }
}
