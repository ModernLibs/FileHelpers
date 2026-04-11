namespace ModernLibs.FileHelpers;

/// <summary>
/// Specifies the alignment and pad character for a fixed-length field during write.
/// The field value is padded to fill <see cref="FieldFixedLengthAttribute.Length"/> characters.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldAlignAttribute : Attribute
{
    /// <summary>The alignment direction for padding.</summary>
    public AlignMode Align { get; }

    /// <summary>The character used to pad the field to its declared length. Default is a space (<c>' '</c>).</summary>
    public char AlignChar { get; }

    /// <summary>Specifies field alignment using space (<c>' '</c>) as the pad character.</summary>
    /// <param name="align">The alignment direction.</param>
    public FieldAlignAttribute(AlignMode align) : this(align, ' ') { }

    /// <summary>Specifies field alignment and a custom pad character.</summary>
    /// <param name="align">The alignment direction.</param>
    /// <param name="alignChar">The character used for padding.</param>
    public FieldAlignAttribute(AlignMode align, char alignChar)
    {
        Align = align;
        AlignChar = alignChar;
    }
}
