namespace ModernLibs.FileHelpers;

/// <summary>Classifies a physical line as master, detail, or skipped.</summary>
public delegate RecordAction MasterDetailSelector(ReadOnlySpan<char> line);
