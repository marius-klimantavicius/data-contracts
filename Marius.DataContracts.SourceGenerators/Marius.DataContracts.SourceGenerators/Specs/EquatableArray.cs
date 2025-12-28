using System.Collections;

namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// An immutable, equatable array. Useful for storing collections in source generator models.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>?
{
    public static readonly EquatableArray<T> Empty = new EquatableArray<T>(Array.Empty<T>());
    
    private readonly T[]? _array;

    public EquatableArray(T[] array)
    {
        _array = array;
    }

    public EquatableArray(IReadOnlyList<T> list)
    {
        var array = new T[list.Count];
        for (var i = 0; i < list.Count; i++)
            array[i] = list[i];
        _array = array;
    }

    public ReadOnlySpan<T> AsSpan() => _array.AsSpan();
    public T[] AsArray() => _array ?? Array.Empty<T>();
    
    public int Length => _array?.Length ?? 0;
    
    public T this[int index] => (_array ?? Array.Empty<T>())[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array is null && other._array is null)
            return true;
        if (_array is null || other._array is null)
            return false;
        if (_array.Length != other._array.Length)
            return false;

        for (var i = 0; i < _array.Length; i++)
        {
            var a = _array[i];
            var b = other._array[i];
            
            if (a is null && b is null)
                continue;
            if (a is null || b is null)
                return false;
            if (!a.Equals(b))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array is null)
            return 0;

        unchecked
        {
            var hash = 17;
            foreach (var item in _array)
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        var array = _array ?? Array.Empty<T>();
        return ((IEnumerable<T>)array).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public List<T> ToList() => new List<T>(_array ?? []);
}

