namespace MusselChart;

using System.Collections;

public sealed class ChartRingBuffer<T> : IReadOnlyList<T>
{
    private readonly Lock sync = new();

    private readonly T[] buffer;

    private int head;

    private int count;

    private long sequence;

    private int generation;

    public ChartRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        buffer = new T[capacity];
    }

    public int Capacity => buffer.Length;

    public int Count
    {
        get
        {
            lock (sync)
            {
                return count;
            }
        }
    }

    public long Sequence
    {
        get
        {
            lock (sync)
            {
                return sequence;
            }
        }
    }

    public T this[int index]
    {
        get
        {
            lock (sync)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
                return buffer[Position(index)];
            }
        }
    }

    public void Push(T item)
    {
        lock (sync)
        {
            buffer[head] = item;
            head = (head + 1) % buffer.Length;
            if (count < buffer.Length)
            {
                count++;
            }

            sequence++;
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            Array.Clear(buffer);
            head = 0;
            count = 0;
            generation++;
        }
    }

    public T[] ToArray()
    {
        lock (sync)
        {
            var items = new T[count];
            for (var i = 0; i < count; i++)
            {
                items[i] = buffer[Position(i)];
            }

            return items;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)ToArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal ChartBufferRead Read(long afterSequence, int afterGeneration, List<T> destination)
    {
        lock (sync)
        {
            var added = sequence - afterSequence;
            var appended = (afterSequence >= 0) && (afterGeneration == generation) && (added >= 0) && (added <= count);
            var start = appended ? count - (int)added : 0;
            for (var i = start; i < count; i++)
            {
                destination.Add(buffer[Position(i)]);
            }

            return new ChartBufferRead(appended, sequence, generation, count);
        }
    }

    private int Position(int index) => (head - count + index + buffer.Length) % buffer.Length;
}

internal readonly record struct ChartBufferRead(bool Appended, long Sequence, int Generation, int Count);
