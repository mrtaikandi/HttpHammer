using System.Net;

namespace HttpHammer.Tests.Infrastructure;

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responseQueue = new();
    private HttpResponseMessage? _response;

    public Exception? ExceptionToThrow { get; set; }

    public HttpRequestMessage? LastRequest => Requests.LastOrDefault();

    public List<string> RequestContents { get; } = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public bool ShouldThrowException { get; set; }

    public void SetResponse(HttpResponseMessage response)
    {
        _response = response;
        _responseQueue.Clear();
    }

    public void SetResponseQueue(IEnumerable<HttpResponseMessage> responses)
    {
        _responseQueue.Clear();
        foreach (var response in responses)
        {
            _responseQueue.Enqueue(response);
        }

        _response = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Check for cancellation
        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException("The operation was canceled.");
        }

        // Simulate network exceptions
        if (ShouldThrowException && ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        Requests.Add(request);

        // Capture content before it gets disposed
        if (request.Content != null)
        {
            RequestContents.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        if (_responseQueue.Count > 0)
        {
            return _responseQueue.Dequeue();
        }

        return _response ?? new HttpResponseMessage(HttpStatusCode.OK);
    }
}