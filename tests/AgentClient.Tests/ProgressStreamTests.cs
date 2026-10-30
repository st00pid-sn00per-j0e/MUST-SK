using System.Net;
using System.Text;
using AgentClient.Core;
using AgentClient.Core.Http;
using AgentClient.Core.Models;
using AgentClient.Core.WebSockets;

namespace AgentClient.Tests;

public sealed class ProgressStreamTests
{
    [Fact]
    public async Task StreamAsyncReplaysOnlyNewEventsAndStopsAtTerminalSnapshot()
    {
        const string snapshot =
            """
            {
              "runId": "run-123",
              "status": "completed",
              "attempt": 1,
              "errorClass": null,
              "errorMessage": null,
              "events": [
                { "seq": 1, "type": "running", "message": "started", "at": "2026-10-30T00:00:00Z" },
                { "seq": 2, "type": "completed", "message": "finished", "at": "2026-10-30T00:00:01Z" }
              ]
            }
            """;
        var handler = new SnapshotHandler(snapshot);
        var stream = new ProgressStream(
            new HttpClient(handler) { BaseAddress = new Uri("https://worker.test") },
            new AgentClientOptions { BaseUrl = "https://worker.test" });

        var events = new List<RunEvent>();
        await foreach (var runEvent in stream.StreamAsync("run-123", 1, CancellationToken.None))
        {
            events.Add(runEvent);
        }

        Assert.Equal("/runs/run-123?since=1", handler.Path);
        Assert.Collection(events, runEvent =>
        {
            Assert.Equal(2, runEvent.Seq);
            Assert.Equal("completed", runEvent.Type);
            Assert.Equal("finished", runEvent.Message);
        });
    }

    [Fact]
    public async Task StreamAsyncSurfacesSnapshotApiErrors()
    {
        var handler = new SnapshotHandler("missing", HttpStatusCode.NotFound);
        var stream = new ProgressStream(
            new HttpClient(handler) { BaseAddress = new Uri("https://worker.test") },
            new AgentClientOptions { BaseUrl = "https://worker.test" });

        var exception = await Assert.ThrowsAsync<AgentApiException>(async () =>
        {
            await foreach (var _ in stream.StreamAsync("missing", 0, CancellationToken.None))
            {
            }
        });

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("missing", exception.Body);
    }

    private sealed class SnapshotHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
