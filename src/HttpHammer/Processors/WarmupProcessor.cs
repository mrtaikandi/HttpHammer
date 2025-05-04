using System.Net;
using System.Text.Json;
using HttpHammer.Configuration;
using HttpHammer.Http;
using Microsoft.Extensions.Logging;

namespace HttpHammer.Processors;

internal sealed class WarmupProcessor : IProcessor
{
    private readonly ILogger<WarmupProcessor> _logger;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IVariableHandler _variableHandler;

    public WarmupProcessor(ILogger<WarmupProcessor> logger, IHttpRequestBuilder requestBuilder, IVariableHandler variableHandler)
    {
        _logger = logger;
        _requestBuilder = requestBuilder;
        _variableHandler = variableHandler;
    }

    /// <inheritdoc />
    public int Order => 1;

    /// <inheritdoc />
    public async ValueTask<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        var plan = context.ExecutionPlan;
        if (plan.WarmupRequests.Length == 0)
        {
            _logger.LogNoWarmupRequestsToExecute();
            return ProcessorResult.Success(plan);
        }

        _logger.LogExecutingWarmupRequest(plan.WarmupRequests.Length);
        var progress = context.Progress.Create(":fire: Warming up :fire:", plan.WarmupRequests.Length);

        try
        {
            foreach (var definition in plan.WarmupRequests)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    progress.Increment();
                    return ProcessorResult.Fail("Warmup request execution was cancelled.");
                }

                string? error = null;

                if (definition is RequestDefinition requestDefinition)
                {
                    error = await ExecuteRequestAsync(requestDefinition, plan.Variables, cancellationToken);
                }

                progress.Increment();

