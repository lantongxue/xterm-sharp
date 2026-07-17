namespace XtermSharp.Internal.Utilities.Collections;

/// <summary>
/// A list with a fixed maximum size that drops values from the front when it wraps.
/// </summary>
internal sealed class CircularList<T> : IDisposable
{
    private T?[] _array;
    private int _startIndex;
    private int _length;
    private int _maxLength;

    public CircularList(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        _maxLength = maxLength;
        _array = new T?[maxLength];
    }

    public Emitter<DeleteEvent> OnDeleteEmitter { get; } = new();

    public XtermEvent<DeleteEvent> OnDelete => OnDeleteEmitter.Event;

    public Emitter<InsertEvent> OnInsertEmitter { get; } = new();

    public XtermEvent<InsertEvent> OnInsert => OnInsertEmitter.Event;

    public Emitter<int> OnTrimEmitter { get; } = new();

    public XtermEvent<int> OnTrim => OnTrimEmitter.Event;

    public int MaxLength
    {
        get => _maxLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_maxLength == value)
            {
                return;
            }

            var newArray = new T?[value];
            for (int i = 0; i < Math.Min(value, Length); i++)
            {
                newArray[i] = _array[GetCyclicIndex(i)];
            }
            _array = newArray;
            _maxLength = value;
            _startIndex = 0;
        }
    }

    public int Length
    {
        get => _length;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value > _maxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Length cannot exceed MaxLength.");
            }
            if (value > _length)
            {
                for (int i = _length; i < value; i++)
                {
                    _array[GetCyclicIndex(i)] = default;
                }
            }
            _length = value;
        }
    }

    public bool IsFull => _length == _maxLength;

    public T? Get(int index) => _array[GetCyclicIndex(index)];

    public void Set(int index, T? value) => _array[GetCyclicIndex(index)] = value;

    public void Push(T value)
    {
        _array[GetCyclicIndex(_length)] = value;
        if (_length == _maxLength)
        {
            _startIndex = ++_startIndex % _maxLength;
            OnTrimEmitter.Fire(1);
        }
        else
        {
            _length++;
        }
    }

    public T Recycle()
    {
        if (_length != _maxLength)
        {
            throw new InvalidOperationException("Can only recycle when the buffer is full");
        }
        _startIndex = ++_startIndex % _maxLength;
        OnTrimEmitter.Fire(1);
        return _array[GetCyclicIndex(_length - 1)]!;
    }

    public T? Pop()
    {
        if (_length == 0)
        {
            return default;
        }
        return _array[GetCyclicIndex(--_length)];
    }

    public void Splice(int start, int deleteCount, params T[] items)
    {
        if (deleteCount != 0)
        {
            for (int i = start; i < _length - deleteCount; i++)
            {
                _array[GetCyclicIndex(i)] = _array[GetCyclicIndex(i + deleteCount)];
            }
            _length -= deleteCount;
            OnDeleteEmitter.Fire(new DeleteEvent(start, deleteCount));
        }

        for (int i = _length - 1; i >= start; i--)
        {
            _array[GetCyclicIndex(i + items.Length)] = _array[GetCyclicIndex(i)];
        }
        for (int i = 0; i < items.Length; i++)
        {
            _array[GetCyclicIndex(start + i)] = items[i];
        }
        if (items.Length != 0)
        {
            OnInsertEmitter.Fire(new InsertEvent(start, items.Length));
        }

        if (_length + items.Length > _maxLength)
        {
            int countToTrim = _length + items.Length - _maxLength;
            _startIndex += countToTrim;
            _length = _maxLength;
            OnTrimEmitter.Fire(countToTrim);
        }
        else
        {
            _length += items.Length;
        }
    }

    public void TrimStart(int count)
    {
        if (count > _length)
        {
            count = _length;
        }
        _startIndex += count;
        _length -= count;
        OnTrimEmitter.Fire(count);
    }

    public void ShiftElements(int start, int count, int offset)
    {
        if (count <= 0)
        {
            return;
        }
        if (start < 0 || start >= _length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "start argument out of range");
        }
        if (start + offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Cannot shift elements in list beyond index 0");
        }

        if (offset > 0)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                Set(start + i + offset, Get(start + i));
            }
            int expandListBy = start + count + offset - _length;
            if (expandListBy > 0)
            {
                _length += expandListBy;
                while (_length > _maxLength)
                {
                    _length--;
                    _startIndex++;
                    OnTrimEmitter.Fire(1);
                }
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                Set(start + i + offset, Get(start + i));
            }
        }
    }

    public void Dispose()
    {
        OnDeleteEmitter.Dispose();
        OnInsertEmitter.Dispose();
        OnTrimEmitter.Dispose();
    }

    private int GetCyclicIndex(int index) => (_startIndex + index) % _maxLength;
}
