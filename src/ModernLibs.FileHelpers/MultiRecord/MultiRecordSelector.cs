namespace ModernLibs.FileHelpers;

/// <summary>Determines whether a physical line belongs to a record type.</summary>
public delegate bool MultiRecordSelector(ReadOnlySpan<char> line);
