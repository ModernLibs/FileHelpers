using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace ModernLibs.FileHelpers;

/// <summary>
/// SIMD-accelerated delimiter position finder. Selects optimal vector path at runtime
/// based on CPU features: AVX2 → NEON → Vector{T} fallback → scalar.
/// </summary>
public static class SimdDelimiterFinder
{
    public enum SimdLevel
    {
        None = 0,
        VectorT = 1,
        Neon128 = 2,
        Avx2 = 3,
    }

    public static SimdLevel Level { get; } = DetectLevel();

    private static SimdLevel DetectLevel()
    {
        if (Avx2.IsSupported)
            return SimdLevel.Avx2;
        if (AdvSimd.IsSupported)
            return SimdLevel.Neon128;
        if (System.Numerics.Vector.IsHardwareAccelerated)
            return SimdLevel.VectorT;
        return SimdLevel.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOf(ReadOnlySpan<char> source, char delimiter)
    {
        return Level switch
        {
            SimdLevel.Avx2 => IndexOfAvx2(source, delimiter),
            SimdLevel.Neon128 => IndexOfNeon(source, delimiter),
            SimdLevel.VectorT => IndexOfVectorT(source, delimiter),
            _ => source.IndexOf(delimiter),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfAvx2(ReadOnlySpan<char> source, char delimiter)
    {
        var vec = Vector256.Create((ushort)delimiter);
        int vectorSize = Vector256<ushort>.Count; // 16

        int i = 0;
        int limit = source.Length - vectorSize;

        for (; i <= limit; i += vectorSize)
        {
            var chunk = Unsafe.ReadUnaligned<Vector256<ushort>>(ref Unsafe.As<char, byte>(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(source[i..])));
            var cmp = Avx2.CompareEqual(chunk, vec);
            // CompareEqual returns -1 (all 1s) for equal lanes, 0 otherwise
            // MoveMask on ushort vector: each ushort becomes 1 bit
            int mask = Avx2.MoveMask(cmp.AsByte());
            // Each ushort is 2 bytes, so we get 2 bits per char in the byte mask
            // We need to find which lane matched
            if (mask != 0)
            {
                for (int j = 0; j < vectorSize; j++)
                {
                    if (source[i + j] == delimiter)
                        return i + j;
                }
            }
        }

        int tailIdx = source[i..].IndexOf(delimiter);
        return tailIdx < 0 ? -1 : i + tailIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfNeon(ReadOnlySpan<char> source, char delimiter)
    {
        var vec = Vector128.Create((ushort)delimiter);
        int vectorSize = Vector128<ushort>.Count; // 8

        int i = 0;
        int limit = source.Length - vectorSize;

        for (; i <= limit; i += vectorSize)
        {
            var chunk = Unsafe.ReadUnaligned<Vector128<ushort>>(ref Unsafe.As<char, byte>(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(source[i..])));
            var cmp = AdvSimd.CompareEqual(chunk, vec);
            // Check if any lane matches by testing each position
            for (int j = 0; j < vectorSize; j++)
            {
                if (source[i + j] == delimiter)
                    return i + j;
            }
        }

        int tailIdx = source[i..].IndexOf(delimiter);
        return tailIdx < 0 ? -1 : i + tailIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfVectorT(ReadOnlySpan<char> source, char delimiter)
    {
        int vectorSize = System.Numerics.Vector<char>.Count;
        var vec = new System.Numerics.Vector<char>(delimiter);

        int i = 0;
        int limit = source.Length - vectorSize;

        for (; i <= limit; i += vectorSize)
        {
            var chunk = new System.Numerics.Vector<char>(source[i..]);
            var cmp = System.Numerics.Vector.Equals(chunk, vec);
            if (!System.Numerics.Vector.EqualsAll(cmp, System.Numerics.Vector<char>.Zero))
            {
                for (int j = 0; j < vectorSize; j++)
                {
                    if (source[i + j] == delimiter)
                        return i + j;
                }
            }
        }

        int tailIdx = source[i..].IndexOf(delimiter);
        return tailIdx < 0 ? -1 : i + tailIdx;
    }
}
