namespace HttpHammer.Diagnostics;

public readonly record struct Measurements(
    long Count,
    TimeSpan Max,
    TimeSpan Min,
    TimeSpan P50,
    TimeSpan P75,
    TimeSpan P95,
    TimeSpan Total,
    IReadOnlyCollection<string> Errors);