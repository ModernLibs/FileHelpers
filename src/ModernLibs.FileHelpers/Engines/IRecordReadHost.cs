namespace ModernLibs.FileHelpers;

/// <summary>
/// Engine-side hooks used by <see cref="RecordReadSession{T}"/>: the engine's read events and the
/// properties a read populates. Implemented explicitly by the engines so nothing becomes public.
/// </summary>
internal interface IRecordReadHost<T>
{
    /// <summary>Current <c>BeforeReadRecord</c> subscribers (may be <see langword="null"/>).</summary>
    BeforeReadHandler<T>? BeforeReadHandler { get; }

    /// <summary>Current <c>AfterReadRecord</c> subscribers (may be <see langword="null"/>).</summary>
    AfterReadHandler<T>? AfterReadHandler { get; }

    /// <summary>Current <c>Progress</c> subscribers (may be <see langword="null"/>).</summary>
    ProgressChangeHandler? ProgressHandler { get; }

    /// <summary>Called once the <see cref="IgnoreFirstAttribute"/> header lines have been read.</summary>
    void OnHeaderText(string headerText);

    /// <summary>Called at end of input with the <see cref="IgnoreLastAttribute"/> footer lines.</summary>
    void OnFooterText(string footerText);
}
