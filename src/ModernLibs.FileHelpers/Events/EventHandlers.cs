// MA0048: Multiple delegates intentionally grouped for event API surface.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers;

/// <summary>Handles the <c>BeforeReadRecord</c> event of an engine.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="sender">The engine raising the event.</param>
/// <param name="e">The event data; set its flags or <see cref="BeforeReadEventArgs{T}.RecordLine"/> to influence the read.</param>
public delegate void BeforeReadHandler<T>(object sender, BeforeReadEventArgs<T> e);

/// <summary>Handles the <c>AfterReadRecord</c> event of an engine.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="sender">The engine raising the event.</param>
/// <param name="e">The event data, including the parsed record.</param>
public delegate void AfterReadHandler<T>(object sender, AfterReadEventArgs<T> e);

/// <summary>Handles the <c>BeforeWriteRecord</c> event of an engine.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="sender">The engine raising the event.</param>
/// <param name="e">The event data, including the record about to be written.</param>
public delegate void BeforeWriteHandler<T>(object sender, BeforeWriteEventArgs<T> e);

/// <summary>Handles the <c>AfterWriteRecord</c> event of an engine.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="sender">The engine raising the event.</param>
/// <param name="e">The event data, including the formatted text that will be written.</param>
public delegate void AfterWriteHandler<T>(object sender, AfterWriteEventArgs<T> e);

/// <summary>Handles the <c>Progress</c> event of an engine.</summary>
/// <param name="sender">The engine raising the event.</param>
/// <param name="e">The progress data.</param>
public delegate void ProgressChangeHandler(object sender, ProgressEventArgs e);
