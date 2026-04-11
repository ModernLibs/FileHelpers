// CA1707: Test methods intentionally use underscores for the Method_Scenario pattern.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

/// <summary>Verifies enum member names and integer values match the FileHelpers 3.x API contract.</summary>
public sealed class EnumTests
{
    [Test]
    public async Task FixedMode_Values()
    {
        int exactLength = (int)FixedMode.ExactLength;
        int allowMoreChars = (int)FixedMode.AllowMoreChars;
        int allowLessChars = (int)FixedMode.AllowLessChars;
        int allowVariableLength = (int)FixedMode.AllowVariableLength;

        await Assert.That(exactLength).IsEqualTo(0);
        await Assert.That(allowMoreChars).IsEqualTo(1);
        await Assert.That(allowLessChars).IsEqualTo(2);
        await Assert.That(allowVariableLength).IsEqualTo(3);
    }

    [Test]
    public async Task TrimMode_Values()
    {
        int none = (int)TrimMode.None;
        int both = (int)TrimMode.Both;
        int left = (int)TrimMode.Left;
        int right = (int)TrimMode.Right;

        await Assert.That(none).IsEqualTo(0);
        await Assert.That(both).IsEqualTo(1);
        await Assert.That(left).IsEqualTo(2);
        await Assert.That(right).IsEqualTo(3);
    }

    [Test]
    public async Task AlignMode_Values()
    {
        int left = (int)AlignMode.Left;
        int center = (int)AlignMode.Center;
        int right = (int)AlignMode.Right;

        await Assert.That(left).IsEqualTo(0);
        await Assert.That(center).IsEqualTo(1);
        await Assert.That(right).IsEqualTo(2);
    }

    [Test]
    public async Task QuoteMode_Values()
    {
        int alwaysQuoted = (int)QuoteMode.AlwaysQuoted;
        int optionalForRead = (int)QuoteMode.OptionalForRead;
        int optionalForWrite = (int)QuoteMode.OptionalForWrite;
        int optionalForBoth = (int)QuoteMode.OptionalForBoth;

        await Assert.That(alwaysQuoted).IsEqualTo(0);
        await Assert.That(optionalForRead).IsEqualTo(1);
        await Assert.That(optionalForWrite).IsEqualTo(2);
        await Assert.That(optionalForBoth).IsEqualTo(3);
    }

    [Test]
    public async Task ConverterKind_Values()
    {
        int none = (int)ConverterKind.None;
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
    public async Task RecordCondition_Values()
    {
        int none = (int)RecordCondition.None;
        int includeIfContains = (int)RecordCondition.IncludeIfContains;
        int excludeIfMatchRegex = (int)RecordCondition.ExcludeIfMatchRegex;

        await Assert.That(none).IsEqualTo(0);
        await Assert.That(includeIfContains).IsEqualTo(1);
        await Assert.That(excludeIfMatchRegex).IsEqualTo(10);
    }

    [Test]
    public async Task MultilineMode_Values()
    {
        int allowForBoth = (int)MultilineMode.AllowForBoth;
        int allowForRead = (int)MultilineMode.AllowForRead;
        int allowForWrite = (int)MultilineMode.AllowForWrite;
        int notAllow = (int)MultilineMode.NotAllow;

        await Assert.That(allowForBoth).IsEqualTo(0);
        await Assert.That(allowForRead).IsEqualTo(1);
        await Assert.That(allowForWrite).IsEqualTo(2);
        await Assert.That(notAllow).IsEqualTo(3);
    }

    [Test]
    public async Task OverflowMode_Values()
    {
        int discardEnd = (int)OverflowMode.DiscardEnd;
        int error = (int)OverflowMode.Error;

        await Assert.That(discardEnd).IsEqualTo(0);
        await Assert.That(error).IsEqualTo(1);
    }
}
