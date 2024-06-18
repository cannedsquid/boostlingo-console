namespace Boostlingo.Console;

using Boostlingo.Console.Models;
using Microsoft.Data.Sqlite;

internal class Database : IDisposable
{
    private const string PersonsTableName = "persons";

    public Database(string? name = null)
    {
        name ??= Guid.NewGuid().ToString();
        Connection = new($"Data Source={name};Mode=Memory;Cache=Shared");
        Connection.Open();
        SetupPersonTable();
    }

    private SqliteConnection Connection { get; }

    public void Dispose()
    {
        Connection.Close();
    }

    public IEnumerable<Person> GetPersonsByName()
    {
        var query = Connection.CreateCommand();
        query.CommandText = $"""
            SELECT first_name, last_name, language, id, bio, version
            FROM {PersonsTableName}
            ORDER BY last_name, first_name
            """;
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            yield return new Person()
            {
                Name = $"{reader.GetString(0)} {reader.GetString(1)}",
                Language = reader.GetString(2),
                Id = reader.GetString(3),
                Bio = reader.GetString(4),
                Version = reader.GetDecimal(5),
            };
        }
    }

    public void InsertPerson(Person person)
    {
        var update = Connection.CreateCommand();
        update.CommandText = $"""
            INSERT INTO {PersonsTableName} (first_name, last_name, language, id, bio, version)
            VALUES ($first_name, $last_name, $language, $id, $bio, $version)
            """;
        var name = person.Name?.Split(' ', 2); // n.b.: We're assuming all inputs consist of single-word first/last name pairs
        update.Parameters.AddWithValue("$first_name", name?.Length > 0 ? name[0] : null);
        update.Parameters.AddWithValue("$last_name", name?.Length > 1 ? name[1] : null);
        update.Parameters.AddWithValue("$language", person.Language);
        update.Parameters.AddWithValue("$id", person.Id);
        update.Parameters.AddWithValue("$bio", person.Bio);
        update.Parameters.AddWithValue("$version", person.Version);
        update.ExecuteNonQuery();
    }

    private void SetupPersonTable()
    {
        var creation = Connection.CreateCommand();
        creation.CommandText = $"""
            CREATE TABLE {PersonsTableName} (
                first_name TEXT,
                last_name TEXT,
                language TEXT,
                id TEXT,
                bio TEXT,
                version TEXT
            );

            CREATE INDEX name_index ON {PersonsTableName} (last_name, first_name)
            """;
        creation.ExecuteNonQuery();
    }
}
