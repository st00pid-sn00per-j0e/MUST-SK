using System.Net;
using System.Text;
using AgentClient.Core.Http;

namespace AgentClient.Tests;

public sealed class UploadServiceTests
{
    [Fact]
    public async Task UploadAsyncSendsCsvAndReportsProgress()
    {
        var bytes = Encoding.UTF8.GetBytes("message\r\nHello\r\n");
        var path = Path.GetTempFileName();
        await File.WriteAllBytesAsync(path, bytes);
        var progress = new RecordingProgress();
        var handler = new UploadHandler();
        var service = new UploadService(new HttpClient(handler));

        try
        {
            await service.UploadAsync(path, "https://worker.test/upload/run-1", "PUT", progress, CancellationToken.None);

            Assert.Equal(HttpMethod.Put, handler.Method);
            Assert.Equal("https://worker.test/upload/run-1", handler.Url);
            Assert.Equal(bytes, handler.Body);
            Assert.Contains(bytes.Length, progress.Values);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UploadAsyncSurfacesApiErrorDetails()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "message\r\nHello\r\n");
        var service = new UploadService(new HttpClient(new UploadHandler(HttpStatusCode.RequestEntityTooLarge, "too large")));

        try
        {
            var exception = await Assert.ThrowsAsync<AgentApiException>(
                () => service.UploadAsync(path, "https://worker.test/upload/run-1", "PUT", null, CancellationToken.None));

            Assert.Equal(413, exception.StatusCode);
            Assert.Equal("too large", exception.Body);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class RecordingProgress : IProgress<long>
    {
        public List<long> Values { get; } = [];

        public void Report(long value) => Values.Add(value);
    }

    private sealed class UploadHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? responseBody = null) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Url { get; private set; }
        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Url = request.RequestUri?.ToString();
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody ?? ""),
            };
        }
    }
}
