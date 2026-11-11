// MLFH001: the plain Compat49 record types have no record attributes on purpose (see Generated49).
#pragma warning disable MLFH001

using System.Globalization;
using System.Reflection;
using System.Text;

namespace ModernLibs.FileHelpers.Tests.Compat;

/// <summary>
/// For each 4.9.1 compat record shape: functions that read an input with the 4.9.1 descriptor (old) and with the
/// current one (new), and that read then write it back. Each returns a canonical text of the records, the errors
/// (<c>line:type:message</c>), <c>HeaderText</c>, <c>FooterText</c>, <c>LineNumber</c> and <c>TotalRecords</c>,
/// with the record type name replaced by <c>T</c>, so the old and the new text are equal when the descriptors behave
/// the same.
/// </summary>
internal static class Compat49Matrix
{
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
        yield return Case<Compat49Recyclable, Compat49RecyclableTwin>(recycle: true);
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

    private static (string, Func<string, string>, Func<string, string>, Func<string, string>, Func<string, string>) Case<TOld, TNew>(bool recycle = false) =>
        (typeof(TOld).Name,
         input => Read<TOld>(input, recycle),
         input => Read<TNew>(input, recycle),
         RoundTrip<TOld>,
         RoundTrip<TNew>);

    private static int FieldCountOf(string name, bool twin)
    {
        var type = typeof(Compat49Matrix).Assembly.GetType(typeof(Compat49Matrix).Namespace + "." + name + (twin ? "Twin" : string.Empty), throwOnError: true)!;
        var method = typeof(Compat49Matrix).GetMethod(nameof(DescriptorFieldCount), BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)method.MakeGenericMethod(type).Invoke(null, null)!;
    }

    private static int DescriptorFieldCount<T>() => FileHelperRegistry<T>.Descriptor.Fields.Length;

    /// <summary>
    /// Reads <paramref name="input"/> with <see cref="ErrorMode.SaveAndContinue"/>. With <paramref name="recycle"/>
    /// the callback read runs with <c>RecycleRecords</c> (the <c>ParseInto</c> path of a recyclable descriptor).
    /// </summary>
    private static string Read<T>(string input, bool recycle)
    {
        var engine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue, RecycleRecords = recycle };
        var text = new StringBuilder();
        try
        {
            if (recycle)
                engine.ReadString(input, r => text.Append("record:").AppendLine(Show(r)));
            else
                foreach (var r in engine.ReadString(input))
                    text.Append("record:").AppendLine(Show(r));
        }
        catch (Exception ex)
        {
            text.Append("throw:").Append(ex.GetType().Name).Append(':').AppendLine(Escape(ex.Message));
        }

        AppendState(text, engine);
        return Canonical(text, typeof(T));
    }

    /// <summary>Reads <paramref name="input"/>, then writes the records back with the same engine.</summary>
    private static string RoundTrip<T>(string input)
    {
        var engine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
        var text = new StringBuilder();
        try
        {
            var records = engine.ReadString(input);
            text.Append("written:").AppendLine(Escape(engine.WriteString(records)));
        }
        catch (Exception ex)
        {
            text.Append("throw:").Append(ex.GetType().Name).Append(':').AppendLine(Escape(ex.Message));
        }

        AppendState(text, engine);
        return Canonical(text, typeof(T));
    }

    private static void AppendState<T>(StringBuilder text, FileHelperEngine<T> engine)
    {
        foreach (var e in engine.ErrorManager.Errors)
            text.Append("error:").Append(e.LineNumber).Append(':').Append(e.ExceptionInfo?.GetType().Name).Append(':')
                .Append(Escape(e.ExceptionInfo?.Message)).Append(':').AppendLine(Escape(e.RecordString));
        text.Append("header:").AppendLine(Escape(engine.HeaderText));
        text.Append("footer:").AppendLine(Escape(engine.FooterText));
        text.Append("line:").AppendLine(engine.LineNumber.ToString(CultureInfo.InvariantCulture));
        text.Append("total:").AppendLine(engine.TotalRecords.ToString(CultureInfo.InvariantCulture));
    }

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
