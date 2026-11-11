namespace ModernLibs.FileHelpers.Tests.Compat;

/// <summary>
/// Inputs for each 4.9.1 compat record shape (the type name without <c>Twin</c>): valid rows, a short row, a long
/// row, a bad value, an unclosed quote, an empty line, an empty input, LF line ends without a final line end,
/// and all of them together.
/// </summary>
internal static class Compat49Inputs
{
    /// <summary>The rows of one shape. <see cref="Valid"/> may hold rows that some modes reject on purpose.</summary>
    private sealed record Shape(
        int FieldCount,
        string[] Valid,
        string Short,
        string Long,
        string[] Bad,
        string Unclosed,
        string? Header = null,
        string? Footer = null);

    private const string Plain25Header =
        "Id,Version,Authors,Description,Tags,DownloadCount,ProjectUrl,LicenseUrl,IconUrl,Language,Framework," +
        "MinClientVersion,Type,IsPrerelease,Published,LastUpdated,PackageSize,DependencyCount,HasReadme,HasLicense," +
        "HasIcon,IsSigned,RepositoryType,RepositoryUrl,RequireLicenseAcceptance";

    private static readonly Dictionary<string, Shape> Shapes = new(StringComparer.Ordinal)
    {
        ["Compat49Plain25"] = new(
            25,
            [
                "1,1.0.0,Alice,A package,tag1 tag2,100,https://p.example/1,https://l.example,https://i.example,en-US,net10.0,3.0,0,False,2026-11-08T10:20:30,2026-11-09T01:02:03,123456789012,2,True,False,True,False,git,https://r.example/1,False",
                "2,2.0.0-beta,Bob,,,0,,,,,,,1,true,2024-02-29T00:00:00,2024-02-29T23:59:59,0,0,false,true,false,true,,,true",
            ],
            Short: "3,1.0.0,Carol,desc,tags,5,u,l,i,en",
            Long: "4,1.0.0,Dan,d,t,5,u,l,i,en,fw,1.0,0,False,2026-01-01T00:00:00,2026-01-01T00:00:00,1,0,True,True,True,True,git,r,False,extra",
            Bad:
            [
                "5,1.0.0,Eve,d,t,x5,u,l,i,en,fw,1.0,0,False,2026-01-01T00:00:00,2026-01-01T00:00:00,1,0,True,True,True,True,git,r,False",
                "6,1.0.0,Fay,d,t,5,u,l,i,en,fw,1.0,0,False,2026-01-01 00:00,2026-01-01T00:00:00,1,0,True,True,True,True,git,r,False",
                "7,1.0.0,Gus,d,t,5,u,l,i,en,fw,1.0,0,maybe,2026-01-01T00:00:00,2026-01-01T00:00:00,99999999999999999999,0,True,True,True,True,git,r,False",
            ],
            // No field is quoted: the quote char is plain data here.
            Unclosed: "8,1.0.0,\"Hal,d,t,5,u,l,i,en,fw,1.0,0,False,2026-01-01T00:00:00,2026-01-01T00:00:00,1,0,True,True,True,True,git,r,False",
            Header: Plain25Header),

        ["Compat49Optional"] = new(
            4,
            ["1,a,note,5", "2,b,note2", "3,c", "4,d,,"],
            Short: "5",
            Long: "6,f,n,1,extra",
            Bad: ["7,g,n,x", "x8,h"],
            Unclosed: "9,\"i,n,1",
            Footer: "TOTAL,4"),

        ["Compat49PerFieldDelimiter"] = new(
            4,
            ["1|abc::2.50,tail", "2|x::0.1,", "3|::-7,t"],
            Short: "4|abc",
            Long: "5|a::1,t,extra",
            Bad: ["6|a::x,t", "7,a::1,t", "8|a:1,t"],
            Unclosed: "9|\"a::1,t"),

        ["Compat49NewLine"] = new(
            4,
            // 3.x writes the delimiter before the line break of a [FieldInNewLine] field; "2,plain" has none.
            ["1,\"Smith, J\",\r\nMain St,12345", "2,plain\r\nElm,1", "3,\"two\r\nlines\",\r\nOak,2", "4,\"last\"\r\nYew,6"],
            Short: "5,\"Short\",\r\nPine",
            Long: "6,\"Long\",extra,\r\nAsh,3",
            Bad: ["7,\"Bad\",\r\nBirch,x", "x8,\"Bad\",\r\nFir,4"],
            Unclosed: "9,\"open,\r\nCedar,5"),

        ["Compat49Trim"] = new(
            4,
            ["1,xx \"ab,c\" x,xxCODExx ,3", "2,plain,  y  ,4", "3, \"q\"\"uote\" ,x,5", "4,xx,xx,6"],
            Short: "5,\"n\"",
            Long: "6,\"n\",c,1,9",
            Bad: ["7,\"n\",c,z", "8,\"n\" junk,c,1"],
            Unclosed: "9,x\"open,c,1"),

        ["Compat49QuoteAlways"] = QuoteShape(),
        ["Compat49QuoteOptionalRead"] = QuoteShape(),
        ["Compat49QuoteOptionalWrite"] = QuoteShape(),
        ["Compat49QuoteOptionalBoth"] = QuoteShape(),
        ["Compat49MultilineNotAllow"] = QuoteShape(),

        ["Compat49Recyclable"] = new(
            4,
            ["1,a,1.5,true", "2,b,,false", "3,c,-0.25,True"],
            Short: "4,d",
            Long: "5,e,1,true,x",
            Bad: ["6,f,1,maybe", "7,g,1e,true"],
            Unclosed: "8,\"h,1,true"),

        ["Compat49Struct"] = new(
            4,
            ["1;a;11/08/2026;0.5", "2;b;02/29/2024;1e3", "   "],
            Short: "3;c",
            Long: "4;d;01/01/2026;1;x",
            Bad: ["5;e;13/01/2026;1", "6;f;01/01/2026;"],
            Unclosed: "7;\"g;01/01/2026;1"),

        ["Compat49Ctor"] = new(
            3,
            ["1,a,2026-11-08", "# comment", "  # indented comment", "2,b,2024-02-29"],
            Short: "3,c",
            Long: "4,d,2026-01-01,x",
            Bad: ["5,e,2026-02-30", "6,f,"],
            Unclosed: "7,\"g,2026-01-01"),

        ["Compat49FixedExact"] = FixedShape(),
        ["Compat49FixedMore"] = FixedShape(),
        ["Compat49FixedLess"] = FixedShape(),
        ["Compat49FixedVariable"] = FixedShape(),

        ["Compat49ConverterBase"] = new(
            4,
            ["1,alice,1F,p-abc", "2,bob,,p-", "3,carol, ff ,p-x", "4,,0,"],
            Short: "5,dan",
            Long: "6,e,1,p-a,x",
            Bad: ["7,!bang,1,p-a", "8,g,zz,p-a", "9,h,1,nopfx"],
            Unclosed: "10,\"i,1,p-a"),

        ["Compat49SpanConverter"] = new(
            3,
            ["1,1250,50", "2,99,", "3,-5,0"],
            Short: "4,100",
            Long: "5,1,2,3",
            Bad: ["6,abc,1", "7,!x,1", "8,,1"],
            Unclosed: "9,\"1,1"),
    };

    /// <summary>The kind of each input of <see cref="For"/>, in the same order (failure messages, golden files).</summary>
    public static readonly string[] Kinds =
        ["valid", "short-row", "long-row", "bad-values", "unclosed-quote", "empty-line", "empty-input", "lf-no-final-line-end", "all-together"];

    /// <summary>The number of fields of the record shape <paramref name="name"/>.</summary>
    public static int FieldCount(string name) => Shapes[name].FieldCount;

    /// <summary>The inputs for the record shape <paramref name="name"/>.</summary>
    public static IEnumerable<string> For(string name)
    {
        var s = Shapes[name];
        string first = s.Valid[0];
        yield return File(s, s.Valid);                                              // valid rows
        yield return File(s, [first, s.Short, .. s.Valid]);                         // a short row
        yield return File(s, [first, s.Long, .. s.Valid]);                          // a long row
        yield return File(s, [first, .. s.Bad, .. s.Valid]);                        // bad values
        yield return File(s, [first, s.Unclosed, .. s.Valid]);                      // an unclosed quote
        yield return File(s, [first, string.Empty, .. s.Valid]);                    // an empty line
        yield return string.Empty;                                                  // an empty input
        yield return File(s, s.Valid).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'); // LF, no final line end
        yield return File(s, [.. s.Valid, s.Short, s.Long, .. s.Bad, string.Empty, s.Unclosed, first]); // all together
    }

    private static string File(Shape shape, string[] rows)
    {
        var lines = new List<string>();
        if (shape.Header is not null) lines.Add(shape.Header);
        lines.AddRange(rows);
        if (shape.Footer is not null) lines.Add(shape.Footer);
        return string.Concat(lines.Select(l => l + "\r\n"));
    }

    /// <summary>One shape for the five quote and multiline records: <c>Id, [FieldQuoted] Name, Tail</c>.</summary>
    private static Shape QuoteShape() => new(
        3,
        [
            "1,\"Smith, John\",t",
            "2,\"say \"\"hi\"\"\",t",
            "3,plain,t",
            "4,\"multi\r\nline\",t",
            "5,\"\",t",
            "6, \"spaced\",t",
            "7,,t",
        ],
        Short: "8,\"n\"",
        Long: "9,\"n\",t,x",
        Bad: ["x10,\"n\",t", "11,\"closed\"junk,t"],
        Unclosed: "12,\"open,t");

    /// <summary>One shape for the four fixed-length records: Id 5 (right, '0'), Name 8 (trim right), Amount 6 (right).</summary>
    /// <remarks>No field is quoted: the "unclosed quote" row is a quote char in plain data (as for Plain25).</remarks>
    private static Shape FixedShape() => new(
        3,
        [Fixed("00001", "Alice", "12.50"), Fixed("00002", "Bob", "0.10"), Fixed("00003", "", "-7")],
        Short: Fixed("00004", "Carol", "1.00")[..15],
        Long: Fixed("00005", "Dan", "2.00") + "XYZ",
        Bad: [Fixed("0000x", "Bad", "1.00"), Fixed("00006", "Eve", "1,00"), "12"],
        Unclosed: Fixed("00007", "\"open", "1.00"));

    private static string Fixed(string id, string name, string amount) => id + name.PadRight(8) + amount.PadLeft(6);
}
