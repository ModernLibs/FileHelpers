// MLFH001: the plain Compat49 record types have no record attributes on purpose (see Generated49).
#pragma warning disable MLFH001

using System.Globalization;
using System.Reflection;
using System.Text;

namespace ModernLibs.FileHelpers.Tests.Compat;

/// <summary>
/// For each 4.9.1 compat record shape: functions that read an input with the 4.9.1 descriptor (old) and with the
/// current one (new), and that read then write it back. Each returns a canonical text of the records, the errors
/// (<c>line:type:message:record</c>), <c>HeaderText</c>, <c>FooterText</c>, <c>LineNumber</c> and
/// <c>TotalRecords</c>, with the record type name replaced by <c>T</c>, so the old and the new text are equal when
/// the descriptors behave the same. Lines end with <c>\n</c> on every platform (the golden files in
/// <c>Golden49</c> hold the old text).
/// </summary>
internal static class Compat49Matrix
{
    /// <summary>
    /// Shapes whose descriptor has no <c>ParseInto</c>, so a read with <c>RecycleRecords</c> runs the same
    /// <c>Parse</c> as the array read: <c>Compat49Struct</c> is a value type (the engine ignores
    /// <c>RecycleRecords</c>), <c>Compat49Ctor</c> sets its get-only members through the constructor (its
    /// descriptor implements <c>IRecordDescriptor&lt;T&gt;</c> only). Every other shape implements
    /// <c>IRecyclableDescriptor&lt;T&gt;</c>.
    /// </summary>
    private static readonly HashSet<string> NoParseInto = new(StringComparer.Ordinal) { "Compat49Struct", "Compat49Ctor" };

    public static IEnumerable<(string Name, Func<string, string> ReadOld, Func<string, string> ReadNew, Func<string, string> RoundTripOld, Func<string, string> RoundTripNew)> All()
    {
        yield return Case<Compat49Plain25, Compat49Plain25Twin>();
        yield return Case<Compat49Optional, Compat49OptionalTwin>();
        yield return Case<Compat49PerFieldDelimiter, Compat49PerFieldDelimiterTwin>();
        yield return Case<Compat49NewLine, Compat49NewLineTwin>();
        yield return Case<Compat49Trim, Compat49TrimTwin>();
        yield return Case<Compat49QuoteAlways, Compat49QuoteAlwaysTwin>();
        yield return Case<Compat49QuoteOptionalRead, Compat49QuoteOptionalReadTwin>();
        yield return Case<Compat49QuoteOptionalWrite, Compat49QuoteOptionalWriteTwin>();
        yield return Case<Compat49QuoteOptionalBoth, Compat49QuoteOptionalBothTwin>();
        yield return Case<Compat49MultilineNotAllow, Compat49MultilineNotAllowTwin>();
        yield return Case<Compat49Recyclable, Compat49RecyclableTwin>();
        yield return Case<Compat49Struct, Compat49StructTwin>();
        yield return Case<Compat49Ctor, Compat49CtorTwin>();
        yield return Case<Compat49FixedExact, Compat49FixedExactTwin>();
        yield return Case<Compat49FixedMore, Compat49FixedMoreTwin>();
        yield return Case<Compat49FixedLess, Compat49FixedLessTwin>();
        yield return Case<Compat49FixedVariable, Compat49FixedVariableTwin>();
        yield return Case<Compat49ConverterBase, Compat49ConverterBaseTwin>();
        yield return Case<Compat49SpanConverter, Compat49SpanConverterTwin>();
    }

    /// <summary>The field count of the old and of the new descriptor of each shape.</summary>
    public static IEnumerable<(string Name, int Old, int New)> FieldCounts()
    {
        foreach (var c in All())
            yield return (c.Name, FieldCountOf(c.Name, twin: false), FieldCountOf(c.Name, twin: true));
    }

    private static (string, Func<string, string>, Func<string, string>, Func<string, string>, Func<string, string>) Case<TOld, TNew>() =>
        (typeof(TOld).Name, Read<TOld>, Read<TNew>, RoundTrip<TOld>, RoundTrip<TNew>);

    private static int FieldCountOf(string name, bool twin)
    {
        var type = typeof(Compat49Matrix).Assembly.GetType(typeof(Compat49Matrix).Namespace + "." + name + (twin ? "Twin" : string.Empty), throwOnError: true)!;
        var method = typeof(Compat49Matrix).GetMethod(nameof(DescriptorFieldCount), BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)method.MakeGenericMethod(type).Invoke(null, null)!;
    }

    private static int DescriptorFieldCount<T>() => FileHelperRegistry<T>.Descriptor.Fields.Length;

    /// <summary>The shape name of a record type: the twin's name without <c>Twin</c>.</summary>
    private static string ShapeName(Type type) =>
        type.Name.EndsWith("Twin", StringComparison.Ordinal) ? type.Name[..^"Twin".Length] : type.Name;

