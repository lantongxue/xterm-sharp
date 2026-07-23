using System.Diagnostics;

namespace XtermSharp.Rendering.Skia.Diagnostics;

internal sealed class RenderingDebugMetrics
{
    private const int Capacity = 120;
    private const double IdleResetMilliseconds = 2000;

    private readonly object _gate = new();
    private readonly double[] _frameTimes = new double[Capacity];
    private long _lastTimestamp;
    private int _nextIndex;
    private int _count;
    private double _sum;

    public RenderingDebugSnapshot RecordFrame()
    {
        long timestamp = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_lastTimestamp != 0)
            {
                double elapsed = Stopwatch.GetElapsedTime(_lastTimestamp, timestamp).TotalMilliseconds;
                if (elapsed > 0 && elapsed <= IdleResetMilliseconds)
                {
                    AddFrameTimeLocked(elapsed);
                }
                else
                {
                    ClearSamplesLocked();
                }
            }
            _lastTimestamp = timestamp;
            return CreateSnapshotLocked();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _lastTimestamp = 0;
            ClearSamplesLocked();
        }
    }

    internal RenderingDebugSnapshot RecordFrameTime(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }
        lock (_gate)
        {
            AddFrameTimeLocked(milliseconds);
            return CreateSnapshotLocked();
        }
    }

    private void AddFrameTimeLocked(double milliseconds)
    {
        if (_count == Capacity)
        {
            _sum -= _frameTimes[_nextIndex];
        }
        else
        {
            _count++;
        }
        _frameTimes[_nextIndex] = milliseconds;
        _sum += milliseconds;
        _nextIndex = (_nextIndex + 1) % Capacity;
    }

    private RenderingDebugSnapshot CreateSnapshotLocked()
    {
        if (_count == 0)
        {
            return RenderingDebugSnapshot.Empty;
        }
        double minimum = double.MaxValue;
        double maximum = double.MinValue;
        for (int index = 0; index < _count; index++)
        {
            double value = _frameTimes[index];
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
        double average = _sum / _count;
        return new RenderingDebugSnapshot(
            _count,
            1000 / average,
            average,
            maximum,
            minimum);
    }

    private void ClearSamplesLocked()
    {
        Array.Clear(_frameTimes);
        _nextIndex = 0;
        _count = 0;
        _sum = 0;
    }
}
