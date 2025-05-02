using HttpHammer.Diagnostics;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HttpHammer.Console.Renderers;

internal class RequestMeasurementsColumn : ProgressColumn
{
    private readonly IProfiler _tracker;

    public RequestMeasurementsColumn(IProfiler tracker)
    {
        _tracker = tracker;
    }

    /// <inheritdoc />
    public override IRenderable Render(RenderOptions options, ProgressTask? task, TimeSpan deltaTime)
    {
        if (task?.Description == null)
        {
            return new Markup("\u2248 --[grey66]ms[/]");
        }

        var stats = _tracker.GetMeasurements(task.Description);

        return stats.Count == 0
            ? new Markup(string.Empty)
            : new Markup($"\u2248 {stats.P50.ToDisplayString()}");
    }
}