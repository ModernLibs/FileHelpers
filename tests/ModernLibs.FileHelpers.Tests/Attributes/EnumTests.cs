// CA1707: Test methods intentionally use underscores for the Method_Scenario pattern.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

/// <summary>Verifies enum member names and integer values match the FileHelpers 3.x API contract.</summary>
public sealed class EnumTests
{
    [Test]
    [DisplayName("FixedMode enum has expected values: ExactLength=0, AllowMoreChars=1, AllowLessChars=2, AllowVariableLength=3")]
    public async Task FixedMode_Values()
    {
        // Arrange: Get enum values as integers
        int exactLength = (int)FixedMode.ExactLength;
        int allowMoreChars = (int)FixedMode.AllowMoreChars;
        int allowLessChars = (int)FixedMode.AllowLessChars;

        // Act: Get remaining enum value
        int allowVariableLength = (int)FixedMode.AllowVariableLength;

        // Assert: All values match expected
        await Assert.That(exactLength).IsEqualTo(0);
        await Assert.That(allowMoreChars).IsEqualTo(1);
        await Assert.That(allowLessChars).IsEqualTo(2);
        await Assert.That(allowVariableLength).IsEqualTo(3);
    }

    [Test]
    [DisplayName("TrimMode enum has expected values: None=0, Both=1, Left=2, Right=3")]
    public async Task TrimMode_Values()
    {
        // Arrange: Get enum values as integers
        int none = (int)TrimMode.None;
        int both = (int)TrimMode.Both;
        int left = (int)TrimMode.Left;

        // Act: Get remaining enum value
        int right = (int)TrimMode.Right;

        // Assert: All values match expected
        await Assert.That(none).IsEqualTo(0);
        await Assert.That(both).IsEqualTo(1);
        await Assert.That(left).IsEqualTo(2);
        await Assert.That(right).IsEqualTo(3);
    }

    [Test]
    [DisplayName("AlignMode enum has expected values: Left=0, Center=1, Right=2")]
    public async Task AlignMode_Values()
    {
        // Arrange: Get enum values as integers
        int left = (int)AlignMode.Left;
        int center = (int)AlignMode.Center;

        // Act: Get remaining enum value
        int right = (int)AlignMode.Right;

        // Assert: All values match expected
        await Assert.That(left).IsEqualTo(0);
        await Assert.That(center).IsEqualTo(1);
        await Assert.That(right).IsEqualTo(2);
    }

    [Test]
    [DisplayName("QuoteMode enum has expected values: AlwaysQuoted=0, OptionalForRead=1, OptionalForWrite=2, OptionalForBoth=3")]
    public async Task QuoteMode_Values()
    {
        // Arrange: Get enum values as integers
        int alwaysQuoted = (int)QuoteMode.AlwaysQuoted;
        int optionalForRead = (int)QuoteMode.OptionalForRead;
        int optionalForWrite = (int)QuoteMode.OptionalForWrite;

        // Act: Get remaining enum value
        int optionalForBoth = (int)QuoteMode.OptionalForBoth;

        // Assert: All values match expected
        await Assert.That(alwaysQuoted).IsEqualTo(0);
        await Assert.That(optionalForRead).IsEqualTo(1);
        await Assert.That(optionalForWrite).IsEqualTo(2);
        await Assert.That(optionalForBoth).IsEqualTo(3);
    }

    [Test]
    [DisplayName("ConverterKind enum has expected value None=0 and defines common converters")]
    public async Task ConverterKind_Values()
    {
        // Arrange & Act: Get None value
        int none = (int)ConverterKind.None;

        // Assert: None equals 0
        await Assert.That(none).IsEqualTo(0);

        // Spot-check key members exist via Enum.IsDefined
        bool hasDate = Enum.IsDefined(typeof(ConverterKind), "Date");
        bool hasInt32 = Enum.IsDefined(typeof(ConverterKind), "Int32");
        bool hasDecimal = Enum.IsDefined(typeof(ConverterKind), "Decimal");
        bool hasBoolean = Enum.IsDefined(typeof(ConverterKind), "Boolean");
        bool hasGuid = Enum.IsDefined(typeof(ConverterKind), "Guid");

        await Assert.That(hasDate).IsTrue();
        await Assert.That(hasInt32).IsTrue();
        await Assert.That(hasDecimal).IsTrue();
        await Assert.That(hasBoolean).IsTrue();
        await Assert.That(hasGuid).IsTrue();
    }

    [Test]
    [DisplayName("RecordCondition enum has expected values: None=0, IncludeIfContains=1, ExcludeIfRegex=10")]
    public async Task RecordCondition_Values()
    {
        // Arrange: Get enum values as integers
        int none = (int)RecordCondition.None;
        int includeIfContains = (int)RecordCondition.IncludeIfContains;

        // Act: Get remaining enum value
        int excludeIfRegex = (int)RecordCondition.ExcludeIfRegex;

        // Assert: All values match expected
        await Assert.That(none).IsEqualTo(0);
        await Assert.That(includeIfContains).IsEqualTo(1);
        await Assert.That(excludeIfRegex).IsEqualTo(10);
    }

    [Test]
    [DisplayName("MultilineMode enum has expected values: AllowForBoth=0, AllowForRead=1, AllowForWrite=2, NotAllow=3")]
    public async Task MultilineMode_Values()
    {
        // Arrange: Get enum values as integers
        int allowForBoth = (int)MultilineMode.AllowForBoth;
        int allowForRead = (int)MultilineMode.AllowForRead;
        int allowForWrite = (int)MultilineMode.AllowForWrite;

        // Act: Get remaining enum value
        int notAllow = (int)MultilineMode.NotAllow;

        // Assert: All values match expected
        await Assert.That(allowForBoth).IsEqualTo(0);
        await Assert.That(allowForRead).IsEqualTo(1);
        await Assert.That(allowForWrite).IsEqualTo(2);
        await Assert.That(notAllow).IsEqualTo(3);
    }

    [Test]
    [DisplayName("OverflowMode enum has expected values: DiscardEnd=0, Error=1")]
    public async Task OverflowMode_Values()
    {
        // Arrange: Get enum values as integers
        int discardEnd = (int)OverflowMode.DiscardEnd;

        // Act: Get remaining enum value
        int error = (int)OverflowMode.Error;

        // Assert: All values match expected
        await Assert.That(discardEnd).IsEqualTo(0);
        await Assert.That(error).IsEqualTo(1);
    }
}
