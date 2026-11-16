// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Reflection;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The per-record write method of the synchronous loops is not inlined into them (dotnet/runtime#137552):
/// .NET 11 Tier1 inlines it into the loop and then stops inlining the writer call.
/// </summary>
[Category("Engines")]
public sealed class WriteLoopShapeTests
{
    [Test]
    [Arguments("WriteRecordText")]
    [Arguments("AppendPendingRecord")]
    public async Task PerRecord_Write_Method_Is_Not_Inlined(string name)
    {
        var method = typeof(FileHelperEngine<RcStruct>).GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining)).IsTrue();
    }
}
