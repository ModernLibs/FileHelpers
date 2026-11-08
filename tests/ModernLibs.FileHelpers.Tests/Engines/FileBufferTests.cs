// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord(",")]
public sealed class FbLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text { get; set; } = string.Empty;
}

/// <summary>
/// The file-path reads and writes give the same records and bytes as the in-memory ones, whatever the encoding,
/// line length or position of a line terminator relative to the I/O buffers.
/// </summary>
[Category("ReadCore")]
public sealed class FileBufferTests
{
    /// <summary>
    /// CRLF lines of 1 to 997 characters (so terminators land on every offset of the 4 KB, 16 KB and 64 KB
    /// buffers), a few lines longer than 16 K characters, and non-ASCII text.
    /// </summary>
    private static string Content()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1500; i++)
        {
            int length = i % 50 == 49 ? 20_000 + i : (i * 37 % 997) + 1;
            sb.Append(i).Append(',').Append(i % 3 == 0 ? 'ż' : 'x', length).Append("\r\n");
        }

        return sb.ToString();
    }

    private static IEnumerable<Encoding> Encodings() =>
        [new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), new UnicodeEncoding(bigEndian: false, byteOrderMark: true)];

    [Test]
    [DisplayName("ReadFile, ReadFile with a callback and ReadFileAsync read what ReadString reads (UTF-8 with and without BOM, UTF-16)")]
    public async Task FileReads_MatchReadString()
    {
        var content = Content();
        var expected = new FileHelperEngine<FbLine>().ReadString(content);

        foreach (var encoding in Encodings())
        {
            // Arrange: the content written with the encoding; the engine's own encoding is the default (UTF-8),
            // so for UTF-16 the byte order mark decides
            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, content, encoding);
                var engine = new FileHelperEngine<FbLine>();

                // Act: every file read path
                var array = engine.ReadFile(path);
                var callback = new List<FbLine>();
                engine.ReadFile(path, callback.Add);
                var asyncCallback = new List<FbLine>();
                await engine.ReadFileAsync(path, asyncCallback.Add);

                // Assert: the same records as ReadString
                foreach (var actual in new[] { array, callback.ToArray(), asyncCallback.ToArray() })
                {
                    await Assert.That(actual.Length).IsEqualTo(expected.Length);
                    for (int i = 0; i < expected.Length; i++)
                    {
                        await Assert.That(actual[i].Id).IsEqualTo(expected[i].Id);
                        await Assert.That(actual[i].Text).IsEqualTo(expected[i].Text);
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    [DisplayName("WriteFile writes the bytes of WriteString saved with the engine's encoding")]
    public async Task WriteFile_MatchesWriteString()
    {
        // Arrange: the records of the buffer test content
        var engine = new FileHelperEngine<FbLine>();
        var records = engine.ReadString(Content());
        var path = Path.GetTempFileName();
        var expectedPath = Path.GetTempFileName();
        try
        {
            // Act
            engine.WriteFile(path, records);
            await File.WriteAllTextAsync(expectedPath, engine.WriteString(records), engine.Encoding);

            // Assert: byte for byte
            var actual = await File.ReadAllBytesAsync(path);
            var expected = await File.ReadAllBytesAsync(expectedPath);
            await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
        }
        finally
        {
            File.Delete(path);
            File.Delete(expectedPath);
        }
    }

    [Test]
    [DisplayName("Reading a small file and appending one record allocate no large-object-heap buffers")]
    public async Task SmallFileOperations_StayOffTheLargeObjectHeap()
    {
        // Arrange: a ten-line file; warm up both operations
        var engine = new FileHelperEngine<FbLine>();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, string.Concat(Enumerable.Range(1, 10).Select(i => $"{i},text{i}\r\n")));
            int count = 0;
            engine.ReadFile(path, _ => count++);
            engine.AppendToFile(path, new FbLine { Id = 11, Text = "x" });

            // Act: allocation of one small read and one append
            long before = GC.GetAllocatedBytesForCurrentThread();
            engine.ReadFile(path, _ => count++);
            long read = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            engine.AppendToFile(path, new FbLine { Id = 12, Text = "y" });
            long append = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert: the reader buffer is sized to the file (4.8.0: about 9 KB); the writer buffers stay below the
            // 85,000-byte large object threshold (a 64 KB writer allocated 140 KB)
            await Assert.That(read).IsLessThan(16 * 1024);
            await Assert.That(append).IsLessThan(64 * 1024);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("AppendToFile after a file without a final line terminator still starts a new line")]
    public async Task AppendToFile_MatchesWriteString()
    {
        // Arrange: a file holding one record without a terminator
        var engine = new FileHelperEngine<FbLine>();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "1,a", engine.Encoding);

            // Act
            engine.AppendToFile(path, [new FbLine { Id = 2, Text = "b" }, new FbLine { Id = 3, Text = "c" }]);

            // Assert
            var text = await File.ReadAllTextAsync(path, engine.Encoding);
            await Assert.That(text).IsEqualTo("1,a\r\n2,b\r\n3,c\r\n");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
