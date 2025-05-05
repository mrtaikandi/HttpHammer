using System.Diagnostics;
using System.Net;
using System.Text.Json;
using HttpHammer.Console;
using HttpHammer.Http;
using HttpHammer.Plan.Definitions;
using Microsoft.Extensions.Logging;

namespace HttpHammer.Processors;

public class RequestProcessor : IProcessor
{
    private readonly ILogger<RequestProcessor> _logger;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IVariableHandler _variableHandler;

    public RequestProcessor(ILogger<RequestProcessor> logger, IHttpRequestBuilder requestBuilder, IVariableHandler variableHandler)
    {
        _logger = logger;
        _requestBuilder = requestBuilder;
        _variableHandler = variableHandler;
    }

    /// <inheritdoc />
    public async Task<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        if (context.Definition is not RequestDefinition request)
        {
            throw new ProcessorException($"{nameof(RequestProcessor)} can only process {nameof(RequestDefinition)}.");
        }

        Debug.Assert(request.MaxRequests != null, "MaxRequests should not be null");
        var maxRequests = request.MaxRequests.Value;
        if (maxRequests == 0)
        {
            return ProcessorResult.Success();
        }

        _logger.LogExecutingRequest(request.Name);
        var progress = context.Progress?.Create(request.Name, maxRequests);

        try
        {
            if (request.ConcurrentConnections == 1)
            {
                return await ExecuteSynchronousRequestAsync(request, context.Variables, progress, cancellationToken);
            }

            await ExecuteConcurrentRequestAsync(request, context.Variables, progress, cancellationToken);
            return ProcessorResult.Success();
        }
        finally
        {
            progress?.Complete();
        }
    }

    private async Task ExecuteConcurrentRequestAsync(
        RequestDefinition request,
        Dictionary<string, string> variables,
        IProgress? progress,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request.ConcurrentConnections != null, "concurrentConnections should not be null");
        var concurrentConnections = request.ConcurrentConnections.Value;

        Debug.Assert(request.MaxRequests != null, "maxRequests should not be null");
        var maxRequests = request.MaxRequests.Value;

        var batchSize = Math.Min(concurrentConnections, maxRequests);

        for (var offset = 0; offset < maxRequests; offset += batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var currentBatchSize = Math.Min(batchSize, maxRequests - offset);
            var tasks = new List<Task>(currentBatchSize);

            for (var i = 0; i < currentBatchSize; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var iteration = offset + i + 1;
                variables["iteration"] = iteration.ToString();

                tasks.Add(_requestBuilder
                    .BuildRequest(request, variables)
                    .SendAsync(cancellationToken)
                    .ContinueWith(
                        static (_, state) =>
                        {
                            if (state is IProgress progress)
                            {
                                progress.Increment();
                            }
                        },
                        progress,
                        cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    private async Task<ProcessorResult> ExecuteSynchronousRequestAsync(
        RequestDefinition request,
        Dictionary<string, string> variables,
        IProgress? progress,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.MaxRequests; i++)
        {
            var iteration = i + 1;
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                variables["iteration"] = iteration.ToString();

                var response = await _requestBuilder
                    .BuildRequest(request, variables)
                    .SendAsync(cancellationToken);

                var result = await ProcessSynchronousResponseAsync(response, request, variables, cancellationToken);
                if (result.HasErrors)
                {
                    return result;
                }
            }
            catch (HttpRequestException)
            {
                // The profiler will log the error.
                return ProcessorResult.Fail();
            }
            finally
            {
                progress?.Increment();
            }
        }

        return ProcessorResult.Success();
    }

    private async Task<ProcessorResult> ProcessSynchronousResponseAsync(
        HttpResponseMessage response,
        RequestDefinition definition,
        Dictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (definition.Response is null)
        {
            return ProcessorResult.Success();
        }

        var expectedStatusCode = definition.Response.StatusCode;
        if ((int)response.StatusCode != expectedStatusCode)
        {
            _logger.LogStatusCodeMismatch(definition.Name, response.StatusCode, expectedStatusCode);
            return ProcessorResult.Fail($"Request '{definition.Name}' failed with status code {response.StatusCode}. Expected {expectedStatusCode}");
        }

        try
        {
            // Extract variables from headers
            var result = ExtractHeaderVariables(response, definition, variables);
            if (result.HasErrors)
            {
                return result;
            }

            // Extract variables from content
            return await ExtractContentVariablesAsync(response, definition, variables, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogResponseProcessingFailed(ex, definition.Name, ex.Message);
            return ProcessorResult.Fail($"Failed to process response for warmup request '{definition.Name}': {ex.Message}");
        }
    }

    private ProcessorResult ExtractHeaderVariables(HttpResponseMessage response, RequestDefinition definition, IDictionary<string, string> variables)
    {
        if (definition.Response?.Headers is null)
        {
            return ProcessorResult.Success();
        }

        _logger.LogExtractingHeaderVariables(definition.Name);

        foreach (var extraction in definition.Response.Headers)
        {
            if (!response.Headers.TryGetValues(extraction.Key, out var values))
            {
                _logger.LogHeaderExtractionFailed(extraction.Key);
                return ProcessorResult.Fail($"Failed to extract '{extraction.Key}' from '{definition.Name}' response headers.");
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

        return ProcessorResult.Success();
    }

    private async Task<ProcessorResult> ExtractContentVariablesAsync(
        HttpResponseMessage response,
        RequestDefinition definition,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (definition.Response?.Content is null)
        {
            return ProcessorResult.Success();
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
                    return ProcessorResult.Fail($"Failed to extract '{extraction.Key}' from '{definition.Name}' JSON response.");
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

            return ProcessorResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogContentProcessingFailed(ex, definition.Name, ex.Message);
            return ProcessorResult.Fail($"Failed to read response content of '{definition.Name}': {ex.Message}");
        }
    }
}

internal static partial class RequestProcessorLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Executing request: {Name}")]
    public static partial void LogExecutingRequest(this ILogger<RequestProcessor> logger, string? name);

    [LoggerMessage(2, LogLevel.Warning, "Request '{Name}' failed with status code {StatusCode}. Expected {ExpectedStatusCode}")]
    public static partial void LogStatusCodeMismatch(this ILogger<RequestProcessor> logger, string? name, HttpStatusCode statusCode, int expectedStatusCode);

    [LoggerMessage(3, LogLevel.Error, "Failed to process response for request '{Name}': {Message}")]
    public static partial void LogResponseProcessingFailed(this ILogger<RequestProcessor> logger, Exception exception, string? name, string message);

    [LoggerMessage(4, LogLevel.Debug, "Extracting variables from headers for request '{Name}'...")]
    public static partial void LogExtractingHeaderVariables(this ILogger<RequestProcessor> logger, string? name);

    [LoggerMessage(5, LogLevel.Error, "Failed to extract header value for key '{Key}'")]
    public static partial void LogHeaderExtractionFailed(this ILogger<RequestProcessor> logger, string key);

    [LoggerMessage(6, LogLevel.Warning, "Expected to find '{Key}' variable in response headers, but it was not found.")]
    public static partial void LogExpectedHeaderVariableNotFound(this ILogger<RequestProcessor> logger, string key);

    [LoggerMessage(7, LogLevel.Debug, "Extracted variable {VariableName} = {VariableValue}")]
    public static partial void LogExtractedHeaderVariable(this ILogger<RequestProcessor> logger, string variableName, string variableValue);

    [LoggerMessage(8, LogLevel.Debug, "Extracting variables from JSON response for request '{Name}'...")]
    public static partial void LogExtractingContentVariables(this ILogger<RequestProcessor> logger, string? name);

    [LoggerMessage(9, LogLevel.Error, "Failed to extract JSON value for path '{Path}'")]
    public static partial void LogContentExtractionFailed(this ILogger<RequestProcessor> logger, string path);

    [LoggerMessage(10, LogLevel.Warning, "Expected to find '{Key}' variable in response content, but it was not found.")]
    public static partial void LogExpectedContentVariableNotFound(this ILogger<RequestProcessor> logger, string key);

    [LoggerMessage(11, LogLevel.Debug, "Extracted variable {VariableName} = {VariableValue}")]
    public static partial void LogExtractedContentVariable(this ILogger<RequestProcessor> logger, string variableName, string variableValue);

    [LoggerMessage(12, LogLevel.Error, "Failed to read JSON response content for warmup request '{Name}': {Message}")]
    public static partial void LogContentProcessingFailed(this ILogger<RequestProcessor> logger, Exception exception, string? name, string message);
}