using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Describes a single mapped field in a record layout (see <see cref="IRecordDescriptor{T}.Fields"/>).
/// </summary>
/// <remarks>
/// The constructor is infrastructure for source-generated code and not intended for direct use;
/// generated code in consumer assemblies binds to its exact signature, which therefore stays stable in 4.x.
/// </remarks>
/// <param name="Name">The field or property name as declared in source.</param>
/// <param name="FieldType">The CLR type of the field (resolved via <c>typeof()</c> — AOT-safe).</param>
/// <param name="Order">The zero-based column index in the record layout.</param>
/// <param name="Offset">
/// For fixed-length records: the zero-based character offset of this field within the line.
/// <c>-1</c> for delimited records (no fixed position). Default: <c>-1</c>.
/// </param>
/// <param name="Width">
/// For fixed-length records: the character width of this field.
/// <c>-1</c> for delimited records (no fixed width). Default: <c>-1</c>.
/// </param>
/// <param name="Delimiter">
/// For delimited records: the per-field delimiter override from [FieldDelimiter], the delimiter that follows
/// this field (FileHelpers 3.x). <c>null</c> means "use record-level delimiter". Default: <c>null</c>.
/// </param>
/// <param name="IsInNewLine">
/// When <see langword="true"/>, this field begins on a new input line (from [FieldInNewLine]).
/// Default: <see langword="false"/>.
/// </param>
/// <param name="Caption">
/// Optional display caption from [FieldCaption]. Used in header generation instead of <see cref="Name"/>.
/// <c>null</c> means "use field name". Default: <c>null</c>.
/// </param>
/// <param name="CultureName">
/// Optional culture name (e.g., "fr-FR") from [FieldConverter] for locale-specific parsing/formatting.
/// <c>null</c> means "use InvariantCulture". Default: <c>null</c>.
/// </param>
/// <param name="IsOptional">
/// When <see langword="true"/>, the field is marked with [FieldOptional]: a record may end before it,
/// in which case the field reads as empty. Only trailing fields can be optional. Default: <see langword="false"/>.
/// </param>
/// <param name="TrimMode">
/// The field's [FieldTrim] mode (<see cref="TrimMode.None"/> when absent). Lets the engine apply
/// FileHelpers 3.x rules that depend on trimming, such as blanks before an opening quote.
/// </param>
/// <param name="QuoteChar">
/// The [FieldQuoted] quote character, or <c>'\0'</c> when the field is not quoted (quote characters in it are
/// literal text). Default: <c>'\0'</c>.
/// </param>
/// <param name="QuoteMultiline">
/// Whether the quoted value may span several lines (from [FieldQuoted] or the record-level override).
/// Ignored when <paramref name="QuoteChar"/> is <c>'\0'</c>. Default: <see cref="MultilineMode.AllowForBoth"/>.
/// </param>
/// <param name="TrimChars">
/// The [FieldTrim] characters skipped before an opening quote (Left/Both) and after a closing quote
/// (Right/Both) of a quoted field (FileHelpers 3.x). <see langword="null"/> means spaces and tabs.
/// Default: <see langword="null"/>.
/// </param>
[method: EditorBrowsable(EditorBrowsableState.Never)]
public readonly record struct FieldDescriptor(
    string Name,
    Type FieldType,
    int Order,
    int Offset = -1,
    int Width = -1,
    string? Delimiter = null,
    bool IsInNewLine = false,
    string? Caption = null,
    string? CultureName = null,
    bool IsOptional = false,
    TrimMode TrimMode = TrimMode.None,
    char QuoteChar = '\0',
    MultilineMode QuoteMultiline = MultilineMode.AllowForBoth,
    string? TrimChars = null);
