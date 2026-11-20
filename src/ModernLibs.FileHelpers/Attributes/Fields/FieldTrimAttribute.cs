namespace ModernLibs.FileHelpers;

/// <summary>
/// Trims the field after reading (FileHelpers 3.x semantics).
/// </summary>
/// <remarks>
/// <para>
/// By default (FileHelpers 3.x behaviour) the field value is trimmed of white space (<see cref="string.Trim()"/>
/// rules) at the start, the end or both, according to <see cref="TrimMode"/>. The given <see cref="TrimChars"/> are
/// not trimmed from the value: for a <see cref="FieldQuotedAttribute"/> field in a delimited record they are skipped
/// before the opening quote (<see cref="FileHelpers.TrimMode.Left"/> / <see cref="FileHelpers.TrimMode.Both"/>) and
/// after the closing quote (<see cref="FileHelpers.TrimMode.Right"/> / <see cref="FileHelpers.TrimMode.Both"/>), and
/// they have no effect on other fields. Set <see cref="TrimGivenChars"/> to trim the given characters from the value.
/// </para>
/// <para>Values are not trimmed on write.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldTrimAttribute : Attribute
{
    /// <summary>The FileHelpers 3.x default trim characters (white space).</summary>
    private static readonly char[] DefaultTrimChars =
    [
        '\t', '\n', '\v', '\f', '\r', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ',
        ' ', ' ', ' ', ' ', ' ', '​', '　', '﻿',
    ];

    /// <summary>The trimming direction (start, end, or both).</summary>
    public TrimMode TrimMode { get; }

    /// <summary>
    /// The characters skipped around the quotes of a quoted field, and trimmed from the value when
    /// <see cref="TrimGivenChars"/> is set. Defaults to the FileHelpers 3.x white-space set.
    /// </summary>
    public char[] TrimChars { get; }

    /// <summary>
    /// When <see langword="true"/>, the characters given to the constructor (the FileHelpers 3.x white-space set when
    /// none were given) are trimmed from the field value per <see cref="TrimMode"/> on read, after quote removal, for
    /// quoted and unquoted values. They replace the white-space trim: white space that is not in the list stops the
    /// trim. Default <see langword="false"/>: FileHelpers 3.x behaviour (see the remarks of the class).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read only: values are never trimmed on write, so read and write are not symmetric. For example, with the quote
    /// character and the space in the list, the value <c>" x "</c> is written as <c>""" x """</c> and reads back as
    /// <c>x</c>.
    /// </para>
    /// <para>Applies to delimited and fixed-length fields.</para>
    /// <para>
    /// For a <see cref="FieldQuotedAttribute"/> field the quote character, when it is in the list, is not skipped
    /// before the opening quote (so the quoted value is still recognised); it is trimmed from the value after
    /// unquoting. A character of the delimiter in the list is trimmed from quoted values too.
    /// </para>
    /// <para>
    /// A value made only of the given characters reads as empty. An empty value on a value-type field without
    /// <see cref="FieldNullValueAttribute"/> throws <see cref="NullValueNotFoundException"/>: for example,
    /// <c>[FieldTrim(TrimMode.Left, '0', TrimGivenChars = true)] int</c> reads <c>0042</c> as 42 but throws on
    /// <c>000</c>. Use <c>int?</c> or <c>[FieldNullValue(0)]</c> when such values occur.
    /// </para>
    /// <para>With <see cref="FileHelpers.TrimMode.None"/> the property has no effect. An empty list trims nothing.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [FieldQuoted('"', QuoteMode.OptionalForRead)]
    /// [FieldTrim(TrimMode.Both, 'x', ' ', TrimGivenChars = true)]
    /// public string Code;   // xxCODExx , "xxCODExx" and  "xx CODE xx"  all read as CODE
    /// </code>
    /// </example>
    public bool TrimGivenChars { get; set; }

    /// <summary>Trims in the specified direction with the FileHelpers 3.x white-space characters.</summary>
    /// <param name="mode">The trimming direction.</param>
    public FieldTrimAttribute(TrimMode mode) : this(mode, (char[])DefaultTrimChars.Clone()) { }

    /// <summary>Trims in the given direction, skipping the specified characters around quotes.</summary>
    /// <param name="mode">The trimming direction.</param>
    /// <param name="chars">
    /// The characters skipped around the quotes of a quoted field (trimmed from the value when
    /// <see cref="TrimGivenChars"/> is set).
    /// </param>
    public FieldTrimAttribute(TrimMode mode, params char[] chars)
    {
        TrimMode = mode;
        TrimChars = chars;
    }
}
