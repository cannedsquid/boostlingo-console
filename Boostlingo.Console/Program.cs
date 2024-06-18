namespace Boostlingo.Console;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using Boostlingo.Console.Models;

public class Program
{
    private static readonly Uri JsonDummyDataSource = new("https://microsoftedge.github.io/Demos/json-dummy-data/64KB.json");

    public static async Task<int> Main() => await Execute();

    public static async Task<int> Execute(HttpMessageHandler? httpMessageHandler = null, CancellationToken cancellationToken = default)
    {
        var persons = await GetPersons(httpMessageHandler, cancellationToken);
        // Read the below Json File directly from the specified URL https://microsoftedge.github.io/Demos/json-dummy-data/64KB.json
        if (persons.Count == 0)
        {
            Console.Error.WriteLine("No Persons fetched, aborting");
            return 1;
        }

        // Read and parse the content of the file and insert it into a Database Table using the Relational Database of your choice.
        // Read the content of the Database Table in which the data has been stored and output its content to the Console(STDOUT) by Sorting by Person’s Last Name Then by First Name.

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
