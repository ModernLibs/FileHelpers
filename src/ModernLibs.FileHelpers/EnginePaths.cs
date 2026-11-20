using System.Runtime.InteropServices;

namespace ModernLibs.FileHelpers;

/// <summary>
/// The code paths one engine uses (design 4.7). Public constructors pass <see cref="FromSwitches"/>; tests,
/// fuzzing and benchmarks pass their own value through the internal constructors to run both paths in one process.
/// </summary>
/// <param name="LegacyParse">Never call <see cref="ITokenizedDescriptor{T}"/>: read with the 4.9 path.</param>
/// <param name="ScalarTokenizer">The tokenizer uses no vector code of its own (one <c>IndexOf</c> per field).</param>
/// <param name="LegacyWriter">Write with <c>Format(T, IBufferWriter&lt;char&gt;)</c> only (T22).</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EnginePaths(bool LegacyParse, bool ScalarTokenizer, bool LegacyWriter)
{
    /// <summary>The paths the <see cref="RuntimeSwitches"/> select.</summary>
    public static EnginePaths FromSwitches { get; } = new(
        RuntimeSwitches.DisableTokenizedParse,
        RuntimeSwitches.DisableVectorizedTokenizer,
        RuntimeSwitches.DisableRecordWriter);
}
