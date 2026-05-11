namespace ModernLibs.FileHelpers;

/// <summary>
/// Callback delegate for ref struct record parsing. Used by callback-based read methods
/// to pass parsed records by reference, avoiding heap allocation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Important:</strong> The callback receives a reference to the record. If the
/// caller needs to retain record data, it must copy the values before the next callback
/// invocation, as the same reference may be reused for subsequent records.
/// </para>
/// </remarks>
/// <typeparam name="T">The ref struct record type.</typeparam>
/// <param name="record">The parsed record, passed by reference.</param>
public delegate void SpanRecordCallback<T>(ref T record);
