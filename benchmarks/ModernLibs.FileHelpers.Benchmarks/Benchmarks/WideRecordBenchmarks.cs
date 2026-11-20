// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// 150 columns, no header: the even columns (0, 2, ...) are int, the odd columns are string (the layout of
/// <see cref="DatasetGenerator.EnsureWideRecords"/>). The properties were generated once (Int000, Str001, Int002, ...) and the file is maintained by hand.
/// </summary>
[DelimitedRecord(",")]
public sealed class WideRecord
{
    [FieldOrder(1)] public int Int000 { get; set; }
    [FieldOrder(2)] public string Str001 { get; set; } = "";
    [FieldOrder(3)] public int Int002 { get; set; }
    [FieldOrder(4)] public string Str003 { get; set; } = "";
    [FieldOrder(5)] public int Int004 { get; set; }
    [FieldOrder(6)] public string Str005 { get; set; } = "";
    [FieldOrder(7)] public int Int006 { get; set; }
    [FieldOrder(8)] public string Str007 { get; set; } = "";
    [FieldOrder(9)] public int Int008 { get; set; }
    [FieldOrder(10)] public string Str009 { get; set; } = "";
    [FieldOrder(11)] public int Int010 { get; set; }
    [FieldOrder(12)] public string Str011 { get; set; } = "";
    [FieldOrder(13)] public int Int012 { get; set; }
    [FieldOrder(14)] public string Str013 { get; set; } = "";
    [FieldOrder(15)] public int Int014 { get; set; }
    [FieldOrder(16)] public string Str015 { get; set; } = "";
    [FieldOrder(17)] public int Int016 { get; set; }
    [FieldOrder(18)] public string Str017 { get; set; } = "";
    [FieldOrder(19)] public int Int018 { get; set; }
    [FieldOrder(20)] public string Str019 { get; set; } = "";
    [FieldOrder(21)] public int Int020 { get; set; }
    [FieldOrder(22)] public string Str021 { get; set; } = "";
    [FieldOrder(23)] public int Int022 { get; set; }
    [FieldOrder(24)] public string Str023 { get; set; } = "";
    [FieldOrder(25)] public int Int024 { get; set; }
    [FieldOrder(26)] public string Str025 { get; set; } = "";
    [FieldOrder(27)] public int Int026 { get; set; }
    [FieldOrder(28)] public string Str027 { get; set; } = "";
    [FieldOrder(29)] public int Int028 { get; set; }
    [FieldOrder(30)] public string Str029 { get; set; } = "";
    [FieldOrder(31)] public int Int030 { get; set; }
    [FieldOrder(32)] public string Str031 { get; set; } = "";
    [FieldOrder(33)] public int Int032 { get; set; }
    [FieldOrder(34)] public string Str033 { get; set; } = "";
    [FieldOrder(35)] public int Int034 { get; set; }
    [FieldOrder(36)] public string Str035 { get; set; } = "";
    [FieldOrder(37)] public int Int036 { get; set; }
    [FieldOrder(38)] public string Str037 { get; set; } = "";
    [FieldOrder(39)] public int Int038 { get; set; }
    [FieldOrder(40)] public string Str039 { get; set; } = "";
    [FieldOrder(41)] public int Int040 { get; set; }
    [FieldOrder(42)] public string Str041 { get; set; } = "";
    [FieldOrder(43)] public int Int042 { get; set; }
    [FieldOrder(44)] public string Str043 { get; set; } = "";
    [FieldOrder(45)] public int Int044 { get; set; }
    [FieldOrder(46)] public string Str045 { get; set; } = "";
    [FieldOrder(47)] public int Int046 { get; set; }
    [FieldOrder(48)] public string Str047 { get; set; } = "";
    [FieldOrder(49)] public int Int048 { get; set; }
    [FieldOrder(50)] public string Str049 { get; set; } = "";
    [FieldOrder(51)] public int Int050 { get; set; }
    [FieldOrder(52)] public string Str051 { get; set; } = "";
    [FieldOrder(53)] public int Int052 { get; set; }
    [FieldOrder(54)] public string Str053 { get; set; } = "";
    [FieldOrder(55)] public int Int054 { get; set; }
    [FieldOrder(56)] public string Str055 { get; set; } = "";
    [FieldOrder(57)] public int Int056 { get; set; }
    [FieldOrder(58)] public string Str057 { get; set; } = "";
    [FieldOrder(59)] public int Int058 { get; set; }
    [FieldOrder(60)] public string Str059 { get; set; } = "";
    [FieldOrder(61)] public int Int060 { get; set; }
    [FieldOrder(62)] public string Str061 { get; set; } = "";
    [FieldOrder(63)] public int Int062 { get; set; }
    [FieldOrder(64)] public string Str063 { get; set; } = "";
    [FieldOrder(65)] public int Int064 { get; set; }
    [FieldOrder(66)] public string Str065 { get; set; } = "";
    [FieldOrder(67)] public int Int066 { get; set; }
    [FieldOrder(68)] public string Str067 { get; set; } = "";
    [FieldOrder(69)] public int Int068 { get; set; }
    [FieldOrder(70)] public string Str069 { get; set; } = "";
    [FieldOrder(71)] public int Int070 { get; set; }
    [FieldOrder(72)] public string Str071 { get; set; } = "";
    [FieldOrder(73)] public int Int072 { get; set; }
    [FieldOrder(74)] public string Str073 { get; set; } = "";
    [FieldOrder(75)] public int Int074 { get; set; }
    [FieldOrder(76)] public string Str075 { get; set; } = "";
    [FieldOrder(77)] public int Int076 { get; set; }
    [FieldOrder(78)] public string Str077 { get; set; } = "";
    [FieldOrder(79)] public int Int078 { get; set; }
    [FieldOrder(80)] public string Str079 { get; set; } = "";
    [FieldOrder(81)] public int Int080 { get; set; }
    [FieldOrder(82)] public string Str081 { get; set; } = "";
    [FieldOrder(83)] public int Int082 { get; set; }
    [FieldOrder(84)] public string Str083 { get; set; } = "";
    [FieldOrder(85)] public int Int084 { get; set; }
    [FieldOrder(86)] public string Str085 { get; set; } = "";
    [FieldOrder(87)] public int Int086 { get; set; }
    [FieldOrder(88)] public string Str087 { get; set; } = "";
    [FieldOrder(89)] public int Int088 { get; set; }
    [FieldOrder(90)] public string Str089 { get; set; } = "";
    [FieldOrder(91)] public int Int090 { get; set; }
    [FieldOrder(92)] public string Str091 { get; set; } = "";
    [FieldOrder(93)] public int Int092 { get; set; }
    [FieldOrder(94)] public string Str093 { get; set; } = "";
    [FieldOrder(95)] public int Int094 { get; set; }
    [FieldOrder(96)] public string Str095 { get; set; } = "";
    [FieldOrder(97)] public int Int096 { get; set; }
    [FieldOrder(98)] public string Str097 { get; set; } = "";
    [FieldOrder(99)] public int Int098 { get; set; }
    [FieldOrder(100)] public string Str099 { get; set; } = "";
    [FieldOrder(101)] public int Int100 { get; set; }
    [FieldOrder(102)] public string Str101 { get; set; } = "";
    [FieldOrder(103)] public int Int102 { get; set; }
    [FieldOrder(104)] public string Str103 { get; set; } = "";
    [FieldOrder(105)] public int Int104 { get; set; }
    [FieldOrder(106)] public string Str105 { get; set; } = "";
    [FieldOrder(107)] public int Int106 { get; set; }
    [FieldOrder(108)] public string Str107 { get; set; } = "";
    [FieldOrder(109)] public int Int108 { get; set; }
    [FieldOrder(110)] public string Str109 { get; set; } = "";
    [FieldOrder(111)] public int Int110 { get; set; }
    [FieldOrder(112)] public string Str111 { get; set; } = "";
    [FieldOrder(113)] public int Int112 { get; set; }
    [FieldOrder(114)] public string Str113 { get; set; } = "";
    [FieldOrder(115)] public int Int114 { get; set; }
    [FieldOrder(116)] public string Str115 { get; set; } = "";
    [FieldOrder(117)] public int Int116 { get; set; }
    [FieldOrder(118)] public string Str117 { get; set; } = "";
    [FieldOrder(119)] public int Int118 { get; set; }
    [FieldOrder(120)] public string Str119 { get; set; } = "";
    [FieldOrder(121)] public int Int120 { get; set; }
    [FieldOrder(122)] public string Str121 { get; set; } = "";
    [FieldOrder(123)] public int Int122 { get; set; }
    [FieldOrder(124)] public string Str123 { get; set; } = "";
    [FieldOrder(125)] public int Int124 { get; set; }
    [FieldOrder(126)] public string Str125 { get; set; } = "";
    [FieldOrder(127)] public int Int126 { get; set; }
    [FieldOrder(128)] public string Str127 { get; set; } = "";
    [FieldOrder(129)] public int Int128 { get; set; }
    [FieldOrder(130)] public string Str129 { get; set; } = "";
    [FieldOrder(131)] public int Int130 { get; set; }
    [FieldOrder(132)] public string Str131 { get; set; } = "";
    [FieldOrder(133)] public int Int132 { get; set; }
    [FieldOrder(134)] public string Str133 { get; set; } = "";
    [FieldOrder(135)] public int Int134 { get; set; }
    [FieldOrder(136)] public string Str135 { get; set; } = "";
    [FieldOrder(137)] public int Int136 { get; set; }
    [FieldOrder(138)] public string Str137 { get; set; } = "";
    [FieldOrder(139)] public int Int138 { get; set; }
    [FieldOrder(140)] public string Str139 { get; set; } = "";
    [FieldOrder(141)] public int Int140 { get; set; }
    [FieldOrder(142)] public string Str141 { get; set; } = "";
    [FieldOrder(143)] public int Int142 { get; set; }
    [FieldOrder(144)] public string Str143 { get; set; } = "";
    [FieldOrder(145)] public int Int144 { get; set; }
    [FieldOrder(146)] public string Str145 { get; set; } = "";
    [FieldOrder(147)] public int Int146 { get; set; }
    [FieldOrder(148)] public string Str147 { get; set; } = "";
    [FieldOrder(149)] public int Int148 { get; set; }
    [FieldOrder(150)] public string Str149 { get; set; } = "";
}

