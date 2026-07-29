using System.Text.Json;
using MySqlConnector;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: Kermaria.VerifyMariaDb <config.json>");
    return 2;
}

try
{
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
    var root = document.RootElement;
    string Required(string name) =>
        root.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Missing configuration key: {name}");

    var builder = new MySqlConnectionStringBuilder
    {
        Server = Required("SQL_HOST"),
        Port = uint.Parse(Required("SQL_PORT")),
        Database = Required("SQL_DATABASE"),
        UserID = Required("SQL_USERNAME"),
        Password = Required("SQL_PASSWORD"),
        CharacterSet = "utf8mb4",
        ConnectionTimeout = 5,
        DefaultCommandTimeout = 15,
        SslMode = MySqlSslMode.Preferred,
    };

    await using var connection = new MySqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DATABASE(), CURRENT_USER(), VERSION()";
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        connected = true,
        database = reader.GetString(0),
        currentUser = reader.GetString(1),
        serverVersion = reader.GetString(2),
    }));
    return 0;
}
catch (MySqlException exception)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        connected = false,
        errorNumber = exception.Number,
        sqlState = exception.SqlState,
        message = exception.Message,
    }));
    return 1;
}
catch (Exception exception)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        connected = false,
        exceptionType = exception.GetType().Name,
        message = exception.Message,
    }));
    return 1;
}
