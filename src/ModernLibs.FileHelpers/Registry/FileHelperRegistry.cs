// CA1000: Static members on generic types are intentional here — FileHelperRegistry<T> is the
// per-type static slot pattern. Each instantiation FileHelperRegistry<Trade>, FileHelperRegistry<Order>,
// etc. holds an independent slot. This is the AOT-safe alternative to a ConcurrentDictionary<Type, ...>.
#pragma warning disable CA1000

namespace ModernLibs.FileHelpers;

/// <summary>
/// Per-type static slot holding the source-generated <see cref="IRecordDescriptor{T}"/> for <typeparamref name="T"/>.
/// Populated exclusively by generated <c>[ModuleInitializer]</c> code before any user code runs.
/// </summary>
/// <typeparam name="T">The annotated record type.</typeparam>
public static class FileHelperRegistry<T>
{
    private static IRecordDescriptor<T>? s_descriptor;

    /// <summary>
    /// Gets the registered descriptor for <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor has been registered — indicates the source generator did not run
    /// or the type is not annotated with <c>[DelimitedRecord]</c> or <c>[FixedLengthRecord]</c>.
    /// </exception>
    public static IRecordDescriptor<T> Descriptor =>
        s_descriptor
        ?? throw new InvalidOperationException(
            $"No IRecordDescriptor<{typeof(T).Name}> registered. " +
            "Ensure the type is annotated with [DelimitedRecord] or [FixedLengthRecord] " +
            "and the source generator ran.");

    /// <summary>
    /// Registers the descriptor. Called exclusively by source-generated <c>[ModuleInitializer]</c> methods.
    /// Write-once: throws if called a second time — double-registration indicates a code-gen bug.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a descriptor for <typeparamref name="T"/> is already registered.
    /// </exception>
    public static void SetDescriptor(IRecordDescriptor<T> descriptor)
    {
        if (s_descriptor is not null)
            throw new InvalidOperationException(
                $"A descriptor for {typeof(T).Name} is already registered. " +
                "SetDescriptor must only be called once per type (source-generator bug if hit at runtime).");
        s_descriptor = descriptor;
    }
}
