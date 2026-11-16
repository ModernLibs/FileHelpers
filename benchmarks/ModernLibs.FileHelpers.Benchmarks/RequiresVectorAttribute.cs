using System.Reflection;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Running;

namespace ModernLibs.FileHelpers.Benchmarks;

/// <summary>
/// A benchmark method that times a vector loop of <see cref="Bits"/> bits (T21). <see cref="BenchConfig"/> leaves the
/// method out, so it is neither listed nor run, when the hardware does not accelerate that width: a machine without
/// AVX-512, or a run with <c>DOTNET_EnableAVX2=0</c>, would otherwise time the software fallback of the vector type.
/// Both builds of an A/B pair run on the same machine, so both leave the method out.
/// </summary>
/// <param name="bits">128, 256 or 512.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class RequiresVectorAttribute(int bits) : Attribute
{
    /// <summary>The vector width the method needs.</summary>
    public int Bits { get; } = bits;

    /// <summary>Whether this process accelerates <see cref="Bits"/>.</summary>
    public bool Accelerated => Bits switch
    {
        128 => Vector128.IsHardwareAccelerated,
        256 => Vector256.IsHardwareAccelerated,
        512 => Vector512.IsHardwareAccelerated,
        _ => throw new InvalidOperationException($"RequiresVector({Bits}): the width must be 128, 256 or 512"),
    };

    /// <summary>The filter of <see cref="BenchConfig"/>: <see langword="false"/> for a method whose width is not accelerated here.</summary>
    /// <param name="benchmark">A benchmark case.</param>
    /// <returns>Whether the case runs on this machine.</returns>
    public static bool Runs(BenchmarkCase benchmark) =>
        benchmark.Descriptor.WorkloadMethod.GetCustomAttribute<RequiresVectorAttribute>() is not { Accelerated: false };
}
