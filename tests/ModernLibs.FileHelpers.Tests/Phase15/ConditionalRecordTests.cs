// CA1707: Test methods intentionally use underscores for the Method_Scenario pattern.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Phase15.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase15;

/// <summary>
/// CREC-02..11: Tests for ConditionalRecord filtering across all 8 condition types.
/// Tests cover IncludeIfBegins, IncludeIfContains, IncludeIfEnds, IncludeIfRegex,
/// ExcludeIfBegins, ExcludeIfContains, ExcludeIfEnds, ExcludeIfRegex.
/// </summary>
public sealed class ConditionalRecordTests
{
    // =========================================================================
    // Include Condition Tests (CREC-02..05)
    // =========================================================================

    [Test]
    [DisplayName("CREC-02: IncludeIfBegins filters lines starting with pattern")]
    public async Task ReadEngine_IncludeIfBegins_FiltersCorrectly()
    {
        // Arrange: Input with HDR and DAT lines
        const string input = "HDR,1,Alpha\nDAT,2,Bravo\nHDR,3,Charlie";
        var engine = new FileHelperEngine<IncludeIfBeginsRecord>();

        // Act: Parse with IncludeIfBegins("HDR") filter
        var results = engine.ReadString(input);

        // Assert: Only HDR lines included (records 1 and 3)
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("Alpha");
        await Assert.That(results[1].Id).IsEqualTo(3);
        await Assert.That(results[1].Name).IsEqualTo("Charlie");
    }

    [Test]
    [DisplayName("CREC-03: IncludeIfContains filters lines containing substring")]
    public async Task ReadEngine_IncludeIfContains_FiltersCorrectly()
    {
        // Arrange: Input with lines containing "Error"
        const string input = "1,ErrorAlpha,OK\n2,Bravo,OK\n3,ErrorCharlie,FAIL";
        var engine = new FileHelperEngine<IncludeIfContainsRecord>();

        // Act: Parse with IncludeIfContains("Error") filter
        var results = engine.ReadString(input);

        // Assert: Only lines containing "Error" included
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("ErrorAlpha");
        await Assert.That(results[1].Id).IsEqualTo(3);
        await Assert.That(results[1].Name).IsEqualTo("ErrorCharlie");
    }

