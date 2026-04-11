namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a class or struct as a fixed-length record type.
/// Each field must be decorated with <see cref="FieldFixedLengthAttribute"/> specifying its character width.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class FixedLengthRecordAttribute : Attribute
{
    /// <summary>Determines how records with lengths that deviate from the declared total width are handled.</summary>
    public FixedMode FixedMode { get; }

    /// <summary>Marks this type as a fixed-length record using <see cref="FixedMode.ExactLength"/> mode.</summary>
    public FixedLengthRecordAttribute() : this(FixedMode.ExactLength) { }

    /// <summary>Marks this type as a fixed-length record with the specified length mode.</summary>
    /// <param name="fixedMode">The mode controlling how records shorter or longer than declared are handled.</param>
    public FixedLengthRecordAttribute(FixedMode fixedMode)
    {
        FixedMode = fixedMode;
    }
}
