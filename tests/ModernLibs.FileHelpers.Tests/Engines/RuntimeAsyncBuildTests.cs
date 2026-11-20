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

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>T01: the runtime-async switch (FileHelpersRuntimeAsync) shows in the built library.</summary>
[Category("Build")]
public sealed class RuntimeAsyncBuildTests
{
    [Test]
    public async Task ReadFileAsync_HasAsyncImplFlag_OnlyInTheRuntimeAsyncBuild()
    {
        var method = typeof(FileHelperEngine<RcStruct>).GetMethod(
            "ReadFileAsync", [typeof(string), typeof(Action<RcStruct>), typeof(int), typeof(CancellationToken)])!;
        bool hasFlag = ((int)method.MethodImplementationFlags & 0x2000) != 0;
#if NET11_0_OR_GREATER
        bool expected = !string.Equals(
            Environment.GetEnvironmentVariable("FH_EXPECT_RUNTIME_ASYNC"), "0", StringComparison.Ordinal);
#else
        bool expected = false;
#endif
        await Assert.That(hasFlag).IsEqualTo(expected);
    }
}