    [Test]
    [DisplayName("CREC-04: IncludeIfEnds filters lines ending with suffix")]
    public async Task ReadEngine_IncludeIfEnds_FiltersCorrectly()
    {
        // Arrange: Input with various file extensions
        const string input = "1,file.txt\n2,report.pdf\n3,doc.txt";
        var engine = new FileHelperEngine<IncludeIfEndsRecord>();

        // Act: Parse with IncludeIfEnds(".txt") filter
        var results = engine.ReadString(input);

        // Assert: Only lines ending with ".txt" included
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[0].Filename).IsEqualTo("file.txt");
        await Assert.That(results[1].Id).IsEqualTo(3);
        await Assert.That(results[1].Filename).IsEqualTo("doc.txt");
    }

    [Test]
    [DisplayName("CREC-05: IncludeIfRegex filters lines matching pattern")]
    public async Task ReadEngine_IncludeIfRegex_FiltersCorrectly()
    {
        // Arrange: Input with YYYY- prefix and non-matching lines
        const string input = "2024-01,EventA,Extra\nAB-123,EventB,Extra\n2025-12,EventC,Extra";
        var engine = new FileHelperEngine<IncludeIfRegexRecord>();

        // Act: Parse with IncludeIfRegex("^\\d{4}-") filter
        var results = engine.ReadString(input);

        // Assert: Only lines matching year prefix pattern included
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].YearPrefix).IsEqualTo("2024-01");
        await Assert.That(results[0].Name).IsEqualTo("EventA");
        await Assert.That(results[1].YearPrefix).IsEqualTo("2025-12");
        await Assert.That(results[1].Name).IsEqualTo("EventC");
    }

    // =========================================================================
    // Exclude Condition Tests (CREC-06..09)
    // =========================================================================

    [Test]
    [DisplayName("CREC-06: ExcludeIfBegins excludes lines starting with pattern")]
    public async Task ReadEngine_ExcludeIfBegins_FiltersCorrectly()
    {
        // Arrange: Input with comment lines starting with #
        const string input = "#Comment,1,Extra\nData,2,Extra\n#Another,3,Extra\nMore,4,Extra";
        var engine = new FileHelperEngine<ExcludeIfBeginsRecord>();

        // Act: Parse with ExcludeIfBegins("#") filter
        var results = engine.ReadString(input);

        // Assert: Only non-comment lines included
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Data).IsEqualTo("Data");
        await Assert.That(results[0].Id).IsEqualTo(2);
        await Assert.That(results[1].Data).IsEqualTo("More");
        await Assert.That(results[1].Id).IsEqualTo(4);
    }

    [Test]
    [DisplayName("CREC-07: ExcludeIfContains excludes lines containing substring")]
    public async Task ReadEngine_ExcludeIfContains_FiltersCorrectly()
    {
        // Arrange: Input with lines to skip (note: fixture uses "Skip" not "SKIP")
        const string input = "1,SkipThis,Extra\n2,KeepThis,Extra\n3,AlsoSkip,Extra";
        var engine = new FileHelperEngine<ExcludeIfContainsRecord>();

        // Act: Parse with ExcludeIfContains("Skip") filter (case-sensitive)
        var results = engine.ReadString(input);

        // Assert: Only lines not containing "Skip" included (case sensitive)
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(2);
        await Assert.That(results[0].Name).IsEqualTo("KeepThis");
    }

    [Test]
    [DisplayName("CREC-08: ExcludeIfEnds excludes lines ending with suffix")]
    public async Task ReadEngine_ExcludeIfEnds_FiltersCorrectly()
    {
        // Arrange: Input with temp file extensions
        const string input = "1,temp.tmp\n2,keep.txt\n3,other.tmp";
        var engine = new FileHelperEngine<ExcludeIfEndsRecord>();

        // Act: Parse with ExcludeIfEnds(".tmp") filter
        var results = engine.ReadString(input);

        // Assert: Only lines not ending with ".tmp" included
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(2);
        await Assert.That(results[0].Filename).IsEqualTo("keep.txt");
    }

    [Test]
    [DisplayName("CREC-09: ExcludeIfRegex excludes lines matching pattern")]
    public async Task ReadEngine_ExcludeIfRegex_FiltersCorrectly()
    {
        // Arrange: Input with X- prefix lines
        const string input = "X-Header,1,Extra\nNormal,2,Extra\nX-Footer,3,Extra";
        var engine = new FileHelperEngine<ExcludeIfRegexRecord>();

        // Act: Parse with ExcludeIfRegex("^X-") filter
        var results = engine.ReadString(input);

        // Assert: Only lines not matching X- prefix included
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Data).IsEqualTo("Normal");
        await Assert.That(results[0].Id).IsEqualTo(2);
    }

    // =========================================================================
    // Pre-parse Filtering Tests (CREC-10)
    // =========================================================================

    [Test]
    [DisplayName("CREC-10: Pre-parse filtering skips malformed lines before parse attempt")]
    public async Task ReadEngine_PreParseFiltering_SkipsBeforeParseAttempt()
    {
        // Arrange: Input with malformed line that would fail parse, but is filtered out first
        // "SKIP,X,Bad" would fail parse (X is not an int), but it's filtered out before parsing
        // Note: PreParseFilterRecord has 3 fields: Data (string), Id (int), Extra (string)
        const string input = "SKIP,X,Bad\nDATA,2,Good";
        var engine = new FileHelperEngine<PreParseFilterRecord>();

        // Act: Parse with ExcludeIfBegins("SKIP") filter
        var results = engine.ReadString(input);

        // Assert: Malformed line was filtered before parse, no error thrown
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(2);
    }

    // =========================================================================
    // Async Engine Parity Tests
    // =========================================================================

    [Test]
    [DisplayName("AsyncEngine IncludeIfBegins produces same results as sync engine")]
    public async Task ReadAsyncEngine_IncludeIfBegins_MatchesSyncEngine()
    {
        // Arrange: Same input as sync test
        const string input = "HDR,1,Alpha\nDAT,2,Bravo\nHDR,3,Charlie";
        var asyncEngine = new FileHelperAsyncEngine<IncludeIfBeginsRecord>();

        // Act: Parse with async engine
        asyncEngine.BeginReadString(input);
        var asyncResults = new List<IncludeIfBeginsRecord>();
        while (asyncEngine.ReadNext() is { } record)
        {
            asyncResults.Add(record);
        }
        asyncEngine.Close();

        // Assert: Same results as sync engine
        await Assert.That(asyncResults.Count).IsEqualTo(2);
        await Assert.That(asyncResults[0].Id).IsEqualTo(1);
        await Assert.That(asyncResults[1].Id).IsEqualTo(3);
    }

    [Test]
    [DisplayName("AsyncEngine ExcludeIfContains produces same results as sync engine")]
    public async Task ReadAsyncEngine_ExcludeIfContains_MatchesSyncEngine()
    {
        // Arrange (note: fixture uses "Skip" not "SKIP")
        const string input = "1,SkipThis,Extra\n2,KeepThis,Extra\n3,AlsoSkip,Extra";
        var asyncEngine = new FileHelperAsyncEngine<ExcludeIfContainsRecord>();

        // Act
        asyncEngine.BeginReadString(input);
        var asyncResults = new List<ExcludeIfContainsRecord>();
        while (asyncEngine.ReadNext() is { } record)
        {
            asyncResults.Add(record);
        }
        asyncEngine.Close();

        // Assert
        await Assert.That(asyncResults.Count).IsEqualTo(1);
        await Assert.That(asyncResults[0].Id).IsEqualTo(2);
    }

    [Test]
    [DisplayName("IAsyncEnumerable ReadAsync honors conditional filtering")]
    public async Task ReadAsync_IAsyncEnumerable_HonorsFiltering()
    {
        // Arrange: Create temp file
        const string input = "HDR,1,Alpha\nDAT,2,Bravo\nHDR,3,Charlie";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, input).ConfigureAwait(false);

        var engine = new FileHelperAsyncEngine<IncludeIfBeginsRecord>();

        // Act: Use IAsyncEnumerable
        var results = new List<IncludeIfBeginsRecord>();
        await foreach (var record in engine.ReadAsync(tempFile).ConfigureAwait(false))
        {
            results.Add(record);
        }

        // Cleanup
        File.Delete(tempFile);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[1].Id).IsEqualTo(3);
    }

    // =========================================================================
    // Edge Case Tests
    // =========================================================================

    [Test]
    [DisplayName("Empty file returns empty array")]
    public async Task ConditionalRecord_EmptyFile_ReturnsEmpty()
    {
        // Arrange
        const string input = "";
        var engine = new FileHelperEngine<IncludeIfBeginsRecord>();

        // Act
        var results = engine.ReadString(input);

        // Assert
        await Assert.That(results.Length).IsEqualTo(0);
    }

    [Test]
    [DisplayName("No matching lines returns empty array")]
    public async Task ConditionalRecord_NoMatches_ReturnsEmpty()
    {
        // Arrange: Input with no lines starting with HDR
        const string input = "DAT,1,Alpha\nDAT,2,Bravo\nDAT,3,Charlie";
        var engine = new FileHelperEngine<IncludeIfBeginsRecord>();

        // Act
        var results = engine.ReadString(input);

        // Assert
        await Assert.That(results.Length).IsEqualTo(0);
    }

    [Test]
    [DisplayName("All lines match returns all records")]
    public async Task ConditionalRecord_AllLinesMatch_ReturnsAll()
    {
        // Arrange: All lines start with HDR
        const string input = "HDR,1,Alpha\nHDR,2,Bravo\nHDR,3,Charlie";
        var engine = new FileHelperEngine<IncludeIfBeginsRecord>();

        // Act
        var results = engine.ReadString(input);

        // Assert
        await Assert.That(results.Length).IsEqualTo(3);
    }

    [Test]
    [DisplayName("Record without ConditionalRecord includes all lines")]
    public async Task NoConditionRecord_IncludesAllLines()
    {
        // Arrange - using NoConditionalRecord from Fixtures
        const string input = "1,Alpha,Extra\n2,Bravo,Extra\n3,Charlie,Extra";
        var engine = new FileHelperEngine<NoConditionalRecord>();

        // Act
        var results = engine.ReadString(input);

        // Assert
        await Assert.That(results.Length).IsEqualTo(3);
    }

    // =========================================================================
    // Error Mode Tests (CREC-11 related)
    // =========================================================================

    [Test]
    [DisplayName("IncludeIfEnds with pattern matching continues normally")]
    public async Task ConditionalRecord_IncludeIfEnds_MatchingContinues()
    {
        // Arrange: Test normal operation with IncludeIfEnds pattern
        // Note: Invalid regex patterns are caught at compile time by MLFH005
        // This test verifies normal IncludeIfEnds behavior
        const string input = "1,file.txt\n2,report.pdf\n3,doc.txt";
        var engine = new FileHelperEngine<IncludeIfEndsRecord>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act
        var results = engine.ReadString(input);

        // Assert: Normal operation - only .txt files included
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Filename).IsEqualTo("file.txt");
        await Assert.That(results[1].Filename).IsEqualTo("doc.txt");
    }

    // =========================================================================
    // Case Sensitivity Tests
    // =========================================================================

    [Test]
    [DisplayName("IncludeIfContains is case-sensitive")]
    public async Task IncludeIfContains_IsCaseSensitive()
    {
        // Arrange: Pattern is "Error" (capital E)
        const string input = "1,ErrorAlpha,OK\n2,errorBeta,OK\n3,ERRORGamma,FAIL";
        var engine = new FileHelperEngine<IncludeIfContainsRecord>();

        // Act
        var results = engine.ReadString(input);

        // Assert: Only exact case match included
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(1);
    }

    [Test]
    [DisplayName("ExcludeIfContains is case-sensitive")]
    public async Task ExcludeIfContains_IsCaseSensitive()
    {
        // Arrange: Pattern is "Skip" (from fixture)
        // "SkipThis" contains "Skip" → excluded
        // "SKIPTHIS" does NOT contain "Skip" (different case) → included
        // "KeepThis" does NOT contain "Skip" → included
        const string input = "1,SkipThis,Extra\n2,SKIPTHIS,Extra\n3,KeepThis,Extra";
        var engine = new FileHelperEngine<ExcludeIfContainsRecord>();

        // Act
        var results = engine.ReadString(input);

        // Assert: 2 records included (SKIPTHIS and KeepThis), SkipThis excluded
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(2);  // SKIPTHIS - different case, included
        await Assert.That(results[1].Id).IsEqualTo(3);  // KeepThis - included
    }
}
