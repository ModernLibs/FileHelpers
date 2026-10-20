// MA0048: Two related types (ConverterBase + ConverterBaseAdapter<T>) grouped in one file intentionally.
#pragma warning disable MA0048
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Legacy FileHelpers 3.x custom converter base class. Subclass and override
/// <see cref="StringToField"/> (and usually <see cref="FieldToString"/>).
/// </summary>
/// <remarks>
/// Reference the converter with <c>[FieldConverter(typeof(MyConverter))]</c>; the generated code wraps it
/// in a <see cref="ConverterBaseAdapter{T}"/>, which allocates one string per field.
/// For zero-allocation performance, implement <see cref="ISpanConverter{T}"/> directly instead.
/// </remarks>
public abstract class ConverterBase
{
    private static string s_defaultDateTimeFormat = DateFormat.LegacyDefault;

    /// <summary>
    /// Default format of <see cref="DateTimeConverter"/> when no format is given
    /// (implicit <see cref="DateTime"/> fields and <c>ConverterKind.Date</c> without arguments).
    /// FileHelpers 3.x default: <c>"ddMMyyyy"</c>.
    /// </summary>
    /// <remarks>Process-wide setting, read on every conversion; set it before reading or writing.</remarks>
    /// <exception cref="BadUsageException">The value is null, empty or not a valid date format.</exception>
    public static string DefaultDateTimeFormat
    {
        get => Volatile.Read(ref s_defaultDateTimeFormat);
        set
        {
            DateFormat.Validate(value);
            Volatile.Write(ref s_defaultDateTimeFormat, value);
        }
    }

    /// <summary>Converts a string field value to the target type.</summary>
    public abstract object? StringToField(string text);

    /// <summary>Converts the target type back to a string field value.</summary>
    /// <remarks>
    /// The default implementation (FileHelpers 3.x) writes <see cref="string.Empty"/> for
    /// <see langword="null"/> and <c>ToString()</c> otherwise — invariant culture for
    /// <see cref="IFormattable"/> values.
    /// </remarks>
    public virtual string? FieldToString(object? field) => field switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => field.ToString(),
    };

    /// <summary>
    /// FileHelpers 3.x switch: when <see langword="false"/> (default), an empty or whitespace-only field
    /// never reaches <see cref="StringToField"/> — it takes the field's null value instead
    /// (<c>[FieldNullValue]</c>, null for nullable / reference types, otherwise
    /// <see cref="NullValueNotFoundException"/>). When <see langword="true"/>, the converter receives
    /// every value, including the empty string.
    /// </summary>
    protected internal virtual bool CustomNullHandling => false;

    /// <summary>The field type this converter produces; set when the converter is bound to a field.</summary>
    internal Type? DestinationType { get; set; }

    /// <summary>Throws a <see cref="ConvertException"/> for <paramref name="from"/> with a custom message (3.x helper).</summary>
    /// <param name="from">The source string.</param>
    /// <param name="errorMsg">The custom error message.</param>
    /// <exception cref="ConvertException">Always.</exception>
    [DoesNotReturn]
    protected void ThrowConvertException(string from, string errorMsg)
        => throw new ConvertException(from, DestinationType ?? typeof(object), errorMsg);
}

/// <summary>
/// Bridges a <see cref="ConverterBase"/> implementation into the <see cref="ISpanConverter{T}"/> pipeline.
/// Infrastructure for source-generated code; not intended for direct use.
/// </summary>
/// <remarks>
/// This adapter allocates one string per operation. Use <see cref="ISpanConverter{T}"/>
/// for zero-alloc performance.
/// </remarks>
/// <typeparam name="T">The field type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ConverterBaseAdapter<T> : ISpanConverter<T>
{
    private readonly ConverterBase _inner;

    /// <summary>Initializes a new adapter wrapping the given <see cref="ConverterBase"/> instance.</summary>
    /// <param name="inner">The converter to wrap; it is bound to <typeparamref name="T"/> (its
    /// destination type becomes the underlying type of <typeparamref name="T"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public ConverterBaseAdapter(ConverterBase inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _inner.DestinationType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
    }

    /// <summary>
    /// The wrapped converter's <see cref="ConverterBase.CustomNullHandling"/>: when <see langword="false"/>,
    /// empty or whitespace-only input is handled as a null value without calling the converter.
    /// </summary>
    public bool CustomNullHandling => _inner.CustomNullHandling;

    /// <inheritdoc/>
    /// <remarks>
    /// Allocates one string per call (span → string for ConverterBase delegation).
    /// Unless <see cref="CustomNullHandling"/> is set, empty / whitespace-only input is not passed to the
    /// converter: it yields <see langword="default"/> when <typeparamref name="T"/> accepts null and
    /// raises <see cref="NullValueNotFoundException"/> otherwise (FileHelpers 3.x).
    /// A <see langword="null"/> result is a successful conversion when <typeparamref name="T"/> accepts
    /// null (reference or nullable value type); otherwise, like a result of the wrong type, it fails.
    /// Exceptions thrown by the wrapped converter propagate to the caller.
    /// </remarks>
    /// <exception cref="NullValueNotFoundException">
    /// The input is empty, <see cref="CustomNullHandling"/> is off and <typeparamref name="T"/> is a
    /// non-nullable value type.
    /// </exception>
    public bool TryParse(ReadOnlySpan<char> span, out T value)
    {
        if (!_inner.CustomNullHandling && span.IsWhiteSpace())
        {
            if (default(T) is not null)
            {
                throw new NullValueNotFoundException(
                    "No value found for the value type field of type '" + typeof(T).Name + "'. You must use the [FieldNullValue] attribute because this is a value type and can't be null or use a Nullable Type instead of the current type.");
            }

            value = default!;
            return true;
        }

        string s = new string(span);          // one allocation
        object? raw = _inner.StringToField(s);
        if (raw is T t) { value = t; return true; }
        value = default!;
        return raw is null && default(T) is null;
    }

    /// <inheritdoc/>
    /// <remarks>Allocates one string per call (ConverterBase returns string).</remarks>
    public bool TryFormat(T value, Span<char> destination, out int charsWritten)
    {
        string? s = _inner.FieldToString(value);     // one allocation (or null)
        if (s is null) { charsWritten = 0; return true; }
        if (!s.AsSpan().TryCopyTo(destination)) { charsWritten = 0; return false; }
        charsWritten = s.Length;
        return true;
    }
}
