using System.Collections.Concurrent;
using HttpHammer.Console;

namespace HttpHammer.Tests.Infrastructure;

public class SynchronousProgress : IProgress
{
    private readonly Action<double>? _handler;
    private readonly ConcurrentBag<ProgressMessage> _messages = new();

    public SynchronousProgress(Action<double>? handler = null)
    {
        _handler = handler;
    }

    public double CurrentValue { get; private set; }

    public double MaximumValue { get; private set; }

    public IReadOnlyCollection<ProgressMessage> Messages => _messages.ToArray();

    public void Complete(int? value = null) => CurrentValue = value ?? MaximumValue;

    public bool IsIndeterminate { get; set; }

    public void Increment()
    {
        CurrentValue++;
        _handler?.Invoke(CurrentValue);
    }

    public void MaxValue(int value) => MaximumValue = value;

    public void SetMaxValue(int value) => MaximumValue = value;
}