namespace Boostlingo.Console.Tests;

using System;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using Moq.Protected;
using Xunit;

public class ProgramTests : IDisposable
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

        Console.SetOut(ConsoleOutput);

        DatabaseConnection = new($"Data Source={DatabaseName};Mode=Memory;Cache=Shared");
        DatabaseConnection.Open();
    }

    private Mock<HttpMessageHandler> Handler { get; } = new();

    private string DatabaseName { get; } = Guid.NewGuid().ToString();

    private StringWriter ConsoleOutput { get; } = new();

    private SqliteConnection DatabaseConnection { get; }

    public void Dispose()
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        ConsoleOutput.Dispose();
        DatabaseConnection.Close();
    }

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

    [Fact]
    public async Task ProgramLoadsFetchedPersonsIntoDatabase()
    {
        await Program.Execute(Handler.Object, DatabaseName);

        var query = DatabaseConnection.CreateCommand();
        query.CommandText = """
            SELECT last_name
            FROM persons
            """;
        using var reader = query.ExecuteReader();
        var lastNames = new List<string>();
        while (reader.Read())
        {
            lastNames.Add(reader.GetString(0));
        }

        lastNames.Should().BeEquivalentTo(["Solangi", "Ghaffar"]);
    }

    [Fact]
    public async Task ProgramLoadsIncompletePersonsIntoDatabase()
    {
        SetupSampleDataRequestHandler()
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"[
  { ""name"": ""First Last"" },
  { ""name"": ""First"" },
  { ""language"": ""one"" },
  { }
]")});

        await Program.Execute(Handler.Object, DatabaseName);

        var query = DatabaseConnection.CreateCommand();
        query.CommandText = """
            SELECT COUNT(*)
            FROM persons
            """;
        query.ExecuteScalar().Should().BeAssignableTo<long>()
            .Which.Should().Be(4);

        GetConsoleOutputLines().Should().ContainMatch("*First Last*");
    }

    [Fact]
    public async Task ProgramPrintsPersonsByLastNameThenFirstName()
    {
        SetupSampleDataRequestHandler()
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"[
  { ""name"": ""Alpha Zulu"" },
  { ""name"": ""Zulu Zulu"" },
  { ""name"": ""Charlie Zulu"" },
  { ""name"": ""Zulu Alpha"" }
]")});

        await Program.Execute(Handler.Object, DatabaseName);

        var output = GetConsoleOutputLines();
        output.Reverse().Skip(1).First().Should().Contain("Zulu Zulu");
        output.Reverse().Skip(2).First().Should().Contain("Charlie Zulu");
        output.Reverse().Skip(3).First().Should().Contain("Alpha Zulu");
        output.Reverse().Skip(4).First().Should().Contain("Zulu Alpha");
    }

    [Fact]
    public async Task ProgramSupportsSortingNonAsciiNames()
    {
        SetupSampleDataRequestHandler()
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"[
  { ""name"": ""Peter Zammit"" },
  { ""name"": ""Ingibjörg Ólafsdóttir"" }
]")
            });

        await Program.Execute(Handler.Object, DatabaseName);

        var output = GetConsoleOutputLines();
        output.Reverse().Skip(1).First().Should().Contain("Peter Zammit", because: "Ó is before Z in lexicographic unicode order");
        output.Reverse().Skip(2).First().Should().Contain("Ingibjörg Ólafsdóttir");
    }

    private string[] GetConsoleOutputLines() => ConsoleOutput.ToString().Split(Environment.NewLine);

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