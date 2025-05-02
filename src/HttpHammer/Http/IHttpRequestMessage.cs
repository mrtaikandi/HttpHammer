namespace HttpHammer.Http;

public interface IHttpRequestMessage
{
    Task<HttpResponseMessage> SendAsync(CancellationToken cancellationToken = default);
}