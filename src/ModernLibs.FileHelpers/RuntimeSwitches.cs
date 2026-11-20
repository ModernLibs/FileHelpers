namespace ModernLibs.FileHelpers;

/// <summary>
/// The <see cref="AppContext"/> switches of 4.10 (design 4.7): a safety valve back to the 4.9 code paths. Each is
/// read once, when this class is first used, so an application sets it before the first engine is created: in
/// <c>runtimeconfig.json</c>, with <c>&lt;RuntimeHostConfigurationOption&gt;</c>, or with
/// <see cref="AppContext.SetSwitch"/> at the start of <c>Main</c>. They are not trimmer feature switches: the legacy
/// paths stay in a trimmed or AOT application. Tests choose the paths with <see cref="EnginePaths"/> instead.
/// </summary>
internal static class RuntimeSwitches
{
    /// <summary><c>ModernLibs.FileHelpers.DisableTokenizedParse</c>: the runtime never calls <see cref="ITokenizedDescriptor{T}"/>.</summary>
    public static readonly bool DisableTokenizedParse = IsOn("ModernLibs.FileHelpers.DisableTokenizedParse");

    /// <summary>
    /// <c>ModernLibs.FileHelpers.DisableVectorizedTokenizer</c>: the tokenizer uses none of its own vector code; it
    /// finds delimiters and quotes with <c>IndexOf</c> and <c>IndexOfAny</c>, which the BCL may vectorize.
    /// </summary>
    public static readonly bool DisableVectorizedTokenizer = IsOn("ModernLibs.FileHelpers.DisableVectorizedTokenizer");

    /// <summary><c>ModernLibs.FileHelpers.DisableRecordWriter</c>: the runtime never calls the record writer of generated code (T22).</summary>
    public static readonly bool DisableRecordWriter = IsOn("ModernLibs.FileHelpers.DisableRecordWriter");

    private static bool IsOn(string name) => AppContext.TryGetSwitch(name, out bool on) && on;
}
