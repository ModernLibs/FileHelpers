using System;
using System.Collections.Immutable;

namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Value-equality wrapper around <see cref="ImmutableArray{T}"/>.
/// Required for correct incremental pipeline caching: ImmutableArray uses reference equality,
/// which causes cache misses on every compilation even when contents are identical.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array) => _array = array;

    public ImmutableArray<T> AsImmutableArray() => _array;

    public int Length => _array.IsDefaultOrEmpty ? 0 : _array.Length;

    public T this[int index] => _array[index];

    // A default (uninitialised) array enumerates as empty instead of throwing NullReferenceException.
    public ImmutableArray<T>.Enumerator GetEnumerator() => (_array.IsDefault ? ImmutableArray<T>.Empty : _array).GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.IsDefaultOrEmpty && other._array.IsDefaultOrEmpty) return true;
        if (_array.IsDefaultOrEmpty || other._array.IsDefaultOrEmpty) return false;
        if (_array.Length != other._array.Length) return false;
        for (int i = 0; i < _array.Length; i++)
        {
            if (!_array[i].Equals(other._array[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) =>
        obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array.IsDefaultOrEmpty) return 0;
        // System.HashCode is netstandard2.1+ only; generator targets netstandard2.0.
        // Use a classic prime-multiply accumulator instead.
        unchecked
        {
            int h = 17;
            foreach (var item in _array)
                h = h * 31 + (item?.GetHashCode() ?? 0);
            return h;
        }
    }
}
