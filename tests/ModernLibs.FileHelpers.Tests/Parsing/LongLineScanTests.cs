// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Diagnostics;
using ModernLibs.FileHelpers.Tests.Engines;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Streaming line sources scan a long pending line once (linear), not once per buffer fill (quadratic).
/// </summary>
[Category("Parsing")]
[NotInParallel]
public sealed class LongLineScanTests
{
    [Test]
    [DisplayName("ReadAsync of a 16 MB single line takes linear time (perf guard)")]
    public async Task ReadAsync_SixteenMegabyteLine_IsLinear()
    {
        // Arrange: one record whose second field is 16 M characters
        const int length = 16 * 1024 * 1024;
        string path = Path.Combine(Path.GetTempPath(), "fh-longline-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, "1," + new string('x', length) + "\r\n2,y\r\n");
        try
        {
            var engine = new FileHelperEngine<RcRecord>();
            _ = engine.ReadFile(path); // warm up
            var sync = Stopwatch.StartNew();
            var viaSync = engine.ReadFile(path);
            sync.Stop();

            // Act: the same input through the PipeReader-based async path
            var async = Stopwatch.StartNew();
            var viaAsync = new List<int>();
            int longest = 0;
            await foreach (var r in engine.ReadAsync(path))
            {
                viaAsync.Add(r.Id);
                longest = Math.Max(longest, r.Name.Length);
            }

            async.Stop();

            // Assert: same result; generous bound (the quadratic rescan took ~45x the sync time)
            await Assert.That(viaSync.Length).IsEqualTo(2);
            await Assert.That(string.Join(",", viaAsync)).IsEqualTo("1,2");
            await Assert.That(longest).IsEqualTo(length);
            await Assert.That(async.ElapsedMilliseconds).IsLessThan((10 * sync.ElapsedMilliseconds) + 400);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
