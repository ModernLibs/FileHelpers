using System.Reflection;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// A <see cref="FileHelperEngine{T}"/> that runs a hand-written descriptor instead of the generated one, so that a
/// prototype is measured through exactly the engine code the generated descriptor runs in.
/// </summary>
/// <remarks>
/// The engine takes its descriptor from <see cref="FileHelperRegistry{T}"/>, which holds the generated one and cannot
/// be replaced, and has no constructor that takes a descriptor. This helper builds a normal engine and replaces its
/// two private read-only fields by reflection: <c>_descriptor</c> (the write path calls its <c>Format</c>) and
/// <c>_core</c> (a <c>RecordReadCore&lt;T&gt;</c> built over the prototype; the read path calls its <c>Parse</c>).
/// Everything else, such as the line reader, the multiline assembler and its quote scan, the field-count check,
/// the error handling and the <c>TextWriter</c> pipeline, stays the engine's own. Benchmark code only; when the
/// engine's fields change, the lookup throws instead of measuring the generated descriptor.
/// </remarks>
internal static class PrototypeEngine
{
    public static FileHelperEngine<T> Create<T>(IRecordDescriptor<T> descriptor)
    {
        var engine = new FileHelperEngine<T>();
        Replace(engine, "_descriptor", descriptor);
        Replace(engine, "_core", new RecordReadCore<T>(descriptor));
        ClearWriterDescriptor(engine);
        if (!ReferenceEquals(engine.Core.Descriptor, descriptor))
            throw new InvalidOperationException("PrototypeEngine: the engine does not read through the prototype descriptor.");
        return engine;
    }

    // A 4.10 engine formats through _writerDescriptor (the RecordWriter path) when it is set, and takes _descriptor.Format
    // only when it is null (the 4.9 write path). A prototype has no RecordWriter Format, so the field is cleared: the
    // engine then writes through the prototype. A 4.9.1 build has no such field.
    private static void ClearWriterDescriptor<T>(FileHelperEngine<T> engine)
    {
        var field = typeof(FileHelperEngine<T>).GetField("_writerDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
            return;
        field.SetValue(engine, null);
        if (field.GetValue(engine) is not null)
            throw new InvalidOperationException("PrototypeEngine: field _writerDescriptor was not cleared.");
    }

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
