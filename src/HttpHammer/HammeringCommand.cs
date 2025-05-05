using System.CommandLine;
using HttpHammer.Console;
using HttpHammer.Console.Renderers;
using HttpHammer.Diagnostics;
using HttpHammer.Plan;
using HttpHammer.Plan.Definitions;
using HttpHammer.Processors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace HttpHammer;

public class HammeringCommand : RootCommand
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IAnsiConsole _console;
    private readonly ILogger<HammeringCommand> _logger;
    private readonly IExecutionPlanLoader _planLoader;
    private readonly IProcessorFactory _processorFactory;
    private readonly IProfiler _profiler;
    private readonly IProgressTracker _tracker;

    public HammeringCommand(
        ILogger<HammeringCommand> logger,
        IHostApplicationLifetime applicationLifetime,
        IAnsiConsole console,
        IProgressTracker tracker,
        IExecutionPlanLoader planLoader,
        IProcessorFactory processorFactory,
        IProfiler profiler)
        : base("Command line load testing tool for HTTP APIs.")
    {
        _logger = logger;
        _applicationLifetime = applicationLifetime;
        _console = console;
        _tracker = tracker;
        _planLoader = planLoader;
        _processorFactory = processorFactory;
        _profiler = profiler;

        ConfigureCommandOptions();
    }

    private void ConfigureCommandOptions()
    {
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

        SetAction(async (parseResult, cancellation) =>
        {
            if (parseResult.GetValue<string>("--file") is not { } filePath)
            {
                filePath = await _console.PromptForStringAsync(
                    "Please provide the path to the plan YAML configuration file:",
                    @".\plan.yaml",
                    value => File.Exists(value) ? ValidationResult.Success() : ValidationResult.Error($"The specified file does not exist: {value}"),
                    cancellation);
            }

            await ExecuteAsync(filePath, cancellation);
        });
    }

    private async Task ExecuteAsync(string executionPlanFilePath, CancellationToken cancellationToken)
    {
        try
        {
            await _tracker.TrackAsync(async progressContext =>
            {
                var executionPlan = _planLoader.Load(executionPlanFilePath);

                var result = await ProcessorWarmupDefinitionsAsync(executionPlan, progressContext, cancellationToken);
                if (result is SuccessProcessorResult)
                {
                    result = await ProcessRequestsAsync(executionPlan, progressContext, cancellationToken);
                }

                if (result is ErrorProcessorResult error)
                {
                    _console.DisplayErrors(error.Errors);
                }
            });

            var measurements = _profiler.GetMeasurements();
            _console.DisplayResults(measurements);

            _applicationLifetime.StopApplication();
        }
        catch (TaskCanceledException)
        {
            // Do nothing
        }
        catch (ExecutionPlanLoadException lx)
        {
            _console.DisplayError(lx.Message);
        }
    }

    private async Task<ProcessorResult> ProcessorWarmupDefinitionsAsync(ExecutionPlan plan, IProgressContext progressContext, CancellationToken cancellationToken)
    {
        foreach (var definition in plan.WarmupDefinitions)
        {
            var processor = _processorFactory.Create(definition);
            var processorName = processor.GetType().Name;
            _logger.LogExecutingProcessor(processorName);

            var result = await processor.ExecuteAsync(new ProcessorContext(definition, plan.Variables, progressContext), cancellationToken);
            _logger.LogFinishedExecutingProcessor(processorName, result);

            if (result.HasErrors)
            {
                return result;
            }
        }

        return ProcessorResult.Success();
    }

    private async Task<ProcessorResult> ProcessRequestsAsync(ExecutionPlan plan, IProgressContext progressContext, CancellationToken cancellationToken)
    {
        var requests = plan.RequestDefinitions.Where(r => r.MaxRequests > 0).ToArray();
        if (requests.Length == 0)
        {
            return ProcessorResult.Fail("No requests defined in the execution plan with MaxRequests > 0.");
        }

        _logger.LogExecutingRequests(requests.Length);

        await Task.WhenAll(requests.Select(request =>
        {
            var processor = _processorFactory.Create(request);
            var processorContext = new ProcessorContext(request, plan.Variables, progressContext);

            return processor.ExecuteAsync(processorContext, cancellationToken);
        }));

        return ProcessorResult.Success();
    }
}

internal static partial class HammeringCommandLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Executing processor: {Processor}")]
    public static partial void LogExecutingProcessor(this ILogger<HammeringCommand> logger, string processor);

    [LoggerMessage(1, LogLevel.Debug, "Finished executing processor: {Processor} with '{Result}'")]
    public static partial void LogFinishedExecutingProcessor(this ILogger<HammeringCommand> logger, string processor, ProcessorResult result);

    [LoggerMessage(2, LogLevel.Debug, "Executing {RequestsCount} requests.")]
    public static partial void LogExecutingRequests(this ILogger<HammeringCommand> logger, int requestsCount);
}