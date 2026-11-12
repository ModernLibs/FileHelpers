using System.Reflection;

namespace ModernLibs.FileHelpers.Tests.Compat;

/// <summary>
/// Hand-made records that each 4.9.1 compat shape writes in addition to what a read produced. They reach the
/// write-only branches of the generated <c>Format</c> code: a line break in a quoted value (accepted, or the
/// "new line inside" <see cref="BadUsageException"/> with <c>MultilineMode.NotAllow</c>), a fixed-length value
/// longer than its width (cut, or an error with <c>OverflowMode.Error</c>), the grow loop of the converter buffer
/// (a converted value longer than 64 chars), a converter that throws while formatting, and null members.
/// </summary>
internal static class Compat49WriteCases
{
    private static readonly string Long100 = new('w', 100);

    private static readonly Dictionary<string, (string Label, (string Member, object? Value)[] Values)[]> Cases =
        new(StringComparer.Ordinal)
        {
            ["Compat49Plain25"] =
            [
                ("null-strings", [("Id", 1), ("Version", null), ("Authors", null), ("RepositoryUrl", null)]),
                ("extremes", [("Id", int.MinValue), ("PackageSize", long.MaxValue), ("Published", DateTime.MinValue), ("LastUpdated", DateTime.MaxValue), ("Description", "has, comma")]),
                ("line-break", [("Id", 2), ("Description", "a\r\nb")]),
            ],
            ["Compat49Optional"] =
            [
                ("nulls", [("Id", 1), ("Name", null), ("Note", null), ("Extra", null)]),
                ("separator", [("Id", 2), ("Name", "x,y"), ("Note", "n"), ("Extra", -1)]),
            ],
            ["Compat49PerFieldDelimiter"] =
            [
                ("delimiters-in-values", [("Id", 1), ("Code", "a|b::c"), ("Amount", 123456789012.345m), ("Tail", "t,u")]),
                ("nulls", [("Id", 2), ("Code", null), ("Tail", null)]),
            ],
            ["Compat49NewLine"] =
            [
                ("line-break-in-quoted", [("Id", 1), ("Name", "a\r\nb"), ("Street", "s"), ("Zip", 1)]),
                ("nulls", [("Id", 2), ("Name", null), ("Street", null)]),
                ("long-quoted", [("Id", 3), ("Name", Long100 + "\"q\""), ("Street", "x,y"), ("Zip", -1)]),
            ],
            ["Compat49Trim"] =
            [
                ("line-break-in-quoted", [("Id", 1), ("Name", "a\nb"), ("Code", "c")]),
                ("nulls", [("Id", 2), ("Name", null), ("Code", null)]),
                ("trim-chars", [("Id", 3), ("Name", "xx q x"), ("Code", " x ")]),
            ],
            ["Compat49QuoteAlways"] = QuoteCases(),
            ["Compat49QuoteOptionalRead"] = QuoteCases(),
            ["Compat49QuoteOptionalWrite"] = QuoteCases(),
            ["Compat49QuoteOptionalBoth"] = QuoteCases(),
            ["Compat49MultilineNotAllow"] = QuoteCases(),
            ["Compat49Recyclable"] =
            [
                ("nulls", [("Id", 1), ("Name", null), ("Amount", null)]),
                ("line-break-unquoted", [("Id", 2), ("Name", "a\nb"), ("Amount", 10000000000m), ("Flag", true)]),
            ],
            ["Compat49Struct"] =
            [
                ("default", []),
                ("separator", [("Id", 1), ("Name", "a;b"), ("Day", DateOnly.MaxValue), ("Ratio", double.NaN)]),
            ],
            ["Compat49Ctor"] =
            [
                ("null-name", [("Id", 1), ("Name", null)]),
                ("separator", [("Id", 2), ("Name", "a,b"), ("Day", DateTime.MaxValue)]),
            ],
            ["Compat49FixedExact"] = FixedCases(nullableAmount: false),
            ["Compat49FixedMore"] = FixedCases(nullableAmount: false),
            ["Compat49FixedLess"] = FixedCases(nullableAmount: true),
            ["Compat49FixedVariable"] = FixedCases(nullableAmount: true),
            ["Compat49ConverterBase"] =
            [
                ("long-converted", [("Id", 1), ("Name", Long100), ("Hex", 255), ("Code", "c")]),
                ("nulls", [("Id", 2), ("Name", null), ("Hex", 0), ("Code", null)]),
                ("long-with-argument", [("Id", 3), ("Name", "n"), ("Hex", -1), ("Code", new string('c', 200))]),
            ],
            ["Compat49SpanConverter"] =
            [
                ("converter-throws", [("Id", 1), ("Price", decimal.MaxValue)]),
                ("null-nullable", [("Id", 2), ("Price", 0.01m), ("Discount", null)]),
                ("fraction", [("Id", 3), ("Price", 1.005m), ("Discount", -0.5m)]),
            ],
        };

    /// <summary>The hand-made records of the shape <paramref name="name"/> as (label, record) pairs.</summary>
    public static IEnumerable<(string Label, Func<T> Create)> For<T>(string name) =>
        Cases[name].Select(c => (c.Label, (Func<T>)(() => Make<T>(c.Values))));

    /// <summary>
    /// Creates a <typeparamref name="T"/> with the given member values: through the constructor with the most
    /// parameters (bound by name, other parameters get their default) when the type has no parameterless one,
    /// then through the property setters.
    /// </summary>
    private static T Make<T>((string Member, object? Value)[] values)
    {
        var type = typeof(T);
        var lookup = values.ToDictionary(v => v.Member, v => v.Value, StringComparer.OrdinalIgnoreCase);
        object instance;
        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (type.IsValueType || ctor is not null)
        {
            instance = Activator.CreateInstance(type)!;
        }
        else
        {
            var withArgs = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            var args = withArgs.GetParameters()
                .Select(p => lookup.Remove(p.Name!, out var v) ? v : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                .ToArray();
            instance = withArgs.Invoke(args);
        }

        foreach (var (member, value) in lookup)
            type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance)!.SetValue(instance, value);
        return (T)instance;
    }

    /// <summary>The cases of the five quote and multiline shapes: <c>Id, [FieldQuoted] Name, Tail</c>.</summary>
    private static (string, (string, object?)[])[] QuoteCases() =>
    [
        ("line-break", [("Id", 1), ("Name", "a\r\nb"), ("Tail", "t")]),
        ("quote-and-separator", [("Id", 2), ("Name", "say \"hi\", ok"), ("Tail", "t")]),
        ("plain", [("Id", 3), ("Name", "plain"), ("Tail", "t")]),
        ("nulls", [("Id", 4), ("Name", null), ("Tail", null)]),
        ("long", [("Id", 5), ("Name", Long100), ("Tail", "t")]),
    ];

    /// <summary>The cases of the four fixed-length shapes: Id 5, Name 8, Amount 6.</summary>
    private static (string, (string, object?)[])[] FixedCases(bool nullableAmount)
    {
        var cases = new List<(string, (string, object?)[])>
        {
            ("name-overflow", [("Id", 1), ("Name", "ABCDEFGHIJKL"), ("Amount", 1m)]),
            ("id-and-amount-overflow", [("Id", 1234567), ("Name", "n"), ("Amount", 123456.78m)]),
            ("null-name", [("Id", 2), ("Name", null), ("Amount", -0.5m)]),
        };
        if (nullableAmount)
            cases.Add(("null-amount", [("Id", 3), ("Name", "x"), ("Amount", null)]));
        return [.. cases];
    }
}
