namespace Boostlingo.Console;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Boostlingo.Console.Models;

public class Program
{
    private static readonly Uri JsonDummyDataSource = new("https://microsoftedge.github.io/Demos/json-dummy-data/64KB.json");

    public static async Task<int> Main() => await Execute();

    public static async Task<int> Execute(HttpMessageHandler? httpMessageHandler = null, CancellationToken cancellationToken = default)
    {
        var persons = await GetPersons(httpMessageHandler, cancellationToken);
        if (persons.Count == 0)
        {
            Console.Error.WriteLine("No Persons fetched, aborting");
            return 1;
        }

        using var database = new Database();
        foreach (var person in persons)
        {
            database.InsertPerson(person);
        }

        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        foreach (var person in database.GetPersonsByName())
        {
            Console.WriteLine(JsonSerializer.Serialize(person, jsonOptions));
        }

        return 0;
    }

    private static async Task<IReadOnlyCollection<Person>> GetPersons(HttpMessageHandler? httpMessageHandler = null, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 3;
        var attempt = 0;

        using HttpClient client = httpMessageHandler == null ? new() : new(httpMessageHandler);
        while (attempt < maxAttempts)
        {
            if (attempt > 0)
            {
                await Task.Delay((2 ^ attempt) * 1_000, cancellationToken);
            }

            try
            {
                return await client.GetFromJsonAsync<List<Person>>(JsonDummyDataSource, cancellationToken)
                    ?? throw new Exception("Unexpected failure fetching data");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not fetch sample data: {ex}");
                attempt++;
            }
        }

        return [];
    }
}
