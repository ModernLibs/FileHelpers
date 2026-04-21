namespace ModernLibs.FileHelpers;

/// <summary>Defines how the engine handles records that fail to parse.</summary>
public enum ErrorMode
{
    /// <summary>Rethrows the parse exception immediately (default).</summary>
    ThrowException = 0,

    /// <summary>Discards the failed line and continues parsing.</summary>
    IgnoreAndContinue = 1,

    /// <summary>Captures the failed line in <see cref="ErrorManager.Errors"/> and continues parsing.</summary>
    SaveAndContinue = 2,
}
