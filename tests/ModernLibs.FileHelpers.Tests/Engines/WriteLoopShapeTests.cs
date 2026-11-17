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
    [Arguments("WriteRecordInPlace")]
    public async Task PerRecord_Write_Method_Is_Not_Inlined(string name)
    {
        var method = typeof(FileHelperEngine<RcStruct>).GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining)).IsTrue();
    }

    /// <summary>
    /// The multi-record write (Ruling P7, D2) is not a write loop of the engine: <c>WriteMultiRecordText</c> has the
    /// per-record body itself and stays inlinable, so it does not pay the call to the marked <c>WriteRecordText</c>.
    /// </summary>
    [Test]
    public async Task MultiRecord_Write_Method_Does_Not_Call_The_Marked_Method()
    {
        var type = typeof(FileHelperEngine<RcStruct>);
        var method = type.GetMethod("WriteMultiRecordText", BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining)).IsFalse();
        await Assert.That(CalledMethodNames(method, type.GetGenericArguments())).Contains("FormatRecord");
        await Assert.That(CalledMethodNames(method, type.GetGenericArguments())).DoesNotContain("WriteRecordText");
    }

    /// <summary>The names of the methods that <paramref name="method"/> calls (call and callvirt operands of its IL).</summary>
    private static List<string> CalledMethodNames(MethodInfo method, Type[] typeArguments)
    {
        const byte Call = 0x28;
        const byte CallVirt = 0x6F;
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        var names = new List<string>();
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != Call && il[i] != CallVirt)
                continue;

            int token = BitConverter.ToInt32(il, i + 1);
            int table = (int)((uint)token >> 24);
            if (table is not (0x06 or 0x0A or 0x2B))
                continue;

            try
            {
                var called = method.Module.ResolveMethod(token, typeArguments, null);
                if (called is not null)
                {
                    names.Add(called.Name);
                    i += 4;
                }
            }
            catch (ArgumentException)
            {
                // Not a call token: a byte of another operand that looks like the opcode.
            }
        }

        return names;
    }
}
