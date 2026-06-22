using System;
using System.Collections;
using System.Collections.Generic;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// An immutable, value-equatable array. Used in incremental generator models
/// to ensure correct caching by the Roslyn incremental generator pipeline.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[] _array;

    public EquatableArray(T[] array)
    {
        _array = array ?? Array.Empty<T>();
    }

    public int Count => _array?.Length ?? 0;

    public T this[int index] => _array![index];

    public bool Equals(EquatableArray<T> other)
    {
        var left = _array ?? Array.Empty<T>();
        var right = other._array ?? Array.Empty<T>();

        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!left[i].Equals(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        // Manual hash combine to avoid HashCode (not available on netstandard2.0).
        var hash = 0;
        foreach (var item in _array ?? Array.Empty<T>())
        {
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);
        }

        return hash;
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public Enumerator GetEnumerator() => new Enumerator(_array ?? Array.Empty<T>());

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (_array ?? Array.Empty<T>()).GetEnumerator();

    public struct Enumerator
    {
        private readonly T[] _array;
        private int _index;

        internal Enumerator(T[] array)
        {
            _array = array;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _array.Length;
        }

        public readonly T Current => _array[_index];
    }
}
