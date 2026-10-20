using System.Buffers;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Reflection-free layout, parse and format operations for record type <typeparamref name="T"/>,
/// implemented by the source generator for every <see cref="DelimitedRecordAttribute"/> /
/// <see cref="FixedLengthRecordAttribute"/> type and exposed through <see cref="FileHelperRegistry{T}.Descriptor"/>.
/// </summary>
/// <remarks>
/// Only the source generator implements this interface; user implementations are not supported.
/// Generated code in consumer assemblies implements it, so members added in later 4.x versions
/// always come with a default implementation.
/// </remarks>
/// <typeparam name="T">The annotated record type.</typeparam>
public interface IRecordDescriptor<T>
{
    /// <summary>
    /// Parses a logical record from one or more physical lines.
    /// </summary>
    /// <param name="lines">The physical line(s), without the final line terminator. For multi-line
    /// records (<see cref="FieldInNewLineAttribute"/>) the lines are concatenated with their original
    /// line terminators.</param>
    /// <param name="lineCount">Number of physical lines in this logical record (1 for single-line).</param>
    /// <returns>The parsed record.</returns>
    /// <exception cref="ConvertException">A field value cannot be converted to the field type.</exception>
    /// <exception cref="BadUsageException">The line layout does not match the record (wrong line count,
    /// fixed-length width or field count), or a converter is misconfigured.</exception>
    T Parse(ReadOnlySpan<char> lines, int lineCount);

    /// <summary>
    /// Parses a single-line logical record.
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <returns>The parsed record.</returns>
    T Parse(ReadOnlySpan<char> line) => Parse(line, lineCount: 1);

    /// <summary>
    /// Formats <paramref name="record"/> into <paramref name="writer"/> (without a line terminator).
    /// </summary>
    /// <remarks>
    /// The generated implementation assumes that each <see cref="IBufferWriter{T}.GetSpan(int)"/> call
    /// returns a contiguous span of at least the requested size; writers that return shorter (segmented)
    /// spans are not supported.
    /// </remarks>
    /// <param name="record">The record to format.</param>
    /// <param name="writer">The destination writer.</param>
    void Format(T record, IBufferWriter<char> writer);

    /// <summary>Metadata for each mapped field, in column order.</summary>
    ReadOnlySpan<FieldDescriptor> Fields { get; }

    /// <summary>
    /// Record-level options captured at compile time from the record attributes
    /// (<see cref="IgnoreFirstAttribute"/>, <see cref="IgnoreLastAttribute"/>,
    /// <see cref="IgnoreEmptyLinesAttribute"/>, <see cref="IgnoreCommentedLinesAttribute"/>,
    /// <see cref="ConditionalRecordAttribute"/>, <see cref="DelimitedRecordAttribute"/> settings).
    /// </summary>
    RecordOptions Options { get; }

    /// <summary>
    /// Creates an instance of <typeparamref name="T"/> without parsed values: <c>default(T)</c> for value
    /// types, otherwise a new instance (constructor parameters receive their default values). Used for
    /// <see cref="INotifyRead{T}.BeforeRead"/> and empty records.
    /// </summary>
    /// <returns>The new instance.</returns>
    T CreateDefaultInstance();
}
