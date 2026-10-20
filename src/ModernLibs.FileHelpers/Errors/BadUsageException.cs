namespace ModernLibs.FileHelpers;

/// <summary>
/// Indicates wrong usage of the library or input that does not match the record layout
/// (for example a missing delimiter, a fixed-length line with the wrong width, or an
/// unclosed quoted field). Matches the FileHelpers 3.x <c>BadUsageException</c>.
/// </summary>
/// <remarks>
/// Constructors are public (3.x used <c>protected internal</c>) because source-generated
/// descriptors live in the consumer assembly and must be able to raise this exception.
/// </remarks>
public class BadUsageException : FileHelpersException
{
    /// <summary>Creates an exception with a default message.</summary>
    public BadUsageException()
    {
    }

    /// <summary>Creates an exception with the given <paramref name="message"/>.</summary>
    public BadUsageException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given <paramref name="message"/> and inner exception.</summary>
    public BadUsageException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception located at <paramref name="line"/>/<paramref name="column"/>.</summary>
    public BadUsageException(int line, int column, string message)
        : base(line, column, message)
    {
    }
}
