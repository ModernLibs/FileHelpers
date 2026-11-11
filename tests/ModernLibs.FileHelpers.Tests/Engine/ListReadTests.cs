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

[Category("Engine")]
public sealed class ListReadTests
{
    // FooteredRecord: [IgnoreLast(1)], so "total" is the footer.
    private const string Csv = "1,a\r\n2,b\r\n3,c\r\ntotal\r\n";

    private const string CsvWithBadRow = "1,a\r\nx,b\r\n3,c\r\ntotal\r\n";

    private static async Task AssertSameState(FileHelperEngine<FooteredRecord> list, FileHelperEngine<FooteredRecord> array)
    {
        await Assert.That(list.HeaderText).IsEqualTo(array.HeaderText);
        await Assert.That(list.FooterText).IsEqualTo(array.FooterText);
        await Assert.That(list.LineNumber).IsEqualTo(array.LineNumber);
        await Assert.That(list.TotalRecords).IsEqualTo(array.TotalRecords);
    }

    private static async Task AssertSameRecords(List<FooteredRecord> list, FooteredRecord[] array)
    {
        await Assert.That(list.Count).IsEqualTo(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            await Assert.That(list[i].Id).IsEqualTo(array[i].Id);
            await Assert.That(list[i].Name).IsEqualTo(array[i].Name);
        }
    }

    [Test]
    public async Task Each_List_Method_Equals_Its_Array_Twin()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Csv);

            var a = new FileHelperEngine<FooteredRecord>();
            var b = new FileHelperEngine<FooteredRecord>();
            await AssertSameRecords(b.ReadStringAsList(Csv), a.ReadString(Csv));
            await AssertSameState(b, a);

            a = new FileHelperEngine<FooteredRecord>();
            b = new FileHelperEngine<FooteredRecord>();
            using (var r1 = new StringReader(Csv))
            using (var r2 = new StringReader(Csv))
            {
                var array = a.ReadStream(r1);
                var list = b.ReadStreamAsList(r2);
                await AssertSameRecords(list, array);
                await AssertSameState(b, a);
                await Assert.That(r2.Peek()).IsEqualTo(r1.Peek());
            }

            a = new FileHelperEngine<FooteredRecord>();
            b = new FileHelperEngine<FooteredRecord>();
            await AssertSameRecords(b.ReadFileAsList(path), a.ReadFile(path));
            await AssertSameState(b, a);

            await Assert.That(b.FooterText).IsEqualTo("total");
            await Assert.That(b.TotalRecords).IsEqualTo(3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(99)]
    public async Task MaxRecords_Behaves_As_In_The_Array_Twin(int maxRecords)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Csv);

            var a = new FileHelperEngine<FooteredRecord>();
            var b = new FileHelperEngine<FooteredRecord>();
            await AssertSameRecords(b.ReadStringAsList(Csv, maxRecords), a.ReadString(Csv, maxRecords));
            await AssertSameState(b, a);

            a = new FileHelperEngine<FooteredRecord>();
            b = new FileHelperEngine<FooteredRecord>();
            using (var r1 = new StringReader(Csv))
            using (var r2 = new StringReader(Csv))
            {
                await AssertSameRecords(b.ReadStreamAsList(r2, maxRecords), a.ReadStream(r1, maxRecords));
                await AssertSameState(b, a);
            }

            a = new FileHelperEngine<FooteredRecord>();
            b = new FileHelperEngine<FooteredRecord>();
            await AssertSameRecords(b.ReadFileAsList(path, maxRecords), a.ReadFile(path, maxRecords));
            await AssertSameState(b, a);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Large_Input_Spans_Several_Chunks_And_Matches()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 1000; i++)
            sb.Append(i).Append(",n").Append(i).Append("\r\n");
        sb.Append("total\r\n");
        string text = sb.ToString();

        var a = new FileHelperEngine<FooteredRecord>();
        var b = new FileHelperEngine<FooteredRecord>();
        using var r1 = new StringReader(text);
        using var r2 = new StringReader(text);
        await AssertSameRecords(b.ReadStreamAsList(r2), a.ReadStream(r1));
        await AssertSameState(b, a);
        await Assert.That(b.TotalRecords).IsEqualTo(1000);
    }

    [Test]
    public async Task SaveAndContinue_Collects_The_Same_Errors()
    {
        var a = new FileHelperEngine<FooteredRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var b = new FileHelperEngine<FooteredRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        var array = a.ReadString(CsvWithBadRow);
        var list = b.ReadStringAsList(CsvWithBadRow);

        await Assert.That(array.Length).IsEqualTo(2);
        await AssertSameRecords(list, array);
        await Assert.That(b.ErrorManager.ErrorCount).IsEqualTo(a.ErrorManager.ErrorCount);
        await Assert.That(b.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(b.ErrorManager.Errors[0].LineNumber).IsEqualTo(a.ErrorManager.Errors[0].LineNumber);
        await Assert.That(b.ErrorManager.Errors[0].RecordString).IsEqualTo(a.ErrorManager.Errors[0].RecordString);
        await AssertSameState(b, a);
    }

    [Test]
    public async Task Interface_Defaults_Work_For_A_Foreign_Implementation()
    {
        IFileHelperEngine<CsvOrder> fake = new ArrayOnlyEngine();
        await Assert.That(fake.ReadStringAsList("x").Count).IsEqualTo(2);
        await Assert.That(fake.ReadStringAsList("x", 1).Count).IsEqualTo(1);
        await Assert.That(fake.ReadFileAsList("p").Count).IsEqualTo(2);
        await Assert.That(fake.ReadFileAsList("p", 1).Count).IsEqualTo(1);
        await Assert.That(fake.ReadStreamAsList(new StringReader("x")).Count).IsEqualTo(2);
        await Assert.That(fake.ReadStreamAsList(new StringReader("x"), 1).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Interface_Call_On_The_Engine_Reaches_The_Class_Method()
    {
        IFileHelperEngine<FooteredRecord> engine = new FileHelperEngine<FooteredRecord>();
        var list = engine.ReadStringAsList(Csv, 2);
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(engine.FooterText).IsEqualTo("total");
    }
}
