using HttpHammer.Plan.Definitions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ExecutionPlanYamlStaticContext = HttpHammer.Plan.Definitions.ExecutionPlanYamlStaticContext;

namespace HttpHammer.Plan;

public class ExecutionPlanLoader : IExecutionPlanLoader
{
    private readonly ILogger<ExecutionPlanLoader> _logger;

    public ExecutionPlanLoader(ILogger<ExecutionPlanLoader> logger)
    {
        _logger = logger;
    }

    public ExecutionPlan Load(string filePath)
    {
        var executionPlan = LoadExecutionPlan(filePath);

        NormalizeDefinitions(executionPlan);
        ValidateExecutionPlan(executionPlan);

        return executionPlan;
    }

    private static void NormalizeDefinitions(ExecutionPlan executionPlan)
    {
        foreach (var request in executionPlan.WarmupDefinitions.OfType<RequestDefinition>())
        {
            Normalize(request, 1, 1);
        }

        foreach (var request in executionPlan.RequestDefinitions)
        {
            Normalize(request, 100, 10);
        }

        static void Normalize(RequestDefinition request, int defaultMaxRequest, int defaultConcurrentConnections)
        {
            request.Method = request.Method.ToUpperInvariant();
            request.MaxRequests = request.MaxRequests switch
            {
                null => defaultMaxRequest,
                < 0 => 0,
                _ => request.MaxRequests
            };

            request.ConcurrentConnections = request.ConcurrentConnections switch
            {
                null => defaultConcurrentConnections,
                < 0 => 1,
                _ => request.ConcurrentConnections
            };
        }
    }

    private static void ValidateExecutionPlan(ExecutionPlan executionPlan)
    {
        var requests = executionPlan.WarmupDefinitions
            .OfType<RequestDefinition>()
            .Concat(executionPlan.RequestDefinitions);

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                throw new ExecutionPlanLoadException($"Request '{request.Name}' URL is missing or empty.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ExecutionPlanLoadException($"Request '{request.Method}: {request.Url}' name is missing or empty.");
            }
        }
    }

    private ExecutionPlan LoadExecutionPlan(string filePath)
    {
        _logger.LogPreparingExecutionPlan(filePath);

        try
        {
            filePath = Path.GetFullPath(filePath);
            _logger.LogFullFilePath(filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogFileNotFound(filePath);
                return new ExecutionPlan { FilePath = filePath };
            }

            var deserializer = new StaticDeserializerBuilder(new ExecutionPlanYamlStaticContext())
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlContent = File.ReadAllText(filePath);
            _logger.LogYamlContent(yamlContent);

            var executionPlan = deserializer.Deserialize<ExecutionPlan>(yamlContent);
            executionPlan.FilePath = filePath;

            // Add default variables
            executionPlan.Variables.TryAdd("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            return executionPlan;
        }
        catch (Exception ex)
        {
            _logger.LogReadFileError(ex);
            throw new ExecutionPlanLoadException("Failed to load execution plan from file.", ex);
        }
    }
}

internal static partial class HammeringPlanFileLoaderLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Preparing execution plan from '{FilePath}' YAML file")]
    public static partial void LogPreparingExecutionPlan(this ILogger<ExecutionPlanLoader> logger, string filePath);

    [LoggerMessage(1, LogLevel.Debug, "Full file path: {FullPath}")]
    public static partial void LogFullFilePath(this ILogger<ExecutionPlanLoader> logger, string fullPath);

    [LoggerMessage(2, LogLevel.Error, "Execution plan file not found: {FilePath}")]
    public static partial void LogFileNotFound(this ILogger<ExecutionPlanLoader> logger, string filePath);

    [LoggerMessage(3, LogLevel.Debug, "YAML content: {Content}")]
    public static partial void LogYamlContent(this ILogger<ExecutionPlanLoader> logger, string content);

    [LoggerMessage(4, LogLevel.Error, "Failed to read execution plan file")]
    public static partial void LogReadFileError(this ILogger<ExecutionPlanLoader> logger, Exception exception);

    [LoggerMessage(5, LogLevel.Error, "Error executing file processor")]
    public static partial void LogExecutionError(this ILogger<ExecutionPlanLoader> logger, Exception exception);
}