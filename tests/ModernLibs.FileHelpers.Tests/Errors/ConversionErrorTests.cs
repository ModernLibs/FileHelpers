// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Helper type kept next to the tests that use it.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>
/// A failed conversion of a non-empty field must raise <see cref="ConvertException"/> and be routed
/// through <see cref="ErrorMode"/> on every read path — never silently become <c>default</c>.
/// </summary>
[Category("Errors")]
public sealed class ConversionErrorTests
{
    private const string DelimitedInput = "1,a,10\noops,x,1\n3,c,30";
    private const string FixedInput = "00001abc\nxx1  def\n00003ghi";

    // ---- ReadString (batch) ---------------------------------------------------------------

    [Test]
    [DisplayName("ReadString ThrowException: non-numeric int field throws ConvertException with field, value, type and position")]
    public async Task ReadString_Throw_RaisesConvertException()
    {
        // Arrange: engine in ThrowException mode
        var engine = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.ThrowException };

        // Act: read input whose second line has "oops" in the int field
        var ex = Capture.Exception(() => engine.ReadString(DelimitedInput));

        // Assert: ConvertException carries full 3.x context
        await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
        await Assert.That(ex!.Message).IsEqualTo("Line: 2. Column: 1. Field: Id. Error Converting 'oops' to type: 'Int32'. ");
    }

    [Test]
    [DisplayName("ReadString SaveAndContinue: bad record is skipped and one error with line/field info is recorded")]
    public async Task ReadString_Save_RecordsErrorAndSkips()
    {
        // Arrange: engine in SaveAndContinue mode
        var engine = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: read input with one bad line
        var records = engine.ReadString(DelimitedInput);

        // Assert: two good records, one error for line 2
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[1].Id).IsEqualTo(3);
        await AssertSingleSavedError(engine.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
    }

    [Test]
    [DisplayName("ReadString IgnoreAndContinue: bad record is skipped without recording an error")]
    public async Task ReadString_Ignore_SkipsSilently()
    {
        // Arrange: engine in IgnoreAndContinue mode
        var engine = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.IgnoreAndContinue };

        // Act: read input with one bad line
        var records = engine.ReadString(DelimitedInput);

        // Assert: bad record skipped, nothing recorded
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("ReadString: failure in a later field reports that field's name and column")]
    public async Task ReadString_Throw_LaterField_ReportsColumn()
    {
        // Arrange: the third field (Qty, starting at column 5) is invalid
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("1,x,zz"));

        // Assert
        await AssertConvertError(ex, line: 1, column: 5, field: "Qty", value: "zz", type: typeof(int));
    }

    // ---- ReadFile / ReadStream (batch) ---------------------------------------------------

    [Test]
    [DisplayName("ReadFile ThrowException and SaveAndContinue surface the conversion error with the right line")]
    public async Task ReadFile_ThrowAndSave()
    {
        // Arrange: input written to a temporary file
        string path = WriteTemp(DelimitedInput);
        try
        {
            var throwing = new FileHelperEngine<ConvClassRecord>();
            var saving = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };

            // Act
            var ex = Capture.Exception(() => throwing.ReadFile(path));
            var records = saving.ReadFile(path);

            // Assert
            await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
            await Assert.That(records.Length).IsEqualTo(2);
            await AssertSingleSavedError(saving.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("ReadStream ThrowException surfaces ConvertException")]
    public async Task ReadStream_Throw()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadStream(new StringReader(DelimitedInput)));

        // Assert
        await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
    }

    // ---- ReadSync (streaming) -------------------------------------------------------------

    [Test]
    [DisplayName("ReadSync ThrowException surfaces ConvertException with line number")]
    public async Task ReadSync_Throw()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var ex = Capture.Exception(() => _ = engine.ReadSync(DelimitedInput).ToList());

        // Assert
        await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
    }

    [Test]
    [DisplayName("ReadSync SaveAndContinue (class): bad record skipped, one error recorded")]
    public async Task ReadSync_Save_Class()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var records = engine.ReadSync(DelimitedInput).ToList();

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
        await AssertSingleSavedError(engine.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
    }

    [Test]
    [DisplayName("ReadSync SaveAndContinue (struct): bad record skipped instead of yielding default")]
    public async Task ReadSync_Save_Struct_DoesNotYieldDefault()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvStructRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var records = engine.ReadSync(DelimitedInput).ToList();

        // Assert: only the two valid records, no default(T) placeholder
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[1].Id).IsEqualTo(3);
        await Assert.That(engine.TotalRecords).IsEqualTo(2);
        await AssertSingleSavedError(engine.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
    }

    // ---- ReadAsync (FileHelperEngine streaming) ------------------------------------------

    [Test]
    [DisplayName("FileHelperEngine.ReadAsync ThrowException surfaces ConvertException with line number")]
    public async Task EngineReadAsync_Throw()
    {
        // Arrange
        string path = WriteTemp(DelimitedInput);
        try
        {
            var engine = new FileHelperEngine<ConvClassRecord>();

            // Act
            var ex = await Capture.ExceptionAsync(async () =>
            {
                await foreach (var _ in engine.ReadAsync(path)) { }
            });

            // Assert
            await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("FileHelperEngine.ReadAsync SaveAndContinue: keeps reading after the bad record (class and struct)")]
    public async Task EngineReadAsync_Save_ContinuesAfterBadRecord()
    {
        // Arrange
        string path = WriteTemp(DelimitedInput);
        try
        {
            var classEngine = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };
            var structEngine = new FileHelperEngine<ConvStructRecord> { ErrorMode = ErrorMode.SaveAndContinue };

            // Act
            var classRecords = new List<ConvClassRecord>();
            await foreach (var r in classEngine.ReadAsync(path)) classRecords.Add(r);
            var structRecords = new List<ConvStructRecord>();
            await foreach (var r in structEngine.ReadAsync(path)) structRecords.Add(r);

            // Assert: both continue past line 2 and do not yield placeholders
            await Assert.That(classRecords.Count).IsEqualTo(2);
            await Assert.That(classRecords[1].Id).IsEqualTo(3);
            await Assert.That(structRecords.Count).IsEqualTo(2);
            await Assert.That(structRecords[1].Id).IsEqualTo(3);
            await AssertSingleSavedError(classEngine.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
            await AssertSingleSavedError(structEngine.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- FileHelperAsyncEngine ------------------------------------------------------------

    [Test]
    [DisplayName("FileHelperAsyncEngine.ReadNext ThrowException surfaces ConvertException with line number")]
    public async Task AsyncEngineReadNext_Throw()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<ConvClassRecord>();
        engine.BeginReadString(DelimitedInput);

        // Act
        var ex = Capture.Exception(() => engine.ReadRemaining());

        // Assert
        await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
    }

    [Test]
    [DisplayName("FileHelperAsyncEngine.ReadNext SaveAndContinue: bad record skipped, one error recorded")]
    public async Task AsyncEngineReadNext_Save()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        engine.BeginReadString(DelimitedInput);

        // Act
        var records = engine.ReadRemaining();

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await AssertSingleSavedError(engine.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
    }

    [Test]
    [DisplayName("FileHelperAsyncEngine.ReadAsync ThrowException and SaveAndContinue surface the conversion error")]
    public async Task AsyncEngineReadAsync_ThrowAndSave()
    {
        // Arrange
        string path = WriteTemp(DelimitedInput);
        try
        {
            using var throwing = new FileHelperAsyncEngine<ConvClassRecord>();
            using var saving = new FileHelperAsyncEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };

            // Act
            var ex = await Capture.ExceptionAsync(async () =>
            {
                await foreach (var _ in throwing.ReadAsync(path)) { }
            });
            var records = new List<ConvClassRecord>();
            await foreach (var r in saving.ReadAsync(path)) records.Add(r);

            // Assert
            await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "oops", type: typeof(int));
            await Assert.That(records.Count).IsEqualTo(2);
            await AssertSingleSavedError(saving.ErrorManager, line: 2, field: "Id", value: "oops", column: 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- Fixed-length ---------------------------------------------------------------------

    [Test]
    [DisplayName("Fixed-length ReadString ThrowException: non-numeric int field throws ConvertException")]
    public async Task Fixed_ReadString_Throw()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvFixedRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString(FixedInput));

        // Assert
        await AssertConvertError(ex, line: 2, column: 1, field: "Id", value: "xx1  ", type: typeof(int));
    }

    [Test]
    [DisplayName("Fixed-length ReadString/ReadSync SaveAndContinue: bad record skipped, one error recorded")]
    public async Task Fixed_Save()
    {
        // Arrange
        var batch = new FileHelperEngine<ConvFixedRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var streaming = new FileHelperEngine<ConvFixedRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var batchRecords = batch.ReadString(FixedInput);
        var streamRecords = streaming.ReadSync(FixedInput).ToList();

        // Assert
        await Assert.That(batchRecords.Length).IsEqualTo(2);
        await Assert.That(streamRecords.Count).IsEqualTo(2);
        await AssertSingleSavedError(batch.ErrorManager, line: 2, field: "Id", value: "xx1  ", column: 1);
        await AssertSingleSavedError(streaming.ErrorManager, line: 2, field: "Id", value: "xx1  ", column: 1);
    }

    // ---- Conversion kinds -----------------------------------------------------------------

    [Test]
    [DisplayName("Implicit decimal, DateTime and bool conversions raise ConvertException on bad input")]
    public async Task ImplicitConversions_Throw()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvMixedRecord>();

        // Act
        var badDecimal = Capture.Exception(() => engine.ReadString("abc;15012024;true"));
        var badDate = Capture.Exception(() => engine.ReadString("1.5;not-a-date;true"));
        var badBool = Capture.Exception(() => engine.ReadString("1.5;15012024;maybe"));

        // Assert
        await AssertConvertError(badDecimal, line: 1, column: 1, field: "Amount", value: "abc", type: typeof(decimal));
        await AssertConvertError(badDate, line: 1, column: 5, field: "When", value: "not-a-date", type: typeof(DateTime));
        await AssertConvertError(badBool, line: 1, column: 14, field: "Flag", value: "maybe", type: typeof(bool));
    }

    [Test]
    [DisplayName("Explicit built-in converters raise ConvertException on bad input")]
    public async Task BuiltInConverters_Throw()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvBuiltInConverterRecord>();

        // Act
        var badInt = Capture.Exception(() => engine.ReadString("x,2024-01-15"));
        var badDate = Capture.Exception(() => engine.ReadString("1,15/01/2024"));

        // Assert
        await AssertConvertError(badInt, line: 1, column: 1, field: "Value", value: "x", type: typeof(int));
        await AssertConvertError(badDate, line: 1, column: 3, field: "Day", value: "15/01/2024", type: typeof(DateTime));
    }

    [Test]
    [DisplayName("Custom converter exceptions are wrapped in ConvertException with field context (3.x message)")]
    public async Task CustomConverter_PlainException_IsWrapped()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvCustomConverterRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("n,boom,1"));

        // Assert
        await AssertConvertError(ex, line: 1, column: 3, field: "Plain", value: "boom", type: typeof(int));
        var convert = (ConvertException)ex!;
        await Assert.That(convert.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(convert.MessageExtra)
            .IsEqualTo("Your custom converter: ThrowingConverter throws an InvalidOperationException with the message: kaboom");
    }

    [Test]
    [DisplayName("ConvertException thrown by a custom converter gets the field name and line attached")]
    public async Task CustomConverter_ConvertException_GetsFieldContext()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvCustomConverterRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var records = engine.ReadString("ok,1,2\nn,1,abc");

        // Assert
        await Assert.That(records.Length).IsEqualTo(1);
        await AssertSingleSavedError(engine.ErrorManager, line: 2, field: "Strict", value: "abc", column: 5);
        var convert = (ConvertException)engine.ErrorManager.Errors[0].ExceptionInfo!;
        await Assert.That(convert.MessageExtra).IsEqualTo("Not a number.");
    }

    // ---- helpers ----------------------------------------------------------------------------

    private static async Task AssertConvertError(Exception? ex, int line, int column, string field, string value, Type type)
    {
        await Assert.That(ex).IsTypeOf<ConvertException>();
        var convert = (ConvertException)ex!;
        await Assert.That(convert.LineNumber).IsEqualTo(line);
        await Assert.That(convert.ColumnNumber).IsEqualTo(column);
        await Assert.That(convert.FieldName).IsEqualTo(field);
        await Assert.That(convert.FieldStringValue).IsEqualTo(value);
        await Assert.That(convert.FieldType).IsEqualTo(type);
    }

    private static async Task AssertSingleSavedError(ErrorManager errors, int line, string field, string value, int column)
    {
        await Assert.That(errors.ErrorCount).IsEqualTo(1);
        var error = errors.Errors[0];
        await Assert.That(error.LineNumber).IsEqualTo(line);
        await Assert.That(error.FieldName).IsEqualTo(field);
        await Assert.That(error.RawFieldValue).IsEqualTo(value);
        await Assert.That(error.ColumnIndex).IsEqualTo(column - 1);
        await Assert.That(error.ExceptionInfo).IsTypeOf<ConvertException>();
        await Assert.That(((ConvertException)error.ExceptionInfo!).LineNumber).IsEqualTo(line);
    }

    private static string WriteTemp(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fh-x1-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        return path;
    }
}

/// <summary>Captures the exception thrown by a delegate (or null) for follow-up assertions.</summary>
internal static class Capture
{
    internal static Exception? Exception(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    internal static async Task<Exception?> ExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