/// <summary>Formats a <see cref="WideRecord"/> back to its line, for <see cref="DataCheck.Wide"/>.</summary>
internal static class WideRecordText
{
    public static string Of(WideRecord r) => string.Join(',', new[]
    {
        r.Int000.ToString(CultureInfo.InvariantCulture), r.Str001, r.Int002.ToString(CultureInfo.InvariantCulture), r.Str003, r.Int004.ToString(CultureInfo.InvariantCulture),
        r.Str005, r.Int006.ToString(CultureInfo.InvariantCulture), r.Str007, r.Int008.ToString(CultureInfo.InvariantCulture), r.Str009,
        r.Int010.ToString(CultureInfo.InvariantCulture), r.Str011, r.Int012.ToString(CultureInfo.InvariantCulture), r.Str013, r.Int014.ToString(CultureInfo.InvariantCulture),
        r.Str015, r.Int016.ToString(CultureInfo.InvariantCulture), r.Str017, r.Int018.ToString(CultureInfo.InvariantCulture), r.Str019,
        r.Int020.ToString(CultureInfo.InvariantCulture), r.Str021, r.Int022.ToString(CultureInfo.InvariantCulture), r.Str023, r.Int024.ToString(CultureInfo.InvariantCulture),
        r.Str025, r.Int026.ToString(CultureInfo.InvariantCulture), r.Str027, r.Int028.ToString(CultureInfo.InvariantCulture), r.Str029,
        r.Int030.ToString(CultureInfo.InvariantCulture), r.Str031, r.Int032.ToString(CultureInfo.InvariantCulture), r.Str033, r.Int034.ToString(CultureInfo.InvariantCulture),
        r.Str035, r.Int036.ToString(CultureInfo.InvariantCulture), r.Str037, r.Int038.ToString(CultureInfo.InvariantCulture), r.Str039,
        r.Int040.ToString(CultureInfo.InvariantCulture), r.Str041, r.Int042.ToString(CultureInfo.InvariantCulture), r.Str043, r.Int044.ToString(CultureInfo.InvariantCulture),
        r.Str045, r.Int046.ToString(CultureInfo.InvariantCulture), r.Str047, r.Int048.ToString(CultureInfo.InvariantCulture), r.Str049,
        r.Int050.ToString(CultureInfo.InvariantCulture), r.Str051, r.Int052.ToString(CultureInfo.InvariantCulture), r.Str053, r.Int054.ToString(CultureInfo.InvariantCulture),
        r.Str055, r.Int056.ToString(CultureInfo.InvariantCulture), r.Str057, r.Int058.ToString(CultureInfo.InvariantCulture), r.Str059,
        r.Int060.ToString(CultureInfo.InvariantCulture), r.Str061, r.Int062.ToString(CultureInfo.InvariantCulture), r.Str063, r.Int064.ToString(CultureInfo.InvariantCulture),
        r.Str065, r.Int066.ToString(CultureInfo.InvariantCulture), r.Str067, r.Int068.ToString(CultureInfo.InvariantCulture), r.Str069,
        r.Int070.ToString(CultureInfo.InvariantCulture), r.Str071, r.Int072.ToString(CultureInfo.InvariantCulture), r.Str073, r.Int074.ToString(CultureInfo.InvariantCulture),
        r.Str075, r.Int076.ToString(CultureInfo.InvariantCulture), r.Str077, r.Int078.ToString(CultureInfo.InvariantCulture), r.Str079,
        r.Int080.ToString(CultureInfo.InvariantCulture), r.Str081, r.Int082.ToString(CultureInfo.InvariantCulture), r.Str083, r.Int084.ToString(CultureInfo.InvariantCulture),
        r.Str085, r.Int086.ToString(CultureInfo.InvariantCulture), r.Str087, r.Int088.ToString(CultureInfo.InvariantCulture), r.Str089,
        r.Int090.ToString(CultureInfo.InvariantCulture), r.Str091, r.Int092.ToString(CultureInfo.InvariantCulture), r.Str093, r.Int094.ToString(CultureInfo.InvariantCulture),
        r.Str095, r.Int096.ToString(CultureInfo.InvariantCulture), r.Str097, r.Int098.ToString(CultureInfo.InvariantCulture), r.Str099,
        r.Int100.ToString(CultureInfo.InvariantCulture), r.Str101, r.Int102.ToString(CultureInfo.InvariantCulture), r.Str103, r.Int104.ToString(CultureInfo.InvariantCulture),
        r.Str105, r.Int106.ToString(CultureInfo.InvariantCulture), r.Str107, r.Int108.ToString(CultureInfo.InvariantCulture), r.Str109,
        r.Int110.ToString(CultureInfo.InvariantCulture), r.Str111, r.Int112.ToString(CultureInfo.InvariantCulture), r.Str113, r.Int114.ToString(CultureInfo.InvariantCulture),
        r.Str115, r.Int116.ToString(CultureInfo.InvariantCulture), r.Str117, r.Int118.ToString(CultureInfo.InvariantCulture), r.Str119,
        r.Int120.ToString(CultureInfo.InvariantCulture), r.Str121, r.Int122.ToString(CultureInfo.InvariantCulture), r.Str123, r.Int124.ToString(CultureInfo.InvariantCulture),
        r.Str125, r.Int126.ToString(CultureInfo.InvariantCulture), r.Str127, r.Int128.ToString(CultureInfo.InvariantCulture), r.Str129,
        r.Int130.ToString(CultureInfo.InvariantCulture), r.Str131, r.Int132.ToString(CultureInfo.InvariantCulture), r.Str133, r.Int134.ToString(CultureInfo.InvariantCulture),
        r.Str135, r.Int136.ToString(CultureInfo.InvariantCulture), r.Str137, r.Int138.ToString(CultureInfo.InvariantCulture), r.Str139,
        r.Int140.ToString(CultureInfo.InvariantCulture), r.Str141, r.Int142.ToString(CultureInfo.InvariantCulture), r.Str143, r.Int144.ToString(CultureInfo.InvariantCulture),
        r.Str145, r.Int146.ToString(CultureInfo.InvariantCulture), r.Str147, r.Int148.ToString(CultureInfo.InvariantCulture), r.Str149,
    });
}

