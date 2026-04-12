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

    /// <summary>Formats a record instance into <paramref name="writer"/>.</summary>
    void Format(T record, IBufferWriter<char> writer);

    /// <summary>Descriptor metadata for each mapped field, in column order.</summary>
    ReadOnlySpan<FieldDescriptor> Fields { get; }
}
