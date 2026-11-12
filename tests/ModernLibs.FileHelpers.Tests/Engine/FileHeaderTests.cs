// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>Tests for GetFileHeader (FileHelpers 3.x compatible header line).</summary>
[Category("Engine")]
public sealed class FileHeaderTests
{
    [Test]
    public async Task Delimited_Uses_Captions_And_The_Record_Delimiter_Without_Quoting()
        => await Assert.That(new FileHelperEngine<HeaderD1>().GetFileHeader()).IsEqualTo("Order, ID,Name,Amount");

    [Test]
    public async Task Delimiter_Of_The_First_Field_Is_Used_Everywhere()
        => await Assert.That(new FileHelperEngine<HeaderD2>().GetFileHeader()).IsEqualTo("A|B|Sea");

    [Test]
    public async Task Delimiter_Of_A_Later_Field_Is_Ignored()
        => await Assert.That(new FileHelperEngine<HeaderD3>().GetFileHeader()).IsEqualTo("A,B,C");

    [Test]
    public async Task Hidden_Fields_Are_Left_Out()
        => await Assert.That(new FileHelperEngine<HeaderP1>().GetFileHeader()).IsEqualTo("Id;N");

    [Test]
    public async Task FixedLength_Uses_A_Tab()
        => await Assert.That(new FileHelperEngine<HeaderF1>().GetFileHeader()).IsEqualTo("Id\tFull Name");

    [Test]
    public async Task Async_Engine_Gives_The_Same_Text()
        => await Assert.That(new FileHelperAsyncEngine<HeaderD1>().GetFileHeader()).IsEqualTo("Order, ID,Name,Amount");

    [Test]
    public async Task Inherited_Fields_Come_First()
        => await Assert.That(new FileHelperEngine<HeaderDerived>().GetFileHeader()).IsEqualTo("Base Id,BaseName,Own,Own 2");

    [Test]
    public async Task FieldOrder_Decides_Order_And_The_First_Field_Delimiter()
        => await Assert.That(new FileHelperEngine<HeaderOrdered>().GetFileHeader()).IsEqualTo("First|Second|3rd");

    [Test]
    public async Task Multi_Character_Delimiter_Is_Used_As_Is()
        => await Assert.That(new FileHelperEngine<HeaderMulti>().GetFileHeader()).IsEqualTo("A||B b||C");

    [Test]
    public async Task Empty_Caption_Is_Kept_Without_Falling_Back_To_The_Name()
        => await Assert.That(new FileHelperEngine<HeaderEmptyCaption>().GetFileHeader()).IsEqualTo(",B");

    [Test]
    public async Task Existing_Fixture()
        => await Assert.That(new FileHelperEngine<CaptionedRecord>().GetFileHeader()).IsEqualTo("Order ID,Customer Name,Amount");
}
