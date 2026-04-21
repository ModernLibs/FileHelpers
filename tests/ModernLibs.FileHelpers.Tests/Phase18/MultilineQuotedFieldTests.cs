// Phase 18: Multiline Quoted Fields - Comprehensive Test Suite
// Tests RFC 4180 multiline quoted field support (MULT-01 through MULT-07)

// CA1707: Test method names use underscores intentionally for readability.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase18;

public class MultilineQuotedFieldTests
{
    #region MULT-01: MultilineMode Attribute Tests

    [Test]
    public void DelimitedRecord_WithMultilineModeAllowForRead_SetsModeCorrectly()
    {
        // Arrange: This test will verify the attribute accepts MultilineMode
        // Act & Assert: Attribute should be creatable with MultilineMode parameter
        var attr = new DelimitedRecordAttribute(",")
        {
            MultilineMode = MultilineMode.AllowForRead
        };

        Assert.Equal(MultilineMode.AllowForRead, attr.MultilineMode);
    }

    [Test]
    public void DelimitedRecord_DefaultMultilineMode_IsNotAllow()
    {
        // Arrange & Act
        var attr = new DelimitedRecordAttribute(",");

        // Assert: Default should be NotAllow for backward compatibility
        Assert.Equal(MultilineMode.NotAllow, attr.MultilineMode);
    }

    [Test]
    public void RecordOptions_CapturesMultilineModeFromAttribute()
    {
        // This test verifies that the source generator correctly emits
        // MultilineMode into the RecordOptions struct

        // Arrange: Get the descriptor for a multiline-enabled record
        var descriptor = FileHelperRegistry<MultilineCustomer>.Descriptor;

        // Act
        var options = descriptor.Options;

        // Assert: The MultilineMode should be captured in options
        Assert.Equal(MultilineMode.AllowForRead, options.MultilineMode);
    }

    #endregion

    #region MULT-02: Basic Multiline Parsing (AllowForRead)

