// MA0048: Multiple delegates intentionally grouped for event API surface.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers;

public delegate void BeforeReadHandler<T>(object sender, BeforeReadEventArgs<T> e);
public delegate void AfterReadHandler<T>(object sender, AfterReadEventArgs<T> e);
public delegate void BeforeWriteHandler<T>(object sender, BeforeWriteEventArgs<T> e);
public delegate void AfterWriteHandler<T>(object sender, AfterWriteEventArgs<T> e);
public delegate void ProgressChangeHandler(object sender, ProgressEventArgs e);
