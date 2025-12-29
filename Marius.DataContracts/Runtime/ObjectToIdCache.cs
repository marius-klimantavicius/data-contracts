// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Marius.DataContracts.Runtime;

public sealed class ObjectToIdCache
{
    private int _currentCount;
    private int[] _ids;
    private object?[] _objs;
    private bool[] _isWrapped;

    public ObjectToIdCache()
    {
        _currentCount = 1;
        _ids = new int[GetPrime(1)];
        _objs = new object[_ids.Length];
        _isWrapped = new bool[_ids.Length];
    }

    public int GetId(object obj, ref bool newId)
    {
        var position = FindElement(obj, out var isEmpty, out var isWrapped);
        if (!isEmpty)
        {
            newId = false;
            return _ids[position];
        }

        if (!newId)
            return -1;

        var id = _currentCount++;
        _objs[position] = obj;
        _ids[position] = id;
        _isWrapped[position] = isWrapped;
        if (_currentCount >= _objs.Length - 1)
            Rehash();
        return id;
    }

    // (oldObjId, oldObj-id, newObj-newObjId) => (oldObj-oldObjId, newObj-id, newObjId )
    public int ReassignId(int oldObjId, object oldObj, object newObj)
    {
        var position = FindElement(oldObj, out var isEmpty, out _);
        if (isEmpty)
            return 0;

        var id = _ids[position];
        if (oldObjId > 0)
            _ids[position] = oldObjId;
        else
            RemoveAt(position);

        position = FindElement(newObj, out isEmpty, out var isWrapped);

        var newObjId = 0;
        if (!isEmpty)
            newObjId = _ids[position];

        _objs[position] = newObj;
        _ids[position] = id;
        _isWrapped[position] = isWrapped;
        return newObjId;
    }

    private int FindElement(object obj, out bool isEmpty, out bool isWrapped)
    {
        isWrapped = false;
        var position = ComputeStartPosition(obj);
        for (var i = position; i != position - 1; i++)
        {
            if (_objs[i] == null)
            {
                isEmpty = true;
                return i;
            }

            if (_objs[i] == obj)
            {
                isEmpty = false;
                return i;
            }

            if (i == _objs.Length - 1)
            {
                isWrapped = true;
                i = -1;
            }
        }

        // m_obj must ALWAYS have at least one slot empty (null).
        Debug.Fail("Object table overflow");
        throw XmlObjectSerializer.CreateSerializationException(SR.ObjectTableOverflow);
    }

    private void RemoveAt(int position)
    {
        var cacheSize = _objs.Length;
        var lastVacantPosition = position;
        for (var next = position == cacheSize - 1 ? 0 : position + 1; next != position; next++)
        {
            if (_objs[next] == null)
            {
                _objs[lastVacantPosition] = null;
                _ids[lastVacantPosition] = 0;
                _isWrapped[lastVacantPosition] = false;
                return;
            }

            var nextStartPosition = ComputeStartPosition(_objs[next]);
            // If we wrapped while placing an object, then it must be that the start position wasn't wrapped to begin with
            var isNextStartPositionWrapped = next < position && !_isWrapped[next];
            var isLastVacantPositionWrapped = lastVacantPosition < position;

            // We want to avoid moving objects in the cache if the next bucket position is wrapped, but the last vacant position isn't
            // and we want to make sure to move objects in the cache when the last vacant position is wrapped but the next bucket position isn't
            if ((nextStartPosition <= lastVacantPosition && !(isNextStartPositionWrapped && !isLastVacantPositionWrapped)) ||
                (isLastVacantPositionWrapped && !isNextStartPositionWrapped))
            {
                _objs[lastVacantPosition] = _objs[next];
                _ids[lastVacantPosition] = _ids[next];
                // A wrapped object might become unwrapped if it moves from the front of the array to the end of the array
                _isWrapped[lastVacantPosition] = _isWrapped[next] && next > lastVacantPosition;
                lastVacantPosition = next;
            }

            if (next == cacheSize - 1)
            {
                next = -1;
            }
        }

        // m_obj must ALWAYS have at least one slot empty (null).
        Debug.Fail("Object table overflow");
        throw XmlObjectSerializer.CreateSerializationException(SR.ObjectTableOverflow);
    }

    private int ComputeStartPosition(object? o)
    {
        return (RuntimeHelpers.GetHashCode(o) & 0x7FFFFFFF) % _objs.Length;
    }

    private void Rehash()
    {
        var size = GetPrime(_objs.Length + 1); // The lookup does an inherent doubling
        var oldIds = _ids;
        var oldObjs = _objs;
        _ids = new int[size];
        _objs = new object[size];
        _isWrapped = new bool[size];

        for (var j = 0; j < oldObjs.Length; j++)
        {
            var obj = oldObjs[j];
            if (obj != null)
            {
                var position = FindElement(obj, out _, out var isWrapped);
                _objs[position] = obj;
                _ids[position] = oldIds[j];
                _isWrapped[position] = isWrapped;
            }
        }
    }

    private static int GetPrime(int min)
    {
        ReadOnlySpan<int> primes =
        [
            3, 7, 17, 37, 89, 197, 431, 919, 1931, 4049, 8419, 17519, 36353,
            75431, 156437, 324449, 672827, 1395263, 2893249, 5999471,
            11998949, 23997907, 47995853, 95991737, 191983481, 383966977, 767933981, 1535867969,
            2146435069, 0x7FFFFFC7,
            // 0x7FFFFFC7 == Array.MaxLength is not prime, but it is the largest possible array size.
            // There's nowhere to go from here. Using a const rather than the MaxLength property
            // so that the array contains only const values.
        ];

        foreach (var prime in primes)
        {
            if (prime >= min)
            {
                return prime;
            }
        }

        return min;
    }
}