namespace ModernLibs.FileHelpers;

/// <summary>
/// Write-side contract of the built-in <see cref="IMultiRecordType"/> entries: formatting with the engine's
/// <see cref="MultiRecordEngine.NewLineForWrite"/>.
/// </summary>
internal interface IMultiRecordTypeWriter
{
    /// <summary>
    /// Formats one record without a trailing terminator; <paramref name="newLine"/> separates the physical
    /// lines of a record with <see cref="FieldInNewLineAttribute"/> fields.
    /// </summary>
    string Format(object record, string newLine);
}
