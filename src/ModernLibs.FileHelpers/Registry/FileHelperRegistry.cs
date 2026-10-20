// CA1000: Static members on generic types are intentional here — FileHelperRegistry<T> is the
// per-type static slot pattern. Each instantiation FileHelperRegistry<Trade>, FileHelperRegistry<Order>,
// etc. holds an independent slot. This is the AOT-safe alternative to a ConcurrentDictionary<Type, ...>.
#pragma warning disable CA1000

using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Per-type static slot holding the source-generated <see cref="IRecordDescriptor{T}"/> for <typeparamref name="T"/>.
/// Populated by generated <c>[ModuleInitializer]</c> code when the assembly that declares
/// <typeparamref name="T"/> is loaded.
/// </summary>
/// <typeparam name="T">The annotated record type.</typeparam>
public static class FileHelperRegistry<T>
{
    private static IRecordDescriptor<T>? s_descriptor;

    /// <summary>
    /// Gets the registered descriptor for <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor has been registered: the source generator did not generate one for the type.
    /// The message lists the likely causes and links the diagnostics reference.
    /// </exception>
    public static IRecordDescriptor<T> Descriptor =>
        Volatile.Read(ref s_descriptor) ?? throw MissingDescriptor();

    private static InvalidOperationException MissingDescriptor() =>
        new($"No IRecordDescriptor<{typeof(T).Name}> registered for '{typeof(T).FullName ?? typeof(T).Name}': " +
            "the FileHelpers source generator did not generate a descriptor for it. Likely causes: the type has no " +
            "[DelimitedRecord] or [FixedLengthRecord] attribute (MLFH001); generated code cannot reference it, for " +
            "example a private or protected nested type (MLFH017) or a generic type (MLFH016); the project uses " +
            "C# 10 or earlier (MLFH026); the type is declared by another source generator; or the project does not " +
            "use the generator (a project reference to the library must also reference the generator as an analyzer). " +
            "The MLFH errors are reported by the analyzer, so check the build output; RunAnalyzers=false hides them. " +
            "See https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md");

    /// <summary>
    /// Registers the descriptor for <typeparamref name="T"/>. Infrastructure for source-generated code;
    /// not intended for direct use.
    /// </summary>
    /// <remarks>
    /// Write-once: the first registration wins and every later call throws, so a registered descriptor
    /// can never be replaced. The generated module initializer of the assembly declaring
    /// <typeparamref name="T"/> makes that first call.
    /// </remarks>
    /// <param name="descriptor">The generated descriptor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a descriptor for <typeparamref name="T"/> is already registered.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetDescriptor(IRecordDescriptor<T> descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (Interlocked.CompareExchange(ref s_descriptor, descriptor, null) is not null)
            throw new InvalidOperationException(
                $"A descriptor for {typeof(T).Name} is already registered. " +
                "SetDescriptor must only be called once per type (source-generator bug if hit at runtime).");
    }
}
