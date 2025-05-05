using HttpHammer.Console.Renderers;
using HttpHammer.Diagnostics;
using Spectre.Console;

namespace HttpHammer.Console;

internal class ProgressTracker : IProgressTracker
{
    private readonly IAnsiConsole _console;
    private readonly IProfiler _profiler;

    public ProgressTracker(IAnsiConsole console, IProfiler profiler)
    {
        _console = console;
        _profiler = profiler;
    }

    /// <inheritdoc />
    public async Task<T> TrackAsync<T>(Func<IProgressContext, Task<T>> action)
    {
        return await CreateProgress()
            .StartAsync(async context =>
            {
                var progressContext = new SpectreProgressContext(context);
                return await action(progressContext);
            });
    }

    /// <inheritdoc />
    public async Task TrackAsync(Func<IProgressContext, Task> action)
    {
        await CreateProgress()
            .StartAsync(async context =>
            {
                var progressContext = new SpectreProgressContext(context);
                await action(progressContext);
            });
    }

    /// <inheritdoc />
    public async Task TrackAsync(string description, int maxValue, Func<IProgress, Task> action)
    {
        await CreateProgress()
            .StartAsync(async context =>
            {
                var progressContext = new SpectreProgressContext(context);
                await action(progressContext.Create(description, maxValue));
            });
    }

    /// <inheritdoc />
    public async Task<T> TrackAsync<T>(string description, int maxValue, Func<IProgress, Task<T>> action)
    {
        return await _console.Progress()
            .StartAsync(async context =>
            {
                var progressContext = new SpectreProgressContext(context);
                return await action(progressContext.Create(description, maxValue));
            });
    }

    private Progress CreateProgress() =>
        _console.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn { Width = 20 },
                new ElapsedTimeColumn(),
                new SpinnerColumn(),
                new RequestMeasurementsColumn(_profiler));

    private class SpectreProgress : IProgress
    {
        private readonly ProgressTask _task;

        public SpectreProgress(ProgressTask task)
        {
            _task = task;
        }

        public void Increment() => _task.Increment(1);

        public void Complete(int? value = null)
        {
            if (value.HasValue)
            {
                _task.Value(value.Value);
            }

            _task.StopTask();
        }

        public bool IsIndeterminate
        {
            get => _task.IsIndeterminate;
            set => _task.IsIndeterminate = value;
        }

        public void MaxValue(int maxValue) => _task.MaxValue(maxValue);
    }

    private class SpectreProgressContext : IProgressContext
    {
        private readonly ProgressContext _context;

        public SpectreProgressContext(ProgressContext context)
        {
            _context = context;
        }

        public IProgress Create(string description, int maxValue)
        {
            var task = _context.AddTask(description, maxValue: maxValue);
            return new SpectreProgress(task);
        }
    }
}