#pragma warning disable SA1201

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HttpHammer.Diagnostics;

public class Profiler : IProfiler
{
    private readonly ConcurrentDictionary<string, InternalMeasurements> _measurements = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Measurements GetMeasurements(string requestName) =>
        _measurements.TryGetValue(requestName, out var stats) ? stats.GetMeasurements() : default;

    public IReadOnlyDictionary<string, Measurements> GetMeasurements() =>
        _measurements.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetMeasurements(), StringComparer.OrdinalIgnoreCase);

    public TimeSpan Record(string requestName, long startTime, string? errorMessage = null)
    {
        var stopTime = Stopwatch.GetTimestamp();
        var duration = Stopwatch.GetElapsedTime(startTime, stopTime);

        _measurements.AddOrUpdate(
            requestName,
            _ =>
            {
                var m = new InternalMeasurements();
                m.AddDuration(duration);
                m.AddError(errorMessage);
                return m;
            },
            (_, m) =>
            {
                m.AddDuration(duration);
                m.AddError(errorMessage);
                return m;
            });

        return duration;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Start() => Stopwatch.GetTimestamp();

    private sealed class InternalMeasurements
    {
        private readonly ConcurrentBag<long> _durationTicks = new();
        private readonly ConcurrentBag<string> _errors = new();
        private long _count;
        private long _maxDurationTicks;
        private long _minDurationTicks = long.MaxValue;
        private long _totalDurationTicks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddDuration(TimeSpan duration)
        {
            var durationTicks = duration.Ticks;
            _durationTicks.Add(durationTicks);
            Interlocked.Add(ref _totalDurationTicks, durationTicks);
            Interlocked.Increment(ref _count);

            long currentMin;
            do
            {
                currentMin = Interlocked.Read(ref _minDurationTicks);
                if (durationTicks >= currentMin)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref _minDurationTicks, durationTicks, currentMin) != currentMin);

            long currentMax;
            do
            {
                currentMax = Interlocked.Read(ref _maxDurationTicks);
                if (durationTicks <= currentMax)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref _maxDurationTicks, durationTicks, currentMax) != currentMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddError(string? errorMessage)
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _errors.Add(errorMessage);
            }
        }

        public Measurements GetMeasurements()
        {
            var count = Interlocked.CompareExchange(ref _count, 0, 0);

            return new Measurements(
                Min: GetMinDuration(),
                Max: GetMaxDuration(),
                Count: count,
                P50: GetPercentileDuration(50),
                P75: GetPercentileDuration(75),
                P95: GetPercentileDuration(95),
                Total: GetTotalDuration(),
                Errors: _errors.ToArray());
        }

        public TimeSpan GetTotalDuration() =>
            TimeSpan.FromTicks(Interlocked.CompareExchange(ref _totalDurationTicks, 0, 0));

        private TimeSpan GetMaxDuration() => TimeSpan.FromTicks(Interlocked.Read(ref _maxDurationTicks));

        private TimeSpan GetMinDuration()
        {
            var minTicks = Interlocked.Read(ref _minDurationTicks);
            return minTicks == long.MaxValue ? TimeSpan.Zero : TimeSpan.FromTicks(minTicks);
        }

        private TimeSpan GetPercentileDuration(int percentile)
        {
            if (_durationTicks.IsEmpty)
            {
                return TimeSpan.Zero;
            }

            // Get a sorted snapshot of durations
            var sortedDurations = _durationTicks.ToArray();
            Array.Sort(sortedDurations);

            // Calculate the index for the percentile
            var index = (int)Math.Ceiling(percentile / 100.0 * sortedDurations.Length) - 1;

            // Ensure the index is within bounds
            index = Math.Max(0, Math.Min(index, sortedDurations.Length - 1));

            return TimeSpan.FromTicks(sortedDurations[index]);
        }
    }
}