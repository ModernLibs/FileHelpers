// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Tests.Engines.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// The default loops of <see cref="RecordTokenizer"/> (T21 step 2): <see cref="RecordTokenizer.PlainLoop"/> and
/// <see cref="RecordTokenizer.QuotedLoop"/> are the values of the default table for this machine, and an engine with
/// <see cref="EnginePaths.ScalarTokenizer"/> runs <see cref="TokenizerLoop.IndexOfHop"/>. The table is written here,
/// not taken from the selection code (ruling F24). The process with the switch set is the fuzz target
/// <c>paths-switch</c>.
/// </summary>
[Category("Parsing")]
public sealed class TokenizerSelectionTests
{
    private static readonly EnginePaths DefaultPaths = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths ScalarPaths = new(LegacyParse: false, ScalarTokenizer: true, LegacyWriter: false);

    private static bool X64 => RuntimeInformation.ProcessArchitecture == Architecture.X64;

    /// <summary>
    /// The default table of 4.10.0, as decided by rounds simd-2026-11-17 and noavx2-2026-11-17 (T21 step 2, branch B: the owner waived Arm64 on 2026-11-16; ruling
    /// F24 adds the row without <c>Vector128</c>). The first row whose condition holds on this machine applies; a
    /// Native AOT process takes the row of its architecture.
    /// </summary>
    private static readonly (string Case, Func<bool> Applies, TokenizerLoop Plain, TokenizerLoop Quoted)[] DefaultTable =
    [
        ("DisableVectorizedTokenizer set", () => RuntimeSwitches.DisableVectorizedTokenizer, TokenizerLoop.IndexOfHop, TokenizerLoop.IndexOfHop),
        ("no Vector128.IsHardwareAccelerated", () => !Vector128.IsHardwareAccelerated, TokenizerLoop.IndexOfHop, TokenizerLoop.IndexOfHop),
        ("Arm64, branch B (owner waiver of 2026-11-16)", () => RuntimeInformation.ProcessArchitecture == Architecture.Arm64, TokenizerLoop.IndexOfHop, TokenizerLoop.IndexOfHop),
        ("x64 with AVX-512 (Vector512 gate passed, deferred by the owner 2026-11-21)", () => X64 && Vector512.IsHardwareAccelerated, TokenizerLoop.Vector256, TokenizerLoop.Vector256),
        ("x64, Vector256.IsHardwareAccelerated", () => X64 && Vector256.IsHardwareAccelerated, TokenizerLoop.Vector256, TokenizerLoop.Vector256),
        ("x64 without AVX2", () => X64, TokenizerLoop.Vector128, TokenizerLoop.Vector128),
        ("another architecture (not measured)", () => true, TokenizerLoop.IndexOfHop, TokenizerLoop.IndexOfHop),
    ];

    private static (string Case, Func<bool> Applies, TokenizerLoop Plain, TokenizerLoop Quoted) ThisMachine() =>
        DefaultTable.First(row => row.Applies());

    [Test]
    public async Task Default_Loops_Are_The_Table_Row_Of_This_Machine()
    {
        var row = ThisMachine();
        string because = $"row '{row.Case}' ({RuntimeInformation.ProcessArchitecture}, .NET {Environment.Version.Major})";
        await Assert.That(RecordTokenizer.PlainLoop).IsEqualTo(row.Plain).Because(because);
        await Assert.That(RecordTokenizer.QuotedLoop).IsEqualTo(row.Quoted).Because(because);
    }

    /// <summary>Bounds-checked span loads on .NET 10 and .NET 11 because round simd-2026-11-17 found no dataset on which both VMs agree that <c>LoadUnsafe</c> is faster (freeze F-8, F5, ruling P41).</summary>
    [Test]
    public async Task Span_Loads_Before_The_Round()
    {
        await Assert.That(RecordTokenizer.UseUnsafeLoad).IsFalse().Because($".NET {Environment.Version.Major}");
    }

    [Test]
    public async Task The_Bool_Overloads_Run_The_Default_Loops_Or_IndexOfHop()
    {
        await Assert.That(RecordTokenizer.PlainLoopFor(vectors: true)).IsEqualTo(RecordTokenizer.PlainLoop);
        await Assert.That(RecordTokenizer.QuotedLoopFor(vectors: true)).IsEqualTo(RecordTokenizer.QuotedLoop);
        await Assert.That(RecordTokenizer.PlainLoopFor(vectors: false)).IsEqualTo(TokenizerLoop.IndexOfHop);
        await Assert.That(RecordTokenizer.QuotedLoopFor(vectors: false)).IsEqualTo(TokenizerLoop.IndexOfHop);
    }

    /// <summary>
    /// The loops an engine's core passes to the tokenizer (<c>RecordReadCore.TokenizerLoops</c>, mapped from its
    /// <see cref="EnginePaths.ScalarTokenizer"/> flag): <see cref="TokenizerLoop.IndexOfHop"/> for both with the flag,
    /// the defaults without it. It checks the selection the reads use, not a loop observed during a read.
    /// </summary>
    [Test]
    public async Task ScalarTokenizer_Engine_Selects_IndexOfHop()
    {
        var scalar = new FileHelperEngine<TokPlain>(new TokPlainDescriptor(), Encoding.UTF8, ScalarPaths);
        var vector = new FileHelperEngine<TokPlain>(new TokPlainDescriptor(), Encoding.UTF8, DefaultPaths);

        await Assert.That(scalar.Core.Tokenized).IsTrue();
        await Assert.That(scalar.Core.TokenizerLoops).IsEqualTo((TokenizerLoop.IndexOfHop, TokenizerLoop.IndexOfHop));
        await Assert.That(vector.Core.TokenizerLoops).IsEqualTo((RecordTokenizer.PlainLoop, RecordTokenizer.QuotedLoop));
    }
}
