namespace ModernLibs.FileHelpers;

/// <summary>Action returned by a master/detail selector for a physical line.</summary>
public enum RecordAction
{
    /// <summary>The line starts a new master group.</summary>
    Master = 0,

    /// <summary>The line is a detail of the current master group.</summary>
    Detail = 1,

    /// <summary>The line is ignored.</summary>
    Skip = 2,
}
