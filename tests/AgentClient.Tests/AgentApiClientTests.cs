using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentClient.Core.Http;
using AgentClient.Core.Models;

namespace AgentClient.Tests;

public sealed class AgentApiClientTests
{
    [Fact]
    public async Task CreateRunAsyncPostsWorkflowAndReturnsRunDetails()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/runs", request.RequestUri!.AbsolutePath);
            return JsonResponse("""{"runId":"run-123","uploadUrl":"https://upload.test/run-123","uploadMethod":"PUT"}""");
        });
        var client = new AgentApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://worker.test") });

        var result = await client.CreateRunAsync(new CreateRunRequest("stalls", 14), CancellationToken.None);

        Assert.Equal("run-123", result.RunId);
        Assert.Equal("https://upload.test/run-123", result.UploadUrl);
        using var requestBody = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("stalls", requestBody.RootElement.GetProperty("mode").GetString());
        Assert.Equal(14, requestBody.RootElement.GetProperty("thresholdDays").GetInt32());
    }

    [Fact]
    public async Task StartCancelAndSnapshotUseExpectedRoutes()
    {
        var requests = new List<(HttpMethod Method, string Path)>();
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.PathAndQuery));
            if (request.RequestUri.AbsolutePath == "/runs/run-123")
            {
                return JsonResponse(
                    """{"runId":"run-123","status":"running","attempt":1,"errorClass":null,"errorMessage":null,"events":[]}""");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new AgentApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://worker.test") });

        await client.StartRunAsync("run-123", CancellationToken.None);
        await client.CancelRunAsync("run-123", CancellationToken.None);
        var snapshot = await client.GetRunAsync("run-123", 4, CancellationToken.None);

        Assert.Equal(
            [
                (HttpMethod.Post, "/runs/run-123/start"),
                (HttpMethod.Post, "/runs/run-123/cancel"),
                (HttpMethod.Get, "/runs/run-123?since=4"),
            ],
            requests);
        Assert.Equal("running", snapshot.Status);
    }

    [Fact]
    public async Task ApiErrorsPreserveStatusAndResponseBody()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"Choose a workflow."}"""),
            });
        var client = new AgentApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://worker.test") });

        var exception = await Assert.ThrowsAsync<AgentApiException>(
            () => client.StartRunAsync("run-123", CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("Choose a workflow", exception.Body);
    }

    [Fact]
    public async Task DownloadOutputReturnsCsvBytes()
    {
        const string csv = "candidate,stage\r\nAda,Interview\r\n";
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("/runs/run-123/outputs/cleaned.csv", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csv),
            };
        });
        var client = new AgentApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://worker.test") });

        var result = await client.DownloadOutputAsync("run-123", "cleaned.csv", CancellationToken.None);

        Assert.Equal(csv, System.Text.Encoding.UTF8.GetString(result));
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return respond(request, cancellationToken);
        }
    }
}
