using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Aot.Tests;

// Each check returns its own non-zero code on mismatch; 0 means all passed.
// The engine writes Environment.NewLine by default (NewLineForWrite), as FileHelpers 3.x does.
string nl = Environment.NewLine;
int passed = 0;

static int Fail(int code, string what, string? got)
{
    Console.Error.WriteLine($"AOT smoke FAILED check {code}: {what}; got: {got}");
    return code;
}

// 1. Delimited class round trip.
{
    var engine = new FileHelperEngine<Person>();
    string text = "1,Ann,10.5" + nl + "2,Bob,20" + nl;
    var records = engine.ReadString(text);
    if (records.Length != 2 || records[0].Name != "Ann" || records[1].Balance != 20m)
        return Fail(1, "delimited read", records.Length.ToString());
    string written = engine.WriteString(records);
    if (written != text) return Fail(1, "delimited write", written);
    passed++;
}

// 2. Quoted record with doubled quotes and a multiline value.
{
    var engine = new FileHelperEngine<Note>();
    string text = "1,\"say \"\"hi\"\"\"" + nl + "2,\"line1" + nl + "line2\"" + nl;
    var records = engine.ReadString(text);
    if (records.Length != 2 || records[0].Text != "say \"hi\"" || records[1].Text != "line1" + nl + "line2")
        return Fail(2, "quoted read", records.Length.ToString());
    string written = engine.WriteString(records);
    var again = new FileHelperEngine<Note>().ReadString(written);
    if (again.Length != 2 || again[0].Text != records[0].Text || again[1].Text != records[1].Text)
        return Fail(2, "quoted round trip", written);
    passed++;
}

// 3. Fixed-length struct round trip.
{
    var engine = new FileHelperEngine<Item>();
    string text = "AB123Widget    XYZ" + nl;
    var records = engine.ReadString(text);
    if (records.Length != 1 || records[0].Code != "AB123" || records[0].Name != "Widget    " || records[0].Suffix != "XYZ")
        return Fail(3, "fixed read", records.Length.ToString());
    string written = engine.WriteString(records);
    if (written != text) return Fail(3, "fixed write", written);
    passed++;
}

// 4. SaveAndContinue keeps the good records and records one error.
{
    var engine = new FileHelperEngine<Person> { ErrorMode = ErrorMode.SaveAndContinue };
    var records = engine.ReadString("1,Ann,10" + nl + "x,Bad,oops" + nl + "3,Cy,30" + nl);
    if (engine.ErrorManager.ErrorCount != 1 || records.Length != 2)
        return Fail(4, "error mode", engine.ErrorManager.ErrorCount + "/" + records.Length);
    passed++;
}

string path = Path.Combine(Path.GetTempPath(), "fh-aot-" + Guid.NewGuid().ToString("N") + ".csv");
try
{
    File.WriteAllText(path, "1,Ann,10" + nl + "2,Bob,20" + nl + "3,Cy,30" + nl);

    // 5. await foreach over ReadAsync(path).
    {
        int count = 0;
        await foreach (var p in new FileHelperEngine<Person>().ReadAsync(path))
        {
            count++;
            if (p.Id != count) return Fail(5, "async order", p.Id.ToString());
        }
        if (count != 3) return Fail(5, "async count", count.ToString());
        passed++;
    }

    // 6. ReadFileAsync with a callback.
    {
        int seen = 0;
        int count = await new FileHelperEngine<Person>().ReadFileAsync(path, _ => seen++);
        if (count != 3 || seen != 3) return Fail(6, "callback count", count + "/" + seen);
        passed++;
    }
}
finally
{
    File.Delete(path);
}

// 7. Runtime matches the target framework.
{
    int expected =
#if NET11_0_OR_GREATER
        11;
#else
        10;
#endif
    if (Environment.Version.Major != expected) return Fail(7, "runtime version", Environment.Version.ToString());
    passed++;
}

// 8. WriteStreamAsync(Stream) gives the bytes of WriteString under the engine's Encoding, preamble included.
{
    string text = "1,Ann,10.5" + nl + "2,Bob,20" + nl;
    var engine = new FileHelperEngine<Person> { Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true) };
    var records = engine.ReadString(text);
    using var ms = new MemoryStream();
    await engine.WriteStreamAsync(ms, records);
    byte[] expected = [0xEF, 0xBB, 0xBF, .. engine.Encoding.GetBytes(text)];
    byte[] actual = ms.ToArray();
    if (!actual.AsSpan().SequenceEqual(expected) || text != engine.WriteString(records))
        return Fail(8, "WriteStreamAsync bytes", actual.Length + "/" + expected.Length);
    passed++;
}

// 9. Enum, formatted date, nullable, custom converter and double: read and write.
{
    var engine = new FileHelperEngine<Wide>();
    string text = "High,2026-11-11,7,aBc,0.25" + nl + "Low,2024-02-29,,X,1.5" + nl;
    var records = engine.ReadString(text);
    if (records.Length != 2
        || records[0].Level != Level.High || records[0].Day != new DateTime(2026, 11, 11) || records[0].Count != 7
        || records[0].Tag != "abc" || records[0].Ratio != 0.25
        || records[1].Level != Level.Low || records[1].Tag != "x" || records[1].Count is not null || records[1].Ratio != 1.5)
        return Fail(9, "wide read", records.Length.ToString());
    string written = engine.WriteString(records);
    string expectedText = "High,2026-11-11,7,ABC,0.25" + nl + "Low,2024-02-29,,X,1.5" + nl;
    if (written != expectedText) return Fail(9, "wide write", written);
    passed++;
}

Console.WriteLine($"AOT smoke: {passed} checks passed");
return 0;
