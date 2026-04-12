// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Compile-verification tests for ParseContext and FormatContext ref structs (PIPE-05, PIPE-06).
/// These tests verify that ParseContext and FormatContext are usable as stack-allocated ref structs.
/// All ref struct work is done synchronously; only primitive results are awaited.
/// </summary>
[Category("Phase05")]
[Category("Parsing")]
public sealed class ParseContextTests
{
    [Test]
    public async Task ParseContext_IsRefStruct_Compiles()
    {
        ParseContext ctx = new();
        ctx.LineNumber = 5;
        ctx.SkipRecord = true;
        int lineNumber = ctx.LineNumber;
        bool skipRecord = ctx.SkipRecord;

        await Assert.That(lineNumber).IsEqualTo(5);
        await Assert.That(skipRecord).IsTrue();
    }

    [Test]
    public async Task ParseContext_DefaultValues_AreZeroAndFalse()
    {
        ParseContext ctx = default;
        int lineNumber = ctx.LineNumber;
        bool skipRecord = ctx.SkipRecord;

        await Assert.That(lineNumber).IsEqualTo(0);
        await Assert.That(skipRecord).IsFalse();
    }

    [Test]
    public async Task FormatContext_IsRefStruct_Compiles()
    {
        FormatContext ctx = new();
        ctx.RecordsWritten = 3;
        int recordsWritten = ctx.RecordsWritten;

        await Assert.That(recordsWritten).IsEqualTo(3);
    }
}
