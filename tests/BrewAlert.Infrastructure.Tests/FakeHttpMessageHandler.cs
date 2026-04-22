using System.Net;

namespace BrewAlert.Infrastructure.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
    public Exception? ThrowOnSend { get; set; }
    public int CallCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;

        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        return Task.FromResult(Response);
    }
}
