namespace ModernLibs.FileHelpers;

/// <summary>
/// Raised when a non-nullable value-type field is empty and has no <see cref="FieldNullValueAttribute"/>.
/// Matches the FileHelpers 3.x <c>NullValueNotFoundException</c>.
/// </summary>
public sealed class NullValueNotFoundException : BadUsageException
{
    /// <summary>Creates an exception with a default message.</summary>
    public NullValueNotFoundException()
    {
    }

    /// <summary>Creates an exception with the given <paramref name="message"/>.</summary>
    public NullValueNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given <paramref name="message"/> and inner exception.</summary>
    public NullValueNotFoundException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception located at <paramref name="line"/>/<paramref name="column"/>.</summary>
    public NullValueNotFoundException(int line, int column, string message)
        : base(line, column, message)
    {
    }
}
