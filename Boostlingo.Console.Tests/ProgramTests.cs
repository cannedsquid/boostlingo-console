namespace Boostlingo.Console.Tests;

using System.Net;
using Moq;
using Moq.Protected;
using Xunit;

public class ProgramTests
{
    private static readonly Uri SampleSource = new("https://microsoftedge.github.io/Demos/json-dummy-data/64KB.json");

    private static readonly string SampleData = @"[
  {
    ""name"": ""Adeel Solangi"",
    ""language"": ""Sindhi"",
    ""id"": ""V59OF92YF627HFY0"",
    ""bio"": ""Donec lobortis eleifend condimentum. Cras dictum dolor lacinia lectus vehicula rutrum. Maecenas quis nisi nunc. Nam tristique feugiat est vitae mollis. Maecenas quis nisi nunc."",
    ""version"": 6.1
  },
  {
    ""name"": ""Afzal Ghaffar"",
    ""language"": ""Sindhi"",
    ""id"": ""ENTOCR13RSCLZ6KU"",
    ""bio"": ""Aliquam sollicitudin ante ligula, eget malesuada nibh efficitur et. Pellentesque massa sem, scelerisque sit amet odio id, cursus tempor urna. Etiam congue dignissim volutpat. Vestibulum pharetra libero et velit gravida euismod."",
    ""version"": 1.88
  }
]";

    public ProgramTests()
    {
        SetupSampleDataRequestHandler()
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(SampleData),
            });
    }

    private Mock<HttpMessageHandler> Handler { get; } = new(MockBehavior.Strict);

    [Fact]
    public async Task ProgramRequestsFileFromCorrectUri()
    {
        await Program.Execute(Handler.Object);

        VerifySampleDataRequests(Times.Once());
    }

    [Fact]
    public async Task ProgramRetriesFileRequestToAvoidTransientFailures()
    {
        Handler.Protected().SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Get && m.RequestUri == SampleSource),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(SampleData),
            });

        await Program.Execute(Handler.Object);

        VerifySampleDataRequests(Times.Exactly(2));
    }

    [Fact]
    public async Task ProgramRetriesFetchingFileIfMalformed()
    {
        SetupSampleDataRequestHandler()
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(SampleData[0..^1]),
            });

        await Program.Execute(Handler.Object);

        VerifySampleDataRequests(Times.AtLeast(3));
    }

    [Fact]
    public async Task ProgramTerminatesIfFileCannotBeFetched()
    {
        SetupSampleDataRequestHandler()
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Program.Execute(Handler.Object);

        VerifySampleDataRequests(Times.AtLeast(3));
    }

    private Moq.Language.Flow.ISetup<HttpMessageHandler, Task<HttpResponseMessage>> SetupSampleDataRequestHandler() =>
        Handler.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Get && m.RequestUri == SampleSource),
            ItExpr.IsAny<CancellationToken>());

    private void VerifySampleDataRequests(Times times) => Handler.Protected().Verify(
        "SendAsync",
        times,
        ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Get && m.RequestUri == SampleSource),
        ItExpr.IsAny<CancellationToken>());
}