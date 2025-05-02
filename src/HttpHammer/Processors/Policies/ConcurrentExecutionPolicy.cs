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
        var batchSize = Math.Min(request.ConcurrentConnections, request.MaxRequests);

        for (var offset = 0; offset < request.MaxRequests; offset += batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var currentBatchSize = Math.Min(batchSize, request.MaxRequests - offset);
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