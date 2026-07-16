using System.Net;

namespace Wrapsfer.Application.Tests.Shopee.Infrastructure;

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void EnqueueJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    public void EnqueueBytes(
        byte[] bytes, string contentType = "application/pdf", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) }
            }
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory = _responses.Dequeue();
        return Task.FromResult(responseFactory(request));
    }
}
