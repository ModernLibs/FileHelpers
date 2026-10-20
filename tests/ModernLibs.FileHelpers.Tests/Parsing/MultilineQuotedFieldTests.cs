// Multiline Quoted Fields - Comprehensive Test Suite
// Tests RFC 4180 multiline quoted field support

// CA1707: Test method names use underscores intentionally for readability.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

public class MultilineQuotedFieldTests
{
    #region MultilineMode Attribute Tests

    [Test]
    public async Task DelimitedRecord_WithMultilineModeAllowForRead_SetsModeCorrectly()
    {
        // Arrange: This test will verify the attribute accepts MultilineMode
        // Act & Assert: Attribute should be creatable with MultilineMode parameter
        var attr = new DelimitedRecordAttribute(",")
        {
            MultilineMode = MultilineMode.AllowForRead
        };

        await Assert.That(attr.MultilineMode).IsEqualTo(MultilineMode.AllowForRead);
    }

    [Test]
    public async Task DelimitedRecord_DefaultMultilineMode_IsNotAllow()
    {
        // Arrange & Act
        var attr = new DelimitedRecordAttribute(",");

        // Assert: Default should be NotAllow for backward compatibility
        await Assert.That(attr.MultilineMode).IsEqualTo(MultilineMode.NotAllow);
    }

    [Test]
    public async Task RecordOptions_CapturesMultilineModeFromAttribute()
    {
        // This test verifies that the source generator correctly emits
        // MultilineMode into the RecordOptions struct

        // Arrange: Get the descriptor for a multiline-enabled record
        var descriptor = FileHelperRegistry<MultilineCustomer>.Descriptor;

        // Act
        var options = descriptor.Options;

        // Assert: The MultilineMode should be captured in options
        await Assert.That(options.MultilineMode).IsEqualTo(MultilineMode.AllowForRead);
    }

    #endregion

    #region Basic Multiline Parsing (AllowForRead)

