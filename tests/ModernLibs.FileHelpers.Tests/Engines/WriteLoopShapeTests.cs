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
/// .NET 11 Tier1 inlines it into the loop and then stops inlining the writer call. The multi-record write is the
/// exception (Ruling P7, D2): <c>WriteMultiRecordText</c> has the per-record body itself and calls none of the
/// marked methods.
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
    /// The multi-record write (Ruling P7, D2) does not pay a NoInlining frame per record: <c>WriteMultiRecordText</c>
    /// is not marked, holds the per-record body (it calls <c>FormatRecord</c>, the positive control of the scanner)
    /// and calls no method of <see cref="FileHelperEngine{T}"/> that is marked NoInlining.
    /// </summary>
    [Test]
    public async Task MultiRecord_Write_Method_Calls_No_NoInlining_Engine_Method()
    {
        var type = typeof(FileHelperEngine<RcStruct>);
        var method = type.GetMethod("WriteMultiRecordText", BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining)).IsFalse();

        var called = CalledMethods(method, type.GetGenericArguments());
        await Assert.That(called.Select(m => m.Name)).Contains("FormatRecord");

        var markedEngineMethods = called
            .Where(m => m.DeclaringType is { IsGenericType: true } declaring
                && declaring.GetGenericTypeDefinition() == typeof(FileHelperEngine<>)
                && m.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining))
            .Select(m => m.Name)
            .ToList();
        await Assert.That(markedEngineMethods).IsEmpty();
    }

    /// <summary>
    /// The methods that <paramref name="method"/> calls: the resolved operands of its call and callvirt instructions.
    /// The scanner walks the IL bytes, not the instructions, so an operand byte equal to a call opcode followed by a
    /// resolvable token would add a wrong method. Fine for the short bodies tested here; decode the instructions before
    /// using it on larger methods.
    /// </summary>
    private static List<MethodBase> CalledMethods(MethodInfo method, Type[] typeArguments)
    {
        const byte Call = 0x28;
        const byte CallVirt = 0x6F;
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        var methods = new List<MethodBase>();
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
                    methods.Add(called);
                    i += 4;
                }
            }
            catch (ArgumentException)
            {
                // Not a call token: a byte of another operand that looks like the opcode.
            }
        }

        return methods;
    }
}
