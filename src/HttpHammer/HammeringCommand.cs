using System.CommandLine;
using HttpHammer.Configuration;
using HttpHammer.Console;
using HttpHammer.Console.Renderers;
using HttpHammer.Diagnostics;
using HttpHammer.Processors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HttpHammer;

public class HammeringCommand : RootCommand
{
    private readonly ILogger<HammeringCommand> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IAnsiConsole _console;
    private readonly IProgressTracker _tracker;
    private readonly IEnumerable<IProcessor> _processors;
    private readonly IProfiler _profiler;

    public HammeringCommand(
        ILogger<HammeringCommand> logger,
        IHostApplicationLifetime applicationLifetime,
        IAnsiConsole console,
        IProgressTracker tracker,
        IEnumerable<IProcessor> processors,
        IProfiler profiler)
        : base("Command line load testing tool for HTTP APIs.")
    {
        _logger = logger;
        _applicationLifetime = applicationLifetime;
        _console = console;
        _tracker = tracker;
        _processors = processors;
        _profiler = profiler;

        Options.Add(new Option<bool>("--debug", "-d")
        {
            Description = "Enable debug logging to the console.",
            Recursive = true
        });

        Options.Add(new Option<bool>("--verbose", "-v")
        {
            Description = "Enable verbose logging to the console.",
            Recursive = true
        });

        Options.Add(new Option<string>("--file", "-f")
        {
            Description = "Path to the plan YAML configuration file.",
            Required = false,
            Validators =
            {
                result =>
                {
                    var filePath = result.GetValueOrDefault<string>();
                    if (!File.Exists(filePath))
                    {
                        result.AddError($"The specified file does not exist: {filePath}");
                    }
                }
            }
        });

        SetAction(ExecuteAsync);
    }

    private async Task ExecuteAsync(ParseResult parseResult, CancellationToken cancellation)
    {
        if (parseResult.GetValue<string>("--file") is not { } filePath)
        {
            filePath = await _console.PromptForStringAsync(
                "Please provide the path to the plan YAML configuration file:",
                @".\plan.yaml",
                validator: value => File.Exists(value) ? ValidationResult.Success() : ValidationResult.Error($"The specified file does not exist: {value}"),
                cancellationToken: cancellation);
        }

        await ExecuteAsync(filePath, cancellation);
    }

    private async Task ExecuteAsync(string executionPlanFilePath, CancellationToken cancellationToken)
    {
        try
        {
            await _tracker.TrackAsync(context => ExecuteProcessorsAsync(context, executionPlanFilePath, cancellationToken));

            var measurements = _profiler.GetMeasurements();
            _console.DisplayResults(measurements);

            _applicationLifetime.StopApplication();
        }
        catch (TaskCanceledException)
        {
            // Do nothing
        }
    }

    private async Task ExecuteProcessorsAsync(IProgressContext context, string filePath, CancellationToken cancellationToken)
    {
        var executionPlan = new ExecutionPlan { FilePath = filePath };

        foreach (var processor in _processors.OrderBy(p => p.Order))
        {
            var processorName = processor.GetType().Name;
            _logger.LogExecutingProcessor(processorName);

            var result = await processor.ExecuteAsync(new ProcessorContext(executionPlan, context), cancellationToken);
            if (result is SuccessProcessorResult success)
            {
                executionPlan = success.ExecutionPlan;
            }
            else if (result is ErrorProcessorResult error)
            {
                _logger.LogFinishedExecutingProcessor(processorName, error.Errors.Length);
                _console.DisplayErrors(error.Errors);

                break;
            }
        }
    }
}

internal static partial class HammeringCommandLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Executing processor: {Processor}")]
    public static partial void LogExecutingProcessor(this ILogger<HammeringCommand> logger, string processor);

    [LoggerMessage(1, LogLevel.Debug, "Finished executing processor: {Processor} with {Errors} errors")]
    public static partial void LogFinishedExecutingProcessor(this ILogger<HammeringCommand> logger, string processor, int errors);
}