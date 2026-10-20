using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>Shared helpers for the FileHelpers 3.x converter parity tests.</summary>
internal static class ParityHelpers
{
    /// <summary>Formats <paramref name="value"/> with <paramref name="converter"/>; returns null when TryFormat fails.</summary>
    public static string? Format<T>(ISpanConverter<T> converter, T value)
    {
        Span<char> buffer = stackalloc char[256];
        return converter.TryFormat(value, buffer, out int written) ? new string(buffer[..written]) : null;
    }

    /// <summary>Parses <paramref name="text"/>; returns (success, value).</summary>
    public static (bool Ok, T Value) Parse<T>(ISpanConverter<T> converter, string text)
    {
        bool ok = converter.TryParse(text, out T value);
        return (ok, value);
    }
}
