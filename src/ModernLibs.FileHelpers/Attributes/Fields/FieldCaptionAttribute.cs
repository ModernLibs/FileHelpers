namespace ModernLibs.FileHelpers;

/// <summary>
/// Overrides the display name used for this field in header generation.
/// When present, the caption is used instead of the field/property name.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldCaptionAttribute : Attribute
{
    /// <summary>The display caption for this field.</summary>
    public string Caption { get; }

    /// <summary>Specifies the display caption for this field.</summary>
    /// <param name="caption">The caption as it should appear in headers.</param>
    public FieldCaptionAttribute(string caption)
    {
        ArgumentNullException.ThrowIfNull(caption);
        if (caption.Length == 0)
            throw new ArgumentException("Caption cannot be empty.", nameof(caption));
        Caption = caption;
    }
}
