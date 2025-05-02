namespace HttpHammer.Diagnostics;

public interface IProfiler
{
    Measurements GetMeasurements(string requestName);

    IReadOnlyDictionary<string, Measurements> GetMeasurements();

    TimeSpan Record(string requestName, long startTime, string? errorMessage = null);

    long Start();
}