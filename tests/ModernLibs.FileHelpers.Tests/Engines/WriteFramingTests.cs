// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// E12–E14: append semantics and header/footer emission follow FileHelpers 3.x.
/// </summary>
[Category("ReadCore")]
public sealed class WriteFramingTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), "fh-write-" + Guid.NewGuid().ToString("N") + ".txt");

    [Test]
    [DisplayName("E12: BeginAppendFile clears HeaderText/FooterText so nothing is written mid-file")]
    public async Task BeginAppendFile_ClearsHeaderAndFooter()
    {
        // Arrange: file written with a header/footer; engine still holds both texts
        string path = TempPath();
        await File.WriteAllTextAsync(path, "HEAD\r\n1,a\r\nFOOT\r\n");
        using var engine = new FileHelperAsyncEngine<RcRecord> { HeaderText = "HEAD", FooterText = "FOOT" };

        // Act: append one record
        string content;
        try
        {
            engine.BeginAppendFile(path);
            engine.WriteNext(new RcRecord { Id = 2, Name = "b" });
            engine.Close();
            content = await File.ReadAllTextAsync(path);
        }
        finally
        {
            File.Delete(path);
        }

        // Assert: only the record was appended
        await Assert.That(content).IsEqualTo("HEAD\r\n1,a\r\nFOOT\r\n2,b\r\n");
        await Assert.That(engine.HeaderText).IsNull();
    }

    [Test]
    [DisplayName("E13: AppendToFile adds the missing trailing newline before the first appended record")]
    public async Task AppendToFile_MissingTrailingNewline_IsCorrected()
    {
        // Arrange: existing file without a final line terminator
        string path = TempPath();
        await File.WriteAllTextAsync(path, "1,a");
        var engine = new FileHelperEngine<RcRecord>();

        // Act: append through both overloads
        string content;
        try
        {
            engine.AppendToFile(path, new RcRecord { Id = 2, Name = "b" });
            await File.AppendAllTextAsync(path, "3,c");
            engine.AppendToFile(path, [new RcRecord { Id = 4, Name = "d" }]);
            content = await File.ReadAllTextAsync(path);
        }
        finally
        {
            File.Delete(path);
        }

        // Assert: every record on its own line, no blank lines
        await Assert.That(content).IsEqualTo("1,a\r\n2,b\r\n3,c\r\n4,d\r\n");
    }

    [Test]
    [DisplayName("E14: empty header/footer write nothing; a trailing newline in them is not doubled")]
    public async Task HeaderFooter_EmptyAndNewlineTerminated()
    {
        // Arrange: one engine with empty texts, one with newline-terminated texts
        var empty = new FileHelperEngine<RcRecord> { HeaderText = string.Empty, FooterText = string.Empty };
        var terminated = new FileHelperEngine<RcRecord> { HeaderText = "H\r\n", FooterText = "F\r\n" };
        using var streaming = new FileHelperAsyncEngine<RcRecord> { HeaderText = "H\r\n", FooterText = string.Empty };
        RcRecord[] records = [new RcRecord { Id = 1, Name = "a" }];

        // Act: write with each
        string emptyOut = empty.WriteString(records);
        string terminatedOut = terminated.WriteString(records);
        streaming.BeginWriteString();
        streaming.WriteNext(records[0]);
        string streamingOut = streaming.EndWriteString();

        // Assert: 3.x framing
        await Assert.That(emptyOut).IsEqualTo("1,a\r\n");
        await Assert.That(terminatedOut).IsEqualTo("H\r\n1,a\r\nF\r\n");
        await Assert.That(streamingOut).IsEqualTo("H\r\n1,a\r\n");
    }

    [Test]
    [DisplayName("BeginAppendFile adds the missing trailing newline before the first appended record")]
    public async Task BeginAppendFile_AddsMissingTrailingNewLine()
    {
        // Arrange: existing file without a final terminator
        string path = TempPath();
        await File.WriteAllTextAsync(path, "1,a");
        try
        {
            using var engine = new FileHelperAsyncEngine<RcRecord>();

            // Act
            engine.BeginAppendFile(path);
            engine.WriteNext(new RcRecord { Id = 2, Name = "b" });
            engine.Close();

            // Assert: same as FileHelperEngine.AppendToFile
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("1,a\r\n2,b\r\n");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("Appending to a file that holds only a byte order mark adds no empty line")]
    public async Task AppendToFile_AfterEmptyWriteFile_NoEmptyLine()
    {
        // Arrange: WriteFile with no records leaves just the UTF-8 BOM
        string path = TempPath();
        var engine = new FileHelperEngine<RcRecord>();
        engine.WriteFile(path, []);
        try
        {
            // Act
            engine.AppendToFile(path, new RcRecord { Id = 9, Name = "z" });

            // Assert: the appended record is the first line and reads back
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("9,z\r\n");
            await Assert.That(engine.ReadFile(path).Length).IsEqualTo(1);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