    [Test]
    public async Task ReadString_SingleMultilineField_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "John,\"Line1\nLine2\",NYC";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("John");
        await Assert.That(results[0].Address).IsEqualTo("Line1\nLine2");  // Newline preserved
        await Assert.That(results[0].City).IsEqualTo("NYC");
    }

    [Test]
    public async Task ReadString_MultilineFieldWithCRLF_PreservesCRLF()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "Jane,\"Line1\r\nLine2\",LA";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("Jane");
        await Assert.That(results[0].Address).IsEqualTo("Line1\r\nLine2");  // CRLF preserved
        await Assert.That(results[0].City).IsEqualTo("LA");
    }

    [Test]
    public async Task ReadString_ThreeLineQuotedField_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<ThreeLineField>();
        string csv = "1,\"Line1\nLine2\nLine3\",CategoryA";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo("1");
        await Assert.That(results[0].Description).IsEqualTo("Line1\nLine2\nLine3");
        await Assert.That(results[0].Category).IsEqualTo("CategoryA");
    }

    [Test]
    public async Task ReadString_MultipleMultilineRecords_ParsesAll()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "John,\"Addr1\nAddr2\",NYC\nJane,\"Addr3\nAddr4\",LA";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);
        await Assert.That(results[0].Name).IsEqualTo("John");
        await Assert.That(results[0].Address).IsEqualTo("Addr1\nAddr2");
        await Assert.That(results[1].Name).IsEqualTo("Jane");
        await Assert.That(results[1].Address).IsEqualTo("Addr3\nAddr4");
    }

    #endregion

    #region Quote Balance Edge Cases

    [Test]
    public async Task ReadString_EscapedQuotesInsideMultiline_WorksCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<EscapedQuotesMultiline>();
        // "" inside quotes represents a single " in the output
        string csv = "Title,\"She said \"\"hello\"\"\nthen left\",Author";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Content).IsEqualTo("She said \"hello\"\nthen left");
    }

    [Test]
    public async Task ReadString_MultipleQuotedFieldsOneMultiline_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterMultiline>();
        // Field2 is multiline, Field1 and Field3 are single-line quoted
        string csv = "\"A\",\"B1\nB2\",\"C\"";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Field1).IsEqualTo("A");
        await Assert.That(results[0].Field2).IsEqualTo("B1\nB2");
        await Assert.That(results[0].Field3).IsEqualTo("C");
    }

    [Test]
    public async Task ReadString_MultilineFieldAtStart_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "\"John\nDoe\",Address,City";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("John\nDoe");
        await Assert.That(results[0].Address).IsEqualTo("Address");
        await Assert.That(results[0].City).IsEqualTo("City");
    }

    [Test]
    public async Task ReadString_MultilineFieldAtEnd_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "Name,Address,\"City\nState\"";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("Name");
        await Assert.That(results[0].Address).IsEqualTo("Address");
        await Assert.That(results[0].City).IsEqualTo("City\nState");
    }

    #endregion

    #region ErrorMode Integration (Unbalanced)

    [Test]
    public async Task ReadString_UnclosedQuoteThrowException_ThrowsWithCorrectLine()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        engine.ErrorMode = ErrorMode.ThrowException;
        // Unclosed quote - no closing " before EOF
        string csv = "John,\"Unclosed multiline,NYC";

        // Act & Assert
        FileHelpersException? ex = null;
        try
        {
            engine.ReadString(csv);
        }
        catch (FileHelpersException caught)
        {
            ex = caught;
        }

        // Should mention line 1 (where the unclosed quote started)
        await Assert.That(ex).IsNotNull();
        await Assert.That(ex!.Message).Contains("1");
    }

    [Test]
    public async Task ReadString_UnclosedQuoteSaveAndContinue_SavesErrorAndContinues()
    {
        // Arrange
        var engine = new FileHelperEngine<SingleLineCustomer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        string csv = "Good,\"Closed\",NYC\nBad,\"Unclosed,LA\nAfter,\"Closed\",SF";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);  // Only valid records
        await Assert.That(results[0].Name).IsEqualTo("Good");
        await Assert.That(results[1].Name).IsEqualTo("After");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);  // One error recorded
        await Assert.That(engine.ErrorManager.Errors[0].RecordString).Contains("Unclosed");
    }

    [Test]
    public async Task ReadString_UnclosedQuoteIgnoreAndContinue_SkipsAndContinues()
    {
        // Arrange
        var engine = new FileHelperEngine<SingleLineCustomer>();
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;
        string csv = "Good,\"Closed\",NYC\nBad,\"Unclosed,LA\nAfter,\"Closed\",SF";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);  // Only valid records
        await Assert.That(results[0].Name).IsEqualTo("Good");
        await Assert.That(results[1].Name).IsEqualTo("After");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);  // No errors recorded
    }

    #endregion

    #region Line Number Tracking

    [Test]
    public async Task ReadString_MultilineRecord_LineNumberReportsLastPhysicalLine()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        // 3 lines: Line 1 starts record, Line 3 ends it
        string csv = "John,\"Addr1\nAddr2\nAddr3\",NYC";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        // LineNumber should report line 3 (where the record ends)
        await Assert.That(engine.LineNumber).IsEqualTo(3);
    }

    [Test]
    public async Task ReadString_ErrorInMultilineRecord_ReportsStartingLine()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        // Unclosed quote starting at line 1
        string csv = "Bad,\"Unclosed\nstill open\nstill going,NYC";

        // Act
        engine.ReadString(csv);

        // Assert
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        // Error should report starting line (1), not EOF line
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(1);
    }

    #endregion

    #region Async Engine Tests

    [Test]
    public async Task ReadNext_StreamingMultiline_HandlesAcrossMultipleCalls()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<MultilineCustomer>();
        string csv = "John,\"Addr1\nAddr2\",NYC\nJane,\"Addr3\nAddr4\",LA";
        engine.BeginReadString(csv);

        // Act
        var record1 = engine.ReadNext();
        var record2 = engine.ReadNext();

        // Assert
        await Assert.That(record1).IsNotNull();
        await Assert.That(record1!.Name).IsEqualTo("John");
        await Assert.That(record1.Address).IsEqualTo("Addr1\nAddr2");

        await Assert.That(record2).IsNotNull();
        await Assert.That(record2!.Name).IsEqualTo("Jane");
        await Assert.That(record2.Address).IsEqualTo("Addr3\nAddr4");

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
            await Assert.That(results.Count).IsEqualTo(2);
            await Assert.That(results[0].Header).IsEqualTo("Header");
            await Assert.That(results[0].Body).IsEqualTo("Body Line1\nBody Line2");
            await Assert.That(results[1].Header).IsEqualTo("Second");
            await Assert.That(results[1].Body).IsEqualTo("Another\nMultiline");
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
        var engine = new FileHelperAsyncEngine<SingleLineCustomer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var tempFile = Path.GetTempFileName();
        try
        {
            // MultilineMode.NotAllow: each physical line is parsed separately (Bad line is invalid).
            File.WriteAllText(tempFile, "Good,\"Closed\",NYC\nBad,\"Unclosed,NYC\nGood2,\"Closed\",SF");

            // Act
            var results = new List<SingleLineCustomer>();
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                results.Add(record);
            }

            // Assert
            await Assert.That(results.Count).IsEqualTo(2);
            await Assert.That(engine.ErrorManager.ErrorCount > 0).IsTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public async Task ReadString_MixedSingleAndMultilineRecords_SameFile()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        // Mix of single-line and multiline records
        string csv = "Single1,Address1,City1\nMulti1,\"Addr2\nAddr3\",City2\nSingle2,Address4,City3";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(3);
        await Assert.That(results[0].Name).IsEqualTo("Single1");
        await Assert.That(results[1].Name).IsEqualTo("Multi1");
        await Assert.That(results[1].Address).IsEqualTo("Addr2\nAddr3");
        await Assert.That(results[2].Name).IsEqualTo("Single2");
    }

    [Test]
    public async Task ReadString_EmptyLinesInsideQuotedField_Preserved()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();
        string csv = "John,\"Addr1\n\nAddr2\",NYC";

        // Act
        var results = engine.ReadString(csv);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Address).IsEqualTo("Addr1\n\nAddr2");  // Empty line preserved
    }

    [Test]
    public async Task ReadString_DelimitersInsideMultilineQuotedField_ParsedCorrectly()
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
    public async Task ReadString_CommentsOutsideMultiline_NotAffected()
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
}
