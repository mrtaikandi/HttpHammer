namespace HttpHammer.Internals;

internal static class DateTimeExtensions
{
    public static string ToDisplayString(this TimeSpan timeSpan) => timeSpan.TotalSeconds switch
    {
        < 1 => $"{timeSpan.TotalMilliseconds:F2} [grey66]ms[/]",
        < 60 => $"{timeSpan.TotalSeconds:F2} [grey66]s[/]",
        _ => $"{timeSpan.TotalMinutes:F2} [grey66]m[/]"
    };
}