using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Optional extension of <see cref="IRecordDescriptor{T}"/> that parses into an existing instance.
/// Infrastructure for source-generated code; not intended for direct use.
/// </summary>
/// <remarks>
/// <para>The source generator implements it for <c>class</c> records whose stored members all have
/// regular setters. When available, engines with <c>RecycleRecords</c> enabled reuse one
/// <typeparamref name="T"/> instance across callback/streaming reads instead of allocating one per record.</para>
/// <para>Value-type and constructor-bound records do not implement it; engines then fall back to
/// <see cref="IRecordDescriptor{T}.Parse(ReadOnlySpan{char}, int)"/>.</para>
/// </remarks>
/// <typeparam name="T">A reference-type record annotated with
/// <see cref="DelimitedRecordAttribute"/> or <see cref="FixedLengthRecordAttribute"/>.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRecyclableDescriptor<T> : IRecordDescriptor<T>
{
    /// <summary>
    /// Parses a logical record and assigns all field values to <paramref name="existing"/>
    /// instead of constructing a new instance.
    /// </summary>
    /// <param name="lines">The physical line(s) of the record. For multi-line records the lines are
    /// concatenated with their original line terminators.</param>
    /// <param name="lineCount">Number of physical lines in this logical record (1 for single-line).</param>
    /// <param name="existing">The instance to overwrite in place.</param>
    void ParseInto(ReadOnlySpan<char> lines, int lineCount, T existing);
}
