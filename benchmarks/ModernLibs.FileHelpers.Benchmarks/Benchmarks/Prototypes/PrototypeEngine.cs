using System.Reflection;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// A <see cref="FileHelperEngine{T}"/> that runs a hand-written descriptor instead of the generated one, so that a
/// prototype is measured through exactly the engine code the generated descriptor runs in.
/// </summary>
/// <remarks>
/// The engine takes its descriptor from <see cref="FileHelperRegistry{T}"/>, which holds the generated one and cannot
/// be replaced. A 4.10 build uses the internal constructor that takes a descriptor. A baseline build (4.9.1 package or
/// source) has no such constructor: the helper builds a normal engine and replaces its two private read-only fields by
/// reflection: <c>_descriptor</c> (the write path calls its <c>Format</c>) and
/// <c>_core</c> (a <c>RecordReadCore&lt;T&gt;</c> built over the prototype; the read path calls its <c>Parse</c>).
/// A baseline build of a 4.10 commit also has <c>_writerDescriptor</c>, which is replaced too.
/// Everything else, such as the line reader, the multiline assembler and its quote scan, the field-count check,
/// the error handling and the <c>TextWriter</c> pipeline, stays the engine's own. Benchmark code only; when the
/// engine's fields change, the lookup throws instead of measuring the generated descriptor.
/// </remarks>
internal static class PrototypeEngine
{
    public static FileHelperEngine<T> Create<T>(IRecordDescriptor<T> descriptor)
    {
#if !FILEHELPERS_BASELINE
        // The internal constructor of tests and benchmarks (design 4.7): a descriptor that has no record writer
        // (IRecordWriterDescriptor) leaves the writer descriptor null, so the engine writes through descriptor.Format.
        var engine = new FileHelperEngine<T>(descriptor, System.Text.Encoding.UTF8, EnginePaths.FromSwitches);
#else
        // The 4.9.1 builds have no constructor that takes a descriptor and no writer descriptor.
        var engine = new FileHelperEngine<T>();
        Replace(engine, "_descriptor", descriptor);
        Replace(engine, "_core", new RecordReadCore<T>(descriptor));
        ReplaceWriterDescriptor(engine, descriptor);
#endif
        if (!ReferenceEquals(engine.Core.Descriptor, descriptor))
            throw new InvalidOperationException("PrototypeEngine: the engine does not read through the prototype descriptor.");
        return engine;
    }

#if FILEHELPERS_BASELINE
    /// <summary>
    /// A baseline build of a 4.10 commit (<c>round.sh --base-commit</c>) has a third field: <c>_writerDescriptor</c>,
    /// the generated descriptor as a record writer, which every write calls instead of <c>_descriptor.Format</c>. It
    /// becomes the prototype when the prototype is a record writer, otherwise <see langword="null"/>, as the internal
    /// constructor sets it. The 4.9.1 builds have no such field and nothing is done.
    /// </summary>
    private static void ReplaceWriterDescriptor<T>(FileHelperEngine<T> engine, IRecordDescriptor<T> descriptor)
    {
        var field = typeof(FileHelperEngine<T>).GetField("_writerDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
            return;

        object? writer = field.FieldType.IsInstanceOfType(descriptor) ? descriptor : null;
        field.SetValue(engine, writer);
        if (!ReferenceEquals(field.GetValue(engine), writer))
            throw new InvalidOperationException("PrototypeEngine: field '_writerDescriptor' was not replaced.");
    }
#endif

    private static void Replace<T>(FileHelperEngine<T> engine, string name, object value)
    {
        var field = typeof(FileHelperEngine<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"PrototypeEngine: FileHelperEngine<T> has no field '{name}' any more.");
        if (!field.FieldType.IsInstanceOfType(value))
            throw new InvalidOperationException($"PrototypeEngine: field '{name}' is a {field.FieldType}, not a {value.GetType()}.");
        field.SetValue(engine, value);
        if (!ReferenceEquals(field.GetValue(engine), value))
            throw new InvalidOperationException($"PrototypeEngine: field '{name}' was not replaced.");
    }
}
