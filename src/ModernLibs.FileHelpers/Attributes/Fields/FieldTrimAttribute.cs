namespace ModernLibs.FileHelpers;

/// <summary>
/// Trims the field after reading (FileHelpers 3.x semantics).
/// </summary>
/// <remarks>
/// The field value is trimmed of white space (<see cref="string.Trim()"/> rules) at the start, the end or
/// both, according to <see cref="TrimMode"/>. For a <see cref="FieldQuotedAttribute"/> field in a delimited
/// record, <see cref="TrimChars"/> are also skipped before the opening quote (<see cref="FileHelpers.TrimMode.Left"/>
/// / <see cref="FileHelpers.TrimMode.Both"/>) and after the closing quote (<see cref="FileHelpers.TrimMode.Right"/>
/// / <see cref="FileHelpers.TrimMode.Both"/>). Values are not trimmed on write.
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
    /// The characters skipped around the quotes of a quoted field. Defaults to the FileHelpers 3.x white-space set.
    /// </summary>
    public char[] TrimChars { get; }

    /// <summary>Trims in the specified direction with the FileHelpers 3.x white-space characters.</summary>
    /// <param name="mode">The trimming direction.</param>
    public FieldTrimAttribute(TrimMode mode) : this(mode, (char[])DefaultTrimChars.Clone()) { }

    /// <summary>Trims in the given direction, skipping the specified characters around quotes.</summary>
    /// <param name="mode">The trimming direction.</param>
    /// <param name="chars">The characters skipped around the quotes of a quoted field.</param>
    public FieldTrimAttribute(TrimMode mode, params char[] chars)
    {
        TrimMode = mode;
        TrimChars = chars;
    }
}