    [Test]
    public void ReadString_SingleMultilineField_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "John,\"Line1\nLine2\",NYC";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("John", results[0].Name);
        Assert.Equal("Line1\nLine2", results[0].Address);  // Newline preserved
        Assert.Equal("NYC", results[0].City);
    }

    [Test]
    public void ReadString_MultilineFieldWithCRLF_PreservesCRLF()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "Jane,\"Line1\r\nLine2\",LA";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("Jane", results[0].Name);
        Assert.Equal("Line1\r\nLine2", results[0].Address);  // CRLF preserved
        Assert.Equal("LA", results[0].City);
    }

    [Test]
    public void ReadString_ThreeLineQuotedField_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<ThreeLineField>();
        string csv = "1,\"Line1\nLine2\nLine3\",CategoryA";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("1", results[0].Id);
        Assert.Equal("Line1\nLine2\nLine3", results[0].Description);
        Assert.Equal("CategoryA", results[0].Category);
    }

    [Test]
    public void ReadString_MultipleMultilineRecords_ParsesAll()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "John,\"Addr1\nAddr2\",NYC\nJane,\"Addr3\nAddr4\",LA";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Equal(2, results.Length);
        Assert.Equal("John", results[0].Name);
        Assert.Equal("Addr1\nAddr2", results[0].Address);
        Assert.Equal("Jane", results[1].Name);
        Assert.Equal("Addr3\nAddr4", results[1].Address);
    }

    #endregion

    #region MULT-03: Quote Balance Edge Cases

    [Test]
    public void ReadString_EscapedQuotesInsideMultiline_WorksCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<EscapedQuotesMultiline>();
        // "" inside quotes represents a single " in the output
        string csv = "Title,\"She said \"\"hello\"\"\nthen left\",Author";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("She said \"hello\"\nthen left", results[0].Content);
    }

    [Test]
    public void ReadString_MultipleQuotedFieldsOneMultiline_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterMultiline>();
        // Field2 is multiline, Field1 and Field3 are single-line quoted
        string csv = "\"A\",\"B1\nB2\",\"C\"";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("A", results[0].Field1);
        Assert.Equal("B1\nB2", results[0].Field2);
        Assert.Equal("C", results[0].Field3);
    }

    [Test]
    public void ReadString_MultilineFieldAtStart_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "\"John\nDoe\",Address,City";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("John\nDoe", results[0].Name);
        Assert.Equal("Address", results[0].Address);
        Assert.Equal("City", results[0].City);
    }

    [Test]
    public void ReadString_MultilineFieldAtEnd_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "Name,Address,\"City\nState\"";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("Name", results[0].Name);
        Assert.Equal("Address", results[0].Address);
        Assert.Equal("City\nState", results[0].City);
    }

    #endregion

    #region MULT-04: ErrorMode Integration (Unbalanced)

    [Test]
    public void ReadString_UnclosedQuoteThrowException_ThrowsWithCorrectLine()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        engine.ErrorMode = ErrorMode.ThrowException;
        // Unclosed quote - no closing " before EOF
        string csv = "John,\"Unclosed multiline,NYC";

        // Act & Assert
        var ex = Assert.Throws<FormatException>(() => engine.ReadString(csv));
        // Should mention line 1 (where the unclosed quote started)
        Assert.Contains("1", ex.Message);
    }

    [Test]
    public void ReadString_UnclosedQuoteSaveAndContinue_SavesErrorAndContinues()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        string csv = "Good,\"Closed\",NYC\nBad,\"Unclosed,LA\nAfter,\"Closed\",SF";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Equal(2, results.Length);  // Only valid records
        Assert.Equal("Good", results[0].Name);
        Assert.Equal("After", results[1].Name);
        Assert.Equal(1, engine.ErrorManager.ErrorCount);  // One error recorded
        Assert.Contains("Unclosed", engine.ErrorManager.Errors[0].Line);
    }

    [Test]
    public void ReadString_UnclosedQuoteIgnoreAndContinue_SkipsAndContinues()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;
        string csv = "Good,\"Closed\",NYC\nBad,\"Unclosed,LA\nAfter,\"Closed\",SF";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Equal(2, results.Length);  // Only valid records
        Assert.Equal("Good", results[0].Name);
        Assert.Equal("After", results[1].Name);
        Assert.Equal(0, engine.ErrorManager.ErrorCount);  // No errors recorded
    }

    #endregion

    #region MULT-05: Line Number Tracking

    [Test]
    public void ReadString_MultilineRecord_LineNumberReportsLastPhysicalLine()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        // 3 lines: Line 1 starts record, Line 3 ends it
        string csv = "John,\"Addr1\nAddr2\nAddr3\",NYC";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        // LineNumber should report line 3 (where the record ends)
        Assert.Equal(3, engine.LineNumber);
    }

    [Test]
    public void ReadString_ErrorInMultilineRecord_ReportsStartingLine()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        // Unclosed quote starting at line 1
        string csv = "Bad,\"Unclosed\nstill open\nstill going,NYC";

        // Act
        engine.ReadString(csv);

        // Assert
        Assert.Equal(1, engine.ErrorManager.ErrorCount);
        // Error should report starting line (1), not EOF line
        Assert.Equal(1, engine.ErrorManager.Errors[0].LineNumber);
    }

    #endregion

    #region MULT-06: Async Engine Tests

    [Test]
    public void ReadNext_StreamingMultiline_HandlesAcrossMultipleCalls()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<MultilineCustomer>();
        string csv = "John,\"Addr1\nAddr2\",NYC\nJane,\"Addr3\nAddr4\",LA";
        engine.BeginReadString(csv);

        // Act
        var record1 = engine.ReadNext();
        var record2 = engine.ReadNext();

        // Assert
        Assert.NotNull(record1);
        Assert.Equal("John", record1!.Name);
        Assert.Equal("Addr1\nAddr2", record1.Address);

        Assert.NotNull(record2);
        Assert.Equal("Jane", record2!.Name);
        Assert.Equal("Addr3\nAddr4", record2.Address);

        engine.Close();
    }

    [Test]
    public async Task ReadAsync_IAsyncEnumerable_HandlesMultilineFields()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<AsyncMultilineRecord>();
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write test file with multiline content
            File.WriteAllText(tempFile, "Header,\"Body Line1\nBody Line2\"\nSecond,\"Another\nMultiline\"");

            // Act
            var results = new List<AsyncMultilineRecord>();
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                results.Add(record);
            }

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("Header", results[0].Header);
            Assert.Equal("Body Line1\nBody Line2", results[0].Body);
            Assert.Equal("Second", results[1].Header);
            Assert.Equal("Another\nMultiline", results[1].Body);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ReadAsync_ErrorModeSaveAndContinue_CapturesMultilineErrors()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<AsyncMultilineRecord>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write test file with unclosed multiline
            File.WriteAllText(tempFile, "Good,\"Closed\"\nBad,\"Unclosed\nStillOpen\nGood2,\"Closed2\"");

            // Act
            var results = new List<AsyncMultilineRecord>();
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                results.Add(record);
            }

            // Assert
            Assert.Equal(2, results.Count);
            Assert.True(engine.ErrorManager.ErrorCount > 0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region MULT-07: Complex Scenarios

    [Test]
    public void ReadString_MixedSingleAndMultilineRecords_SameFile()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        // Mix of single-line and multiline records
        string csv = "Single1,Address1,City1\nMulti1,\"Addr2\nAddr3\",City2\nSingle2,Address4,City3";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Equal(3, results.Length);
        Assert.Equal("Single1", results[0].Name);
        Assert.Equal("Multi1", results[1].Name);
        Assert.Equal("Addr2\nAddr3", results[1].Address);
        Assert.Equal("Single2", results[2].Name);
    }

    [Test]
    public void ReadString_EmptyLinesInsideQuotedField_Preserved()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "John,\"Addr1\n\nAddr2\",NYC";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        Assert.Single(results);
        Assert.Equal("Addr1\n\nAddr2", results[0].Address);  // Empty line preserved
    }

    [Test]
    public void ReadString_DelimitersInsideMultilineQuotedField_ParsedCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterMultiline>();
        // Comma inside quoted multiline field
        string csv = "A,\"B1,B2\nB3,B4\",C";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Field1).IsEqualTo("A");
        await Assert.That(results[0].Field2).IsEqualTo("B1,B2\nB3,B4");  // Commas preserved inside quotes
        await Assert.That(results[0].Field3).IsEqualTo("C");
    }

    [Test]
    public void ReadString_CommentsOutsideMultiline_NotAffected()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "# Comment line\nJohn,\"Addr1\nAddr2\",NYC\n# Another comment";

        // Act
        var results = engine.ReadString(csv);

        // Assert - comment lines should be skipped
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("John");
    }

    #endregion

    #region QuoteBalanceScanner Tests

    [Test]
    public void HasUnclosedQuote_NoQuotes_ReturnsFalse()
    {
        // Arrange
        string line = "A,B,C";

        // Act
        bool hasUnclosed = DelimitedFieldEnumerator.HasUnclosedQuote(line.AsSpan());

        // Assert
        await Assert.That(hasUnclosed).IsFalse();
    }

    [Test]
    public void HasUnclosedQuote_BalancedQuotes_ReturnsFalse()
    {
        // Arrange
        string line = "\"A\",\"B\"";

        // Act
        bool hasUnclosed = DelimitedFieldEnumerator.HasUnclosedQuote(line.AsSpan());

        // Assert
        await Assert.That(hasUnclosed).IsFalse();
    }

    [Test]
    public void HasUnclosedQuote_UnclosedQuote_ReturnsTrue()
    {
        // Arrange
        string line = "\"A,B";

        // Act
        bool hasUnclosed = DelimitedFieldEnumerator.HasUnclosedQuote(line.AsSpan());

        // Assert
        await Assert.That(hasUnclosed).IsTrue();
    }

    [Test]
    public void HasUnclosedQuote_EscapedQuotes_ReturnsFalse()
    {
        // Arrange - "" inside quotes represents escaped quote
        string line = "\"A\"\"B\",\"C\"\"";

        // Act
        bool hasUnclosed = DelimitedFieldEnumerator.HasUnclosedQuote(line.AsSpan());

        // Assert
        await Assert.That(hasUnclosed).IsFalse();
    }

    #endregion
}
