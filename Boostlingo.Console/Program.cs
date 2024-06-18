namespace Boostlingo.Console;

using System;

public class Program
{
    private static readonly Uri JsonDummyDataSource = new("https://microsoftedge.github.io/Demos/json-dummy-data/64KB.json");

    public static async Task Main() => await Execute();

    public static async Task Execute(HttpMessageHandler? httpMessageHandler = null, CancellationToken cancellationToken = default)
    {
        using HttpClient client = httpMessageHandler == null ? new() : new(httpMessageHandler);
        // Read the below Json File directly from the specified URL https://microsoftedge.github.io/Demos/json-dummy-data/64KB.json
        // Read and parse the content of the file and insert it into a Database Table using the Relational Database of your choice.
        // Read the content of the Database Table in which the data has been stored and output its content to the Console(STDOUT) by Sorting by Person’s Last Name Then by First Name.
    }
}
