namespace ModernLibs.FileHelpers;

/// <summary>Container for one master record and its detail records.</summary>
public sealed class MasterDetails<TMaster, TDetail>
{
    /// <summary>Creates a grouped master/detail result.</summary>
    public MasterDetails(TMaster master, IReadOnlyList<TDetail> details)
    {
        Master = master;
        Details = details;
    }

    /// <summary>The master record.</summary>
    public TMaster Master { get; }

    /// <summary>The detail records associated with <see cref="Master"/>.</summary>
    public IReadOnlyList<TDetail> Details { get; }
}