                if (error is not null)
                {
                    return ProcessorResult.Fail(error);
                }
            }

            return ProcessorResult.Success(plan);
        }
        catch (Exception ex)
        {
            _logger.LogWarmupErrorOccurred(ex, ex.Message);
            return ProcessorResult.Fail($"An error occurred during warmup requests: {ex.Message}");
        }
        finally
        {
            progress.Complete();
        }
    }

    private async Task<string?> ExecuteRequestAsync(RequestDefinition definition, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        _logger.LogExecutingWarmupRequestByName(definition.Name);

        try
        {
            using var response = await _requestBuilder.BuildRequest(definition, variables).SendAsync(cancellationToken);
            _logger.LogWarmupResponseReceived(definition.Name, response.StatusCode);

            return await ProcessResponseAsync(response, definition, variables, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarmupRequestExecutionFailed(ex, definition.Name, ex.Message);
            return $"Failed to execute warmup request '{definition.Name}': {ex.Message}";
        }
    }

    private async Task<string?> ProcessResponseAsync(
        HttpResponseMessage response,
        RequestDefinition definition,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (definition.Response is null)
        {
            return null;
        }

        var expectedStatusCode = definition.Response.StatusCode;
        if ((int)response.StatusCode != expectedStatusCode)
        {
            _logger.LogWarmupStatusCodeMismatch(definition.Name, response.StatusCode, expectedStatusCode);
            return $"Warmup request '{definition.Name}' failed with status code {response.StatusCode}. Expected {expectedStatusCode}";
        }

        try
        {
            // Extract variables from headers
            var (headerSuccess, headerError) = ExtractHeaderVariables(response, definition, variables);
            if (!headerSuccess)
            {
                return headerError;
            }

            // Extract variables from content
            var (contentSuccess, contentError) = await ExtractContentVariablesAsync(response, definition, variables, cancellationToken);
            return contentSuccess ? null : contentError;
        }
        catch (Exception ex)
        {
            _logger.LogWarmupResponseProcessingFailed(ex, definition.Name, ex.Message);
            return $"Failed to process response for warmup request '{definition.Name}': {ex.Message}";
        }
    }

    private (bool Success, string? Error) ExtractHeaderVariables(HttpResponseMessage response, RequestDefinition definition, IDictionary<string, string> variables)
    {
        if (definition.Response?.Headers is null)
        {
            return (true, null);
        }

        _logger.LogExtractingHeaderVariables(definition.Name);

        foreach (var extraction in definition.Response.Headers)
        {
            if (!response.Headers.TryGetValues(extraction.Key, out var values))
            {
                _logger.LogHeaderExtractionFailed(extraction.Key);
                return (false, $"Failed to extract '{extraction.Key}' from '{definition.Name}' response headers.");
            }

            if (!_variableHandler.TryGetAssignmentVariableName(extraction.Value, out var variableName))
            {
                _logger.LogExpectedHeaderVariableNotFound(extraction.Key);
            }
            else
            {
                variables[variableName] = values.FirstOrDefault() ?? string.Empty;
                _logger.LogExtractedHeaderVariable(variableName, variables[variableName]);
            }
        }

        return (true, null);
    }

    private async Task<(bool Success, string? Error)> ExtractContentVariablesAsync(
        HttpResponseMessage response,
        RequestDefinition definition,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (definition.Response?.Content is null)
        {
            return (true, null);
        }

        _logger.LogExtractingContentVariables(definition.Name);

        try
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            foreach (var extraction in definition.Response.Content)
            {
                if (!root.TryExtractJsonValue(extraction.Key, out var value))
                {
                    _logger.LogContentExtractionFailed(extraction.Key);
                    return (false, $"Failed to extract '{extraction.Key}' from '{definition.Name}' JSON response.");
                }

                if (!_variableHandler.TryGetAssignmentVariableName(extraction.Value, out var variableName))
                {
                    _logger.LogExpectedContentVariableNotFound(extraction.Key);
                }
                else
                {
                    variables[variableName] = value;
                    _logger.LogExtractedContentVariable(variableName, value);
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogContentProcessingFailed(ex, definition.Name, ex.Message);
            return (false, $"Failed to read response content of '{definition.Name}': {ex.Message}");
        }
    }
}

internal static partial class WarmupProcessorLogs
{
    [LoggerMessage(0, LogLevel.Debug, "No warmup requests to execute")]
    public static partial void LogNoWarmupRequestsToExecute(this ILogger<WarmupProcessor> logger);

    [LoggerMessage(1, LogLevel.Debug, "Executing '{WarmupCount}' warmup requests'")]
    public static partial void LogExecutingWarmupRequest(this ILogger<WarmupProcessor> logger, int warmupCount);

    [LoggerMessage(2, LogLevel.Error, "An error occurred during warmup requests: {Message}")]
    public static partial void LogWarmupErrorOccurred(this ILogger<WarmupProcessor> logger, Exception exception, string message);

    [LoggerMessage(3, LogLevel.Debug, "Executing warmup request '{Name}'")]
    public static partial void LogExecutingWarmupRequestByName(this ILogger<WarmupProcessor> logger, string? name);

    [LoggerMessage(4, LogLevel.Debug, "Received response for warmup request '{Name}' with status code {StatusCode}")]
    public static partial void LogWarmupResponseReceived(this ILogger<WarmupProcessor> logger, string? name, HttpStatusCode statusCode);

    [LoggerMessage(5, LogLevel.Error, "Failed to execute warmup request '{Name}': {Message}")]
    public static partial void LogWarmupRequestExecutionFailed(this ILogger<WarmupProcessor> logger, Exception exception, string? name, string message);

    [LoggerMessage(6, LogLevel.Warning, "Warmup request '{Name}' failed with status code {StatusCode}. Expected {ExpectedStatusCode}")]
    public static partial void LogWarmupStatusCodeMismatch(this ILogger<WarmupProcessor> logger, string? name, HttpStatusCode statusCode, int expectedStatusCode);

    [LoggerMessage(7, LogLevel.Error, "Failed to process response for warmup request '{Name}': {Message}")]
    public static partial void LogWarmupResponseProcessingFailed(this ILogger<WarmupProcessor> logger, Exception exception, string? name, string message);

    [LoggerMessage(8, LogLevel.Debug, "Extracting variables from headers for warmup request '{Name}'...")]
    public static partial void LogExtractingHeaderVariables(this ILogger<WarmupProcessor> logger, string? name);

    [LoggerMessage(9, LogLevel.Error, "Failed to extract header value for key '{Key}'")]
    public static partial void LogHeaderExtractionFailed(this ILogger<WarmupProcessor> logger, string key);

    [LoggerMessage(10, LogLevel.Warning, "Expected to find '{Key}' variable in response headers, but it was not found.")]
    public static partial void LogExpectedHeaderVariableNotFound(this ILogger<WarmupProcessor> logger, string key);

    [LoggerMessage(11, LogLevel.Debug, "Extracted variable {VariableName} = {VariableValue}")]
    public static partial void LogExtractedHeaderVariable(this ILogger<WarmupProcessor> logger, string variableName, string variableValue);

    [LoggerMessage(12, LogLevel.Debug, "Extracting variables from JSON response for warmup request '{Name}'...")]
    public static partial void LogExtractingContentVariables(this ILogger<WarmupProcessor> logger, string? name);

    [LoggerMessage(13, LogLevel.Error, "Failed to extract JSON value for path '{Path}'")]
    public static partial void LogContentExtractionFailed(this ILogger<WarmupProcessor> logger, string path);

    [LoggerMessage(14, LogLevel.Warning, "Expected to find '{Key}' variable in response content, but it was not found.")]
    public static partial void LogExpectedContentVariableNotFound(this ILogger<WarmupProcessor> logger, string key);

    [LoggerMessage(15, LogLevel.Debug, "Extracted variable {VariableName} = {VariableValue}")]
    public static partial void LogExtractedContentVariable(this ILogger<WarmupProcessor> logger, string variableName, string variableValue);

    [LoggerMessage(16, LogLevel.Error, "Failed to read JSON response content for warmup request '{Name}': {Message}")]
    public static partial void LogContentProcessingFailed(this ILogger<WarmupProcessor> logger, Exception exception, string? name, string message);
}