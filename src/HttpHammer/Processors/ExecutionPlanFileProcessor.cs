using HttpHammer.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HttpHammer.Processors;

public class ExecutionPlanFileProcessor : IProcessor
{
    private readonly ILogger<ExecutionPlanFileProcessor> _logger;

    public ExecutionPlanFileProcessor(ILogger<ExecutionPlanFileProcessor> logger)
    {
        _logger = logger;
    }

    public int Order => 0;

    public ValueTask<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var executionPlan = LoadExecutionPlan(context.ExecutionPlan.FilePath);
            if (executionPlan is null)
            {
                return new ValueTask<ProcessorResult>(ProcessorResult.Fail("Failed to load execution plan from file."));
            }

            NormalizeRequestDefinitions(executionPlan);

            return ValidateExecutionPlan(executionPlan) is { } error
                ? new ValueTask<ProcessorResult>(ProcessorResult.Fail(error))
                : new ValueTask<ProcessorResult>(ProcessorResult.Success(executionPlan));
        }
        catch (Exception ex)
        {
            _logger.LogExecutionError(ex);
            return new ValueTask<ProcessorResult>(ProcessorResult.Fail($"Error processing plan: {ex.Message}"));
        }
    }

    private static void NormalizeRequestDefinitions(ExecutionPlan executionPlan)
    {
        foreach (var (name, request) in executionPlan.WarmupRequests)
        {
            NormalizeRequestDefinitions(name, request);
        }

        foreach (var (name, request) in executionPlan.Requests)
        {
            NormalizeRequestDefinitions(name, request);
        }
    }

    private static void NormalizeRequestDefinitions(string name, BaseRequestDefinition request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            request.Name = name;
        }

        if (request.MaxRequests < 0)
        {
            request.MaxRequests = 0;
        }
    }

    private static string? ValidateExecutionPlan(ExecutionPlan executionPlan)
    {
        var requests = executionPlan.WarmupRequests.Values
            .OfType<BaseRequestDefinition>()
            .Concat(executionPlan.Requests.Values);

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return $"Request '{request.Name}' URL is missing or empty.";
            }
        }

        return null;
    }

    private ExecutionPlan? LoadExecutionPlan(string filePath)
    {
        _logger.LogPreparingExecutionPlan(filePath);

        try
        {
            filePath = Path.GetFullPath(filePath);
            _logger.LogFullFilePath(filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogFileNotFound(filePath);
                return null;
            }

            var deserializer = new StaticDeserializerBuilder(new Configuration.ExecutionPlanYamlStaticContext())
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlContent = File.ReadAllText(filePath);
            _logger.LogYamlContent(yamlContent);

            var executionPlan = deserializer.Deserialize<ExecutionPlan>(yamlContent);

            // Add default variables
            executionPlan.Variables.TryAdd("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            return executionPlan;
        }
        catch (Exception ex)
        {
            _logger.LogReadFileError(ex);
            return null;
        }
    }
}

internal static partial class ExecutionPlanFileProcessorLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Preparing execution plan from '{FilePath}' YAML file")]
    public static partial void LogPreparingExecutionPlan(this ILogger<ExecutionPlanFileProcessor> logger, string filePath);

    [LoggerMessage(1, LogLevel.Debug, "Full file path: {FullPath}")]
    public static partial void LogFullFilePath(this ILogger<ExecutionPlanFileProcessor> logger, string fullPath);

    [LoggerMessage(2, LogLevel.Error, "Execution plan file not found: {FilePath}")]
    public static partial void LogFileNotFound(this ILogger<ExecutionPlanFileProcessor> logger, string filePath);

    [LoggerMessage(3, LogLevel.Debug, "YAML content: {Content}")]
    public static partial void LogYamlContent(this ILogger<ExecutionPlanFileProcessor> logger, string content);

    [LoggerMessage(4, LogLevel.Error, "Failed to read execution plan file")]
    public static partial void LogReadFileError(this ILogger<ExecutionPlanFileProcessor> logger, Exception exception);

    [LoggerMessage(5, LogLevel.Error, "Error executing file processor")]
    public static partial void LogExecutionError(this ILogger<ExecutionPlanFileProcessor> logger, Exception exception);
}