/// <summary>150 columns (75 int, 75 string) per record: the cost of a wide record, mostly per-field work.</summary>
[MemoryDiagnoser]
public class WideRecordBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<WideRecord> _engine = null!;

    [Params(1_000, 10_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(WideRecordBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureWideRecords(dataDir, Rows);
        _engine = new FileHelperEngine<WideRecord>();
    }

    // Correctness guard: each setup runs only the guard of its own benchmark (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadFile))]
    public void SetupReadFile()
    {
        Prepare();
        var records = ReadFile();
        DataCheck.Wide("FileHelpers", _filePath, Rows, records.Length, i => WideRecordText.Of(records[i]));
    }

    [GlobalSetup(Target = nameof(ReadFile_ParseAndDiscard))]
    public void SetupDiscard()
    {
        Prepare();
        DataCheck.Equal(ExpectedChecksum(), ReadFile_ParseAndDiscard(), "parse-and-discard checksum");
    }

    private long ExpectedChecksum()
    {
        long sum = 0;
        foreach (var line in DataCheck.DataLines(_filePath, hasHeader: false))
            sum += long.Parse(line.AsSpan(0, line.IndexOf(',')), CultureInfo.InvariantCulture);
        return sum;
    }

    [Benchmark(Baseline = true)]
    public WideRecord[] ReadFile() => _engine.ReadFile(_filePath);

    /// <summary>Every record parsed completely, none kept; the checksum is the sum of the first column.</summary>
    [Benchmark]
    public long ReadFile_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Int000);
        return checksum;
    }
}
