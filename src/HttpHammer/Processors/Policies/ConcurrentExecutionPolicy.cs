using System.Diagnostics;
using HttpHammer.Http;

namespace HttpHammer.Processors.Policies;

internal class ConcurrentExecutionPolicy : IExecutionPolicy
{
    private readonly IHttpRequestBuilder _requestBuilder;

    public ConcurrentExecutionPolicy(IHttpRequestBuilder requestBuilder)
    {
        _requestBuilder = requestBuilder;
    }

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var request = context.Request;

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
                context.Variables["iteration"] = iteration.ToString();

                tasks.Add(_requestBuilder
                    .BuildRequest(context.Request, context.Variables)
                    .SendAsync(cancellationToken)
                    .ContinueWith(
                        static (_, state) =>
                        {
                            var executionContext = (ExecutionContext)state!;
                            executionContext.Progress?.Increment();
                        },
                        context,
                        cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}