namespace ModernLibs.FileHelpers;

/// <summary>
/// Write-side contract of the built-in <see cref="IMultiRecordType"/> entries: formatting with the engine's
/// <see cref="MultiRecordEngine.NewLineForWrite"/>.
/// </summary>
internal interface IMultiRecordTypeWriter
{
    /// <summary>
    /// Writes one record without a trailing terminator to <paramref name="writer"/>; <paramref name="newLine"/>
    /// separates the physical lines of a record with <see cref="FieldInNewLineAttribute"/> fields.
    /// <paramref name="buffer"/> is the formatting buffer shared by the records of one write.
    /// </summary>
    void Write(object record, TextWriter writer, string newLine, RentedBufferWriter buffer);
}
