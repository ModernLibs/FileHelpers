namespace ModernLibs.FileHelpers;

/// <summary>
/// Optional extension of <see cref="IRecordDescriptor{T}"/> emitted by the source generator
/// for <c>class</c> record types only.
/// When implemented, <see cref="FileHelperAsyncEngine{T}"/> can activate <c>RecycleRecords</c>
/// mode to reuse a single <typeparamref name="T"/> instance per call — eliminating per-record
/// heap allocations for reference types.
///
/// <para>Plan 03: This interface is defined here so <c>FileHelperAsyncEngine&lt;T&gt;</c> can
/// reference it via a cast (<c>_descriptor as IRecyclableDescriptor&lt;T&gt;</c>). The generator
/// does not yet emit implementations. The cast will return <see langword="null"/> until Plan 04
/// extends the generator.</para>
///
/// <para>Struct types: <c>ParseInto</c> is not emitted for value types because struct assignment
/// copies by value — recycling is meaningless. The engine checks <c>_recyclable != null</c>
/// to activate the path, so struct descriptors (which will not implement this interface)
/// fall through to the normal <c>Parse</c> path.</para>
/// </summary>
/// <typeparam name="T">A reference-type record annotated with
/// <see cref="DelimitedRecordAttribute"/> or <see cref="FixedLengthRecordAttribute"/>.</typeparam>
public interface IRecyclableDescriptor<T> : IRecordDescriptor<T>
{
    /// <summary>
    /// Parses <paramref name="line"/> and assigns all field values to the existing
    /// <paramref name="existing"/> instance rather than constructing a new one.
    /// </summary>
    /// <param name="line">The raw record line (without line terminator).</param>
    /// <param name="existing">The instance to mutate in-place.</param>
    void ParseInto(ReadOnlySpan<char> line, T existing);
}