    /// <summary>
    /// Reads <paramref name="input"/> three times: an array read with <see cref="ErrorMode.SaveAndContinue"/>
    /// (<c>Parse</c>); a callback read with <c>RecycleRecords</c> and <see cref="ErrorMode.SaveAndContinue"/>
    /// (<c>ParseInto</c>, prefix <c>recycled-</c>); an array read with the default
    /// <see cref="ErrorMode.ThrowException"/> (prefix <c>thrown-</c>).
    /// </summary>
    private static string Read<T>(string input)
    {
        var text = new StringBuilder();

        var engine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
        Run(text, string.Empty, () =>
        {
            foreach (var r in engine.ReadString(input))
                Line(text, "record:", Show(r));
        });
        AppendState(text, string.Empty, engine);

        if (NoParseInto.Contains(ShapeName(typeof(T))))
        {
            Line(text, "recycled-skipped:", "no ParseInto");
        }
        else
        {
            var recycling = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue, RecycleRecords = true };
            Run(text, "recycled-", () =>
            {
                int count = recycling.ReadString(input, r => Line(text, "recycled-record:", Show(r)));
                Line(text, "recycled-count:", count.ToString(CultureInfo.InvariantCulture));
            });
            AppendState(text, "recycled-", recycling);
        }

        var throwing = new FileHelperEngine<T>();
        Run(text, "thrown-", () => Line(text, "thrown-count:", throwing.ReadString(input).Length.ToString(CultureInfo.InvariantCulture)));
        AppendState(text, "thrown-", throwing);

        return Canonical(text, typeof(T));
    }

    /// <summary>
    /// Reads <paramref name="input"/>, writes the records back with the same engine, then writes each hand-made
    /// record of <see cref="Compat49WriteCases"/> with its own engine (prefix <c>written-extra:</c>).
    /// </summary>
    private static string RoundTrip<T>(string input)
    {
        // Golden49 pins the 4.9.1 bytes, written with "\r\n" on every OS (T49: the default is now Environment.NewLine).
        var engine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue, NewLineForWrite = "\r\n" };
        var text = new StringBuilder();
        Run(text, string.Empty, () =>
        {
            var records = engine.ReadString(input);

            // Write back with the header/footer in the form 4.9.1 read them (T49, Ruling R41), then restore them.
            string? header = engine.HeaderText;
            string? footer = engine.FooterText;
            engine.HeaderText = As491(header);
            engine.FooterText = As491(footer);
            Line(text, "written:", Escape(engine.WriteString(records)));
            engine.HeaderText = header;
            engine.FooterText = footer;
        });
        AppendState(text, string.Empty, engine);

        foreach (var (label, create) in Compat49WriteCases.For<T>(ShapeName(typeof(T))))
        {
            var writer = new FileHelperEngine<T> { NewLineForWrite = "\r\n" };
            Run(text, "written-extra:" + label + ":", () => Line(text, "written-extra:" + label + ":", Escape(writer.WriteString([create()]))));
        }

        return Canonical(text, typeof(T));
    }

    /// <summary>
    /// <c>HeaderText</c>/<c>FooterText</c> in the form 4.9.1 gave them, which the Golden49 files hold (they are never
    /// edited). Since T49 (Ruling R41) the engine follows FileHelpers 3.5.2: <c>""</c> instead of
    /// <see langword="null"/>, and each captured line followed by <see cref="Environment.NewLine"/> instead of lines
    /// joined with <c>"\r\n"</c>. The mapping is exact: any other form (a <see langword="null"/>, or a non-empty text
    /// that does not end with <see cref="Environment.NewLine"/>) gives a marker that cannot match a golden line.
    /// </summary>
    private static string? As491(string? text)
    {
        if (text is null)
            return "<unexpected null in 4.10>";
        if (text.Length == 0)
            return null;
        if (!text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            return "<not the 3.x form>" + text;
        return text[..^Environment.NewLine.Length].Replace(Environment.NewLine, "\r\n", StringComparison.Ordinal);
    }

    /// <summary>Runs <paramref name="action"/>; an exception becomes a <c>{prefix}throw:type:message</c> line.</summary>
    private static void Run(StringBuilder text, string prefix, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Line(text, prefix + "throw:", ex.GetType().Name + ":" + Escape(ex.Message));
        }
    }

    private static void AppendState<T>(StringBuilder text, string prefix, FileHelperEngine<T> engine)
    {
        foreach (var e in engine.ErrorManager.Errors)
        {
            Line(text, prefix + "error:",
                e.LineNumber.ToString(CultureInfo.InvariantCulture) + ":" + e.ExceptionInfo?.GetType().Name + ":" +
                Escape(e.ExceptionInfo?.Message) + ":" + Escape(e.RecordString));
        }

        Line(text, prefix + "header:", Escape(As491(engine.HeaderText)));
        Line(text, prefix + "footer:", Escape(As491(engine.FooterText)));
        Line(text, prefix + "line:", engine.LineNumber.ToString(CultureInfo.InvariantCulture));
        Line(text, prefix + "total:", engine.TotalRecords.ToString(CultureInfo.InvariantCulture));
    }

    private static void Line(StringBuilder text, string prefix, string value) => text.Append(prefix).Append(value).Append('\n');

    private static string Canonical(StringBuilder text, Type recordType) =>
        text.ToString()
            .Replace(recordType.FullName!, "T", StringComparison.Ordinal)
            .Replace(recordType.Name, "T", StringComparison.Ordinal);

    /// <summary>The public instance properties in declaration order, as <c>name=value</c>.</summary>
    private static string Show<T>(T record) =>
        string.Join("|", typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.MetadataToken)
            .Select(p => p.Name + "=" + Value(p.GetValue(record))));

    private static string Value(object? value) => value switch
    {
        null => "<null>",
        string s => "\"" + Escape(s) + "\"",
        DateTime d => d.ToString("o", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("o", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "<null>",
    };

    private static string Escape(string? s) =>
        s is null ? "<null>" : s.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
