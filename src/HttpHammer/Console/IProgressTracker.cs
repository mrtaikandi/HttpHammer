namespace HttpHammer.Console;

public interface IProgressTracker
{
    Task<T> TrackAsync<T>(Func<IProgressContext, Task<T>> action);

    Task TrackAsync(Func<IProgressContext, Task> action);

    Task TrackAsync(string description, int maxValue, Func<IProgress, Task> action);

    Task<T> TrackAsync<T>(string description, int maxValue, Func<IProgress, Task<T>> action);
}