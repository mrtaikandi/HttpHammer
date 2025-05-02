using HttpHammer.Console;

namespace HttpHammer.Tests.Infrastructure;

public class FakeProgressContext : IProgressContext
{
    private readonly SynchronousProgress _progress;
    private readonly FakeProgressTracker? _tracker;

    public FakeProgressContext(SynchronousProgress? progress = null, FakeProgressTracker? tracker = null)
    {
        _progress = progress ?? new SynchronousProgress();
        _tracker = tracker;
    }

    public SynchronousProgress Progress => _progress;

    public IProgress Create(string description, int maxValue)
    {
        _progress.MaxValue(maxValue);
        return _progress;
    }
}