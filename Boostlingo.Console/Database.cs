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
        Connection.CreateCollation("NOCASE", (a, b) => string.Compare(a, b, StringComparison.InvariantCultureIgnoreCase));
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
            ORDER BY last_name COLLATE NOCASE, first_name COLLATE NOCASE
            """;
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            string? GetNullableString(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
            yield return new Person()
            {
                Name = $"{GetNullableString(0)} {GetNullableString(1)}",
                Language = GetNullableString(2),
                Id = GetNullableString(3),
                Bio = GetNullableString(4),
                Version = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
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

        void AddNullableParameterValue(string? parameterName, object? value) => update.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
        var name = person.Name?.Split(' ', 2); // n.b.: We're assuming all inputs consist of single-word first/last name pairs
        AddNullableParameterValue("$first_name", name?.Length > 0 ? name[0] : null);
        AddNullableParameterValue("$last_name", name?.Length > 1 ? name[1] : null);
        AddNullableParameterValue("$language", person.Language);
        AddNullableParameterValue("$id", person.Id);
        AddNullableParameterValue("$bio", person.Bio);
        AddNullableParameterValue("$version", person.Version);
        update.ExecuteNonQuery();
    }

    private void SetupPersonTable()
    {
        var creation = Connection.CreateCommand();
        creation.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {PersonsTableName} (
                first_name TEXT,
                last_name TEXT,
                language TEXT,
                id TEXT,
                bio TEXT,
                version TEXT
            );

            CREATE INDEX IF NOT EXISTS name_index ON {PersonsTableName} (
                last_name COLLATE NOCASE,
                first_name COLLATE NOCASE
            )
            """;
        creation.ExecuteNonQuery();
    }
}
