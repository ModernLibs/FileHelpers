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
    [DisplayName("ParseContext ref struct stores and returns LineNumber and SkipRecord values")]
    public async Task ParseContext_IsRefStruct_Compiles()
    {
        // Arrange: Create ParseContext and set properties
        ParseContext ctx = new();
        ctx.LineNumber = 5;
        ctx.SkipRecord = true;
        int lineNumber = ctx.LineNumber;

        // Act: Read SkipRecord property
        bool skipRecord = ctx.SkipRecord;

        // Assert: Properties stored and returned correctly
        await Assert.That(lineNumber).IsEqualTo(5);
        await Assert.That(skipRecord).IsTrue();
    }

    [Test]
    [DisplayName("ParseContext default constructor yields LineNumber=0 and SkipRecord=false")]
    public async Task ParseContext_DefaultValues_AreZeroAndFalse()
    {
        // Arrange: Create default ParseContext
        ParseContext ctx = default;
        int lineNumber = ctx.LineNumber;

        // Act: Read SkipRecord from default context
        bool skipRecord = ctx.SkipRecord;

        // Assert: Default values are zero and false
        await Assert.That(lineNumber).IsEqualTo(0);
        await Assert.That(skipRecord).IsFalse();
    }

    [Test]
    [DisplayName("FormatContext ref struct stores and returns RecordsWritten value")]
    public async Task FormatContext_IsRefStruct_Compiles()
    {
        // Arrange: Create FormatContext and set RecordsWritten
        FormatContext ctx = new();
        ctx.RecordsWritten = 3;

        // Act: Read RecordsWritten property
        int recordsWritten = ctx.RecordsWritten;

        // Assert: Property stored and returned correctly
        await Assert.That(recordsWritten).IsEqualTo(3);
    }
}
