using HttpHammer.Http;

namespace HttpHammer.Processors.Policies;

internal class SynchronousExecutionPolicy : IExecutionPolicy
{
    private readonly IHttpRequestBuilder _requestBuilder;

    public SynchronousExecutionPolicy(IHttpRequestBuilder requestBuilder)
    {
        _requestBuilder = requestBuilder;
    }

    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var request = context.Request;

        for (var i = 0; i < request.MaxRequests; i++)
        {
            var iteration = i + 1;
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                context.Variables["iteration"] = iteration.ToString();

                await _requestBuilder
                    .BuildRequest(request, context.Variables)
                    .SendAsync(cancellationToken);
            }
            finally
            {
                context.Progress?.Increment();
            }
        }
    }
}