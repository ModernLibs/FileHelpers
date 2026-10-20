// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>
/// Shape tests for the FileHelpers 3.x compatible exception hierarchy:
/// <see cref="FileHelpersException"/>, <see cref="BadUsageException"/>,
/// <see cref="NullValueNotFoundException"/> and <see cref="ConvertException"/>.
/// </summary>
[Category("Errors")]
public sealed class ExceptionTypesTests
{
    [Test]
    [DisplayName("BadUsageException, NullValueNotFoundException and ConvertException derive from FileHelpersException")]
    public async Task Hierarchy_MatchesFileHelpers3x()
    {
        // Arrange: one instance of each library exception type
        Exception badUsage = new BadUsageException("bad");
        Exception nullValue = new NullValueNotFoundException("missing");
        Exception convert = new ConvertException("abc", typeof(int));

        // Act: classify each instance against the base types
        bool badUsageIsBase = badUsage is FileHelpersException;
        bool nullValueIsBadUsage = nullValue is BadUsageException;
        bool convertIsBase = convert is FileHelpersException;

        // Assert: all of them are FileHelpersException subclasses with the 3.x relationships
        await Assert.That(badUsageIsBase).IsTrue();
        await Assert.That(nullValueIsBadUsage).IsTrue();
        await Assert.That(convertIsBase).IsTrue();
    }

    [Test]
    [DisplayName("FileHelpersException(line, column, message) formats the 3.x 'Line: N Column: C.' prefix")]
    public async Task FileHelpersException_LineColumnCtor_FormatsPrefix()
    {
        // Arrange + Act: construct with an explicit position
        var ex = new FileHelpersException(3, 7, "Something failed.");

        // Assert: message and position properties follow the 3.x format
        await Assert.That(ex.Message).IsEqualTo("Line: 3 Column: 7. Something failed.");
        await Assert.That(ex.LineNumber).IsEqualTo(3);
        await Assert.That(ex.ColumnNumber).IsEqualTo(7);
    }

    [Test]
    [DisplayName("FileHelpersException(message) has unknown position (-1) and an unprefixed message")]
    public async Task FileHelpersException_MessageCtor_UnknownPosition()
    {
        // Arrange + Act: construct without a position
        var inner = new InvalidOperationException("inner");
        var ex = new FileHelpersException("Outer failure.", inner);

        // Assert: position is unknown and the message is kept verbatim
        await Assert.That(ex.Message).IsEqualTo("Outer failure.");
        await Assert.That(ex.LineNumber).IsEqualTo(-1);
        await Assert.That(ex.ColumnNumber).IsEqualTo(-1);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }

    [Test]
    [DisplayName("ConvertException(value, type) exposes FieldStringValue/FieldType and the 3.x message")]
    public async Task ConvertException_ShortCtor_ExposesValueAndType()
    {
        // Arrange + Act: the constructor custom 3.x converters use
        var ex = new ConvertException("abc", typeof(int));

        // Assert: value, type and unknown position are exposed
        await Assert.That(ex.FieldStringValue).IsEqualTo("abc");
        await Assert.That(ex.FieldType).IsEqualTo(typeof(int));
        await Assert.That(ex.LineNumber).IsEqualTo(-1);
        await Assert.That(ex.ColumnNumber).IsEqualTo(-1);
        await Assert.That(ex.MessageOriginal).IsEqualTo("Error Converting 'abc' to type: 'Int32'. ");
        await Assert.That(ex.Message).IsEqualTo("Error Converting 'abc' to type: 'Int32'. ");
    }

    [Test]
    [DisplayName("ConvertException full ctor exposes field name, line, column, extra info and inner exception")]
    public async Task ConvertException_FullCtor_ExposesAllMembers()
    {
        // Arrange: an underlying failure to wrap
        var inner = new FormatException("bad digits");

        // Act: construct with every piece of context
        var ex = new ConvertException("x1", typeof(decimal), "Price", 12, 5, "Extra info.", inner);

        // Assert: all members round-trip and the message uses the 3.x layout
        await Assert.That(ex.FieldName).IsEqualTo("Price");
        await Assert.That(ex.LineNumber).IsEqualTo(12);
        await Assert.That(ex.ColumnNumber).IsEqualTo(5);
        await Assert.That(ex.MessageExtra).IsEqualTo("Extra info.");
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        await Assert.That(ex.Message)
            .IsEqualTo("Line: 12. Column: 5. Field: Price. Error Converting 'x1' to type: 'Decimal'. Extra info.");
    }
}
