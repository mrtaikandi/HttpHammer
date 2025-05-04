using System.Diagnostics;
using HttpHammer.Console;
using HttpHammer.Processors.Policies;
using Microsoft.Extensions.Logging;
using ExecutionContext = HttpHammer.Processors.Policies.ExecutionContext;

namespace HttpHammer.Processors;

public class RequestProcessor : IProcessor
{
    private readonly ILogger<RequestProcessor> _logger;
    private readonly IExecutionPolicyFactory _executionPolicyFactory;

    public RequestProcessor(ILogger<RequestProcessor> logger, IExecutionPolicyFactory executionPolicyFactory)
    {
        _logger = logger;
        _executionPolicyFactory = executionPolicyFactory;
    }

    /// <inheritdoc />
    public int Order => 2;

    /// <inheritdoc />
    public async ValueTask<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        var plan = context.ExecutionPlan;
        if (plan.Requests.Length == 0)
        {
            return ProcessorResult.Fail("No requests to execute.");
        }

        var tasks = new List<Task>(plan.Requests.Length);
        foreach (var request in plan.Requests.Where(r => r.MaxRequests > 0))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var progress = context.Progress.Create(request.Name, request.MaxRequests ?? 1);
            var executionContext = new ExecutionContext(request, plan.Variables, progress);

            tasks.Add(
                ExecuteAsync(executionContext, cancellationToken)
                    .ContinueWith(static (_, state) => ((IProgress)state!).Complete(), progress, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return ProcessorResult.Success(plan);
    }

    private async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (context.Request.MaxRequests == 0)
            {
                return;
            }

            _logger.LogExecutingRequest(context.Request.Name);

            var execution = _executionPolicyFactory.Create(context.Request);
            await execution.ExecuteAsync(context, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Do nothing
        }
        catch (Exception ex)
        {
            _logger.LogRequestExecutionError(ex, context.Request.Name);
        }
    }
}

internal static partial class RequestProcessorLogs
{
    [LoggerMessage(0, LogLevel.Debug, "Executing request: {Name}")]
    public static partial void LogExecutingRequest(this ILogger<RequestProcessor> logger, string? name);

    [LoggerMessage(1, LogLevel.Error, "Error executing request: {Name}")]
    public static partial void LogRequestExecutionError(this ILogger<RequestProcessor> logger, Exception exception, string? name);
}