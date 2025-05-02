using System.Globalization;
using System.Text;
using HttpHammer.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HttpHammer.Http;

/// <summary>
/// A delegating handler that measures HTTP request duration.
/// </summary>
internal class HttpRequestProfilingHandler : DelegatingHandler
{
    private readonly ILogger<HttpRequestProfilingHandler> _logger;
    private readonly IProfiler _profiler;

    public HttpRequestProfilingHandler(ILogger<HttpRequestProfilingHandler> logger, IProfiler profiler)
    {
        _logger = logger;
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestName = GetRequestName(request);
        var startTime = _profiler.Start();
        StringBuilder? error = null;

        try
        {
            _logger.LogSendingRequest(requestName);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            error = new StringBuilder();
            error.AppendFormat(
                CultureInfo.CurrentUICulture,
                "{0} ({1})",
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(response.ReasonPhrase) ? response.StatusCode.ToString() : response.ReasonPhrase);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(content))
            {
                error.AppendLine().Append(content);
            }

            return response;
        }
        finally
        {
            var duration = _profiler.Record(requestName, startTime, error?.ToString());
            _logger.LogRequestCompleted(requestName, duration.TotalMilliseconds);
        }
    }

    private static string GetRequestName(HttpRequestMessage request)
    {
        request.Options.TryGetValue(new HttpRequestOptionsKey<string?>("RequestDefinitionName"), out var requestName);
        if (string.IsNullOrWhiteSpace(requestName))
        {
            throw new InvalidOperationException("Request definition name is not set.");
        }

        return requestName;
    }
}

internal static partial class HttpRequestTimingHandlerLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Sending request '{RequestName}'")]
    public static partial void LogSendingRequest(this ILogger<HttpRequestProfilingHandler> logger, string requestName);

    [LoggerMessage(1, LogLevel.Debug, "Request '{RequestName}' completed in {Duration} ms")]
    public static partial void LogRequestCompleted(this ILogger<HttpRequestProfilingHandler> logger, string requestName, double duration);
}