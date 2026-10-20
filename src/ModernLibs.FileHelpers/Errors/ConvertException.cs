using System.Globalization;
using System.Text;

namespace ModernLibs.FileHelpers;

// RCS1194: the parameterless / (message) / (message, inner) constructors are intentionally absent —
// a conversion error always has a source value and a destination type (FileHelpers 3.x shape).
#pragma warning disable RCS1194

/// <summary>
/// Indicates that a field value can't be converted to the destination type.
/// Matches the FileHelpers 3.x <c>ConvertException</c>: custom converters may throw it directly, and the
/// generated descriptors raise it for every failed conversion of a non-empty field.
/// </summary>
public sealed class ConvertException : FileHelpersException
{
    /// <summary>Creates a conversion error for <paramref name="origValue"/> and <paramref name="destType"/>.</summary>
    /// <param name="origValue">The value that could not be converted.</param>
    /// <param name="destType">The destination type.</param>
    public ConvertException(string? origValue, Type destType)
        : this(origValue, destType, string.Empty)
    {
    }

    /// <summary>Creates a conversion error with additional information.</summary>
    /// <param name="origValue">The value that could not be converted.</param>
    /// <param name="destType">The destination type.</param>
    /// <param name="extraInfo">Additional information about the error.</param>
    public ConvertException(string? origValue, Type destType, string? extraInfo)
        : this(origValue, destType, string.Empty, -1, -1, extraInfo, null)
    {
    }

    /// <summary>Creates a conversion error with full position information.</summary>
    /// <param name="origValue">The value that could not be converted.</param>
    /// <param name="destType">The destination type.</param>
    /// <param name="fieldName">The name of the field with the error.</param>
    /// <param name="lineNumber">The 1-based line where the error was found, or <c>-1</c>.</param>
    /// <param name="columnNumber">The 1-based estimated column, or <c>-1</c>.</param>
    /// <param name="extraInfo">Additional information about the error.</param>
    /// <param name="innerEx">The underlying exception, if any.</param>
    public ConvertException(
        string? origValue,
        Type destType,
        string? fieldName,
        int lineNumber,
        int columnNumber,
        string? extraInfo,
        Exception? innerEx)
        : base(BuildMessage(origValue, destType, fieldName, lineNumber, columnNumber, extraInfo), innerEx)
    {
        FieldStringValue = origValue;
        FieldType = destType;
        FieldName = fieldName;
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        MessageExtra = extraInfo ?? string.Empty;
        MessageOriginal = origValue is not null && destType is not null
            ? BuildOriginal(origValue, destType)
            : string.Empty;
    }

    /// <summary>The destination type.</summary>
    public Type FieldType { get; }

    /// <summary>The value that can't be converted (<see langword="null"/> when unknown).</summary>
    public string? FieldStringValue { get; }

    /// <summary>Extra information about the error.</summary>
    public string MessageExtra { get; }

    /// <summary>The message without the line, column and field name.</summary>
    public string MessageOriginal { get; }

    /// <summary>The name of the field related to the exception (<see langword="null"/> or empty when unknown).</summary>
    public string? FieldName { get; internal set; }

    /// <inheritdoc/>
    /// <remarks>Rebuilt on each access so a line number attached by the engine is included.</remarks>
    public override string Message =>
        BuildMessage(FieldStringValue, FieldType, FieldName, LineNumber, ColumnNumber, MessageExtra);

    private static string BuildOriginal(string origValue, Type destType) =>
        "Error Converting '" + origValue + "' to type: '" + destType.Name + "'. ";

    private static string BuildMessage(
        string? origValue,
        Type? destType,
        string? fieldName,
        int lineNumber,
        int columnNumber,
        string? extraInfo)
    {
        var sb = new StringBuilder();
        if (lineNumber >= 0)
            sb.Append(CultureInfo.InvariantCulture, $"Line: {lineNumber}. ");
        if (columnNumber >= 0)
            sb.Append(CultureInfo.InvariantCulture, $"Column: {columnNumber}. ");
        if (!string.IsNullOrEmpty(fieldName))
            sb.Append("Field: ").Append(fieldName).Append(". ");
        if (origValue is not null && destType is not null)
            sb.Append(BuildOriginal(origValue, destType));
        sb.Append(extraInfo);
        return sb.ToString();
    }
}
