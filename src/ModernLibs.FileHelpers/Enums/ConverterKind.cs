// CA1720: Member names intentionally use type names to match FileHelpers 3.x API contract.
// Renaming would break source-compatibility for users migrating from FileHelpers 3.x.
#pragma warning disable CA1720

namespace ModernLibs.FileHelpers;

/// <summary>
/// Selects the built-in converter implementation for a field.
/// Used by <c>FieldConverterAttribute</c> to identify a named built-in type converter.
/// </summary>
public enum ConverterKind
{
    /// <summary>No built-in converter; use a custom converter type overload.</summary>
    None = 0,

    /// <summary>Converts <see cref="System.DateTime"/> fields using a single format string.</summary>
    Date,

    /// <summary>Converts <see cref="System.DateTime"/> fields using multiple candidate format strings.</summary>
    DateMultiFormat,

    /// <summary>Converts <see cref="System.Byte"/> fields.</summary>
    Byte,

    /// <summary>Converts <see cref="System.SByte"/> fields.</summary>
    SByte,

    /// <summary>Converts <see cref="System.Int16"/> (short) fields.</summary>
    Int16,

    /// <summary>Converts <see cref="System.Int32"/> (int) fields.</summary>
    Int32,

    /// <summary>Converts <see cref="System.Int64"/> (long) fields.</summary>
    Int64,

    /// <summary>Converts <see cref="System.UInt16"/> (ushort) fields.</summary>
    UInt16,

    /// <summary>Converts <see cref="System.UInt32"/> (uint) fields.</summary>
    UInt32,

    /// <summary>Converts <see cref="System.UInt64"/> (ulong) fields.</summary>
    UInt64,

    /// <summary>Converts <see cref="System.Decimal"/> fields (culture-sensitive).</summary>
    Decimal,

    /// <summary>Converts <see cref="System.Double"/> fields (culture-sensitive).</summary>
    Double,

    /// <summary>Converts percentage-formatted <see cref="System.Double"/> fields (e.g., "42.5%").</summary>
    PercentDouble,

    /// <summary>Converts <see cref="System.Single"/> (float) fields (culture-sensitive).</summary>
    Single,

    /// <summary>Converts <see cref="System.Boolean"/> fields using "true"/"false" or "1"/"0".</summary>
    Boolean,

    /// <summary>Converts single-character <see cref="System.Char"/> fields.</summary>
    Char,

    /// <summary>Converts <see cref="System.Guid"/> fields using standard Guid format strings.</summary>
    Guid,
}
