using System.Reflection;

namespace ModernLibs.FileHelpers.Tests.Engines.Fixtures;

/// <summary>
/// Which write path an engine chose when it was made (design 5.2): the record writer of an
/// <see cref="IRecordWriterDescriptor{T}"/>, or the legacy <c>Format</c>. The engines keep the choice in a private
/// field (<c>_writerDescriptor</c>, <see langword="null"/> for the legacy path) and have no member that shows it, so
/// the probe reads the field by reflection and throws when it is not there. Used for engines made by the public
/// constructors over the generated descriptors (T23, preflight F14), where no checking descriptor can stand between
/// the engine and the generated code.
/// </summary>
internal static class WritePathProbe
{
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;

    public static bool UsesRecordWriter<T>(FileHelperEngine<T> engine) => WriterDescriptor(engine) is not null;

    public static bool UsesRecordWriter<T>(FileHelperAsyncEngine<T> engine) => WriterDescriptor(engine) is not null;

    /// <summary>The engine inside a <see cref="MultiRecordType{T}"/>.</summary>
    public static bool UsesRecordWriter<T>(MultiRecordType<T> type) => UsesRecordWriter((FileHelperEngine<T>)Field(type, "_engine")!);

    /// <summary>Both the master and the detail engine.</summary>
    public static bool UsesRecordWriter<TMaster, TDetail>(MasterDetailEngine<TMaster, TDetail> engine) =>
        UsesRecordWriter((FileHelperEngine<TMaster>)Field(engine, "_masterEngine")!)
        && UsesRecordWriter((FileHelperEngine<TDetail>)Field(engine, "_detailEngine")!);

    /// <summary>Neither the master nor the detail engine.</summary>
    public static bool UsesLegacyWriter<TMaster, TDetail>(MasterDetailEngine<TMaster, TDetail> engine) =>
        !UsesRecordWriter((FileHelperEngine<TMaster>)Field(engine, "_masterEngine")!)
        && !UsesRecordWriter((FileHelperEngine<TDetail>)Field(engine, "_detailEngine")!);

    private static object? WriterDescriptor(object engine) => Field(engine, "_writerDescriptor");

    private static object? Field(object owner, string name)
    {
        var field = owner.GetType().GetField(name, Instance)
            ?? throw new InvalidOperationException($"{owner.GetType().Name} has no field {name}: update {nameof(WritePathProbe)}.");
        return field.GetValue(owner);
    }
}
