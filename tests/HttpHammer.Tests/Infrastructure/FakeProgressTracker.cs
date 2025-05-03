using System.Collections.Concurrent;
using HttpHammer.Console;

namespace HttpHammer.Tests.Infrastructure;

public class FakeProgressTracker : IProgressTracker
{
    private readonly SynchronousProgress _progress;
    private readonly ConcurrentBag<ProgressMessage> _messages = new();

    public FakeProgressTracker(SynchronousProgress? progress = null)
    {
        _progress = progress ?? new SynchronousProgress();
    }

    public SynchronousProgress Progress => _progress;

    public Task<T> TrackAsync<T>(Func<IProgressContext, Task<T>> action)
    {
        var context = new FakeProgressContext(_progress, this);
        return action(context);
    }

    public Task TrackAsync(Func<IProgressContext, Task> action)
    {
        var context = new FakeProgressContext(_progress, this);
        return action(context);
    }

    public Task TrackAsync(string description, int maxValue, Func<IProgress, Task> action)
    {
        _progress.SetMaxValue(maxValue);
        return action(_progress);
    }

    public Task<T> TrackAsync<T>(string description, int maxValue, Func<IProgress, Task<T>> action)
    {
        _progress.SetMaxValue(maxValue);
        return action(_progress);
    }

    public IReadOnlyCollection<ProgressMessage> GetMessages()
    {
        var progressMessages = _progress.Messages.ToList();
        var trackerMessages = _messages.ToList();
        return progressMessages.Concat(trackerMessages).ToArray();
    }

    public IReadOnlyCollection<ProgressMessage> GetMessages(MessageType type)
    {
        return GetMessages().Where(m => m.Type == type).ToArray();
    }

    public void ClearMessages()
    {
        _messages.Clear();

        // Note: We can't clear messages in SynchronousProgress directly
        // as it doesn't expose a clear method
    }

    internal void AddMessage(ProgressMessage message)
    {
        _messages.Add(message);
    }

    private class FakeProgressContext : IProgressContext
    {
        private readonly SynchronousProgress _progress;
        private readonly FakeProgressTracker _tracker;

        public FakeProgressContext(SynchronousProgress progress, FakeProgressTracker tracker)
        {
            _progress = progress;
            _tracker = tracker;
        }

        public IProgress Create(string description, int maxValue)
        {
            _progress.SetMaxValue(maxValue);
            return _progress;
        }

        public void Report(int increment) => _progress.Increment();
    }
}