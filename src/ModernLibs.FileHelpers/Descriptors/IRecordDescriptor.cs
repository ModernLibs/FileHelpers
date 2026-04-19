using System.Buffers;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Contract implemented by source-generated descriptors for record type <typeparamref name="T"/>.
/// Provides span-based parse and format operations without reflection.
/// </summary>
/// <typeparam name="T">The annotated record type.</typeparam>
public interface IRecordDescriptor<T>
{
    /// <summary>Parses a single record line into an instance of <typeparamref name="T"/>.</summary>
    T Parse(ReadOnlySpan<char> line);

    /// <summary>
    /// Formats a record instance into <paramref name="writer"/>.
    /// <para>IN-03 remark: the source-generated implementation assumes each
    /// <see cref="IBufferWriter{T}.GetSpan(int)"/> call returns a contiguous span of at
    /// least the requested size (no mid-field flush/chunk). <c>IBufferWriter&lt;T&gt;</c>
    /// contractually allows shorter spans, so custom implementations passed to the engine
    /// must honor the <c>sizeHint</c> exactly — segmented writers are not supported here.
    /// <c>RentedBufferWriter</c> (the engine-internal default) always satisfies this invariant.</para>
    /// </summary>
    void Format(T record, IBufferWriter<char> writer);

    /// <summary>Descriptor metadata for each mapped field, in column order.</summary>
    ReadOnlySpan<FieldDescriptor> Fields { get; }

    /// <summary>
    /// Compile-time filtering and delimiter options captured from record-level attributes.
    /// D-07: surfaced so the engine can honor <see cref="IgnoreFirstAttribute"/>,
    /// <see cref="IgnoreLastAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>, and
    /// <see cref="IgnoreCommentedLinesAttribute"/> without reflection.
    /// </summary>
    RecordOptions Options { get; }
}
