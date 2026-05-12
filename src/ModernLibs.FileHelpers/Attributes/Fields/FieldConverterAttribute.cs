namespace ModernLibs.FileHelpers;

/// <summary>
/// Selects a converter for parsing and formatting this field's value.
/// Use the <see cref="ConverterKind"/> overload for built-in converters.
/// Use the <see cref="Type"/> overload for custom converters.
/// The attribute is a pure data token — no converter instance is created at attribute construction time
/// (required for AOT compatibility; the source generator resolves the converter at compile time).
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldConverterAttribute : Attribute
{
    /// <summary>The built-in converter kind. <see cref="ConverterKind.None"/> when using a custom type.</summary>
    public ConverterKind Kind { get; }

    /// <summary>
    /// The custom converter type. Must implement <c>ISpanConverter&lt;T&gt;</c>.
    /// <see langword="null"/> when using a <see cref="ConverterKind"/> overload.
    /// </summary>
    public Type? ConverterType { get; }

    /// <summary>Additional string arguments forwarded to the converter's constructor (e.g., format strings for dates).</summary>
    public string[] ConverterArgs { get; }

    /// <summary>
    /// Gets or sets the culture name (e.g., "fr-FR", "de-DE") to use when parsing
    /// and formatting this field. When null or empty, <see cref="System.Globalization.CultureInfo.InvariantCulture"/>
    /// is used.
    /// </summary>
    public string? CultureName { get; set; }

    /// <summary>Uses the specified built-in converter with optional format arguments.</summary>
    /// <param name="converter">The built-in converter to use.</param>
    /// <param name="args">Optional format arguments (e.g., <c>"yyyy-MM-dd"</c> for date converters).</param>
    public FieldConverterAttribute(ConverterKind converter, params string[] args)
    {
        Kind = converter;
        ConverterType = null;
        ConverterArgs = args;
    }

    /// <summary>Uses a custom converter type with optional arguments.</summary>
    /// <param name="customConverter">
    /// The type of the custom converter. Must be a compile-time <see langword="typeof"/> expression.
    /// The type must implement <c>ISpanConverter&lt;T&gt;</c> (resolved by the source generator at compile time).
    /// </param>
    /// <param name="args">Optional arguments forwarded to the converter.</param>
    public FieldConverterAttribute(Type customConverter, params string[] args)
    {
        Kind = ConverterKind.None;
        ConverterType = customConverter;
        ConverterArgs = args;
    }
}
