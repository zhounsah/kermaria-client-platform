using System.Text;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

internal static class MariaDbIdentifierReader
{
    public static string ReadRequired(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return ConvertRequiredValue(reader.GetValue(ordinal), columnName);
    }

    public static string? ReadNullable(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return ConvertNullableValue(reader.GetValue(ordinal), columnName);
    }

    internal static string ConvertRequiredValue(
        object? value,
        string columnName)
    {
        return ConvertNullableValue(value, columnName)
            ?? throw new InvalidDataException(
                $"MariaDB identifier column '{columnName}' cannot be null.");
    }

    internal static string? ConvertNullableValue(
        object? value,
        string columnName)
    {
        return value switch
        {
            null or DBNull => null,
            Guid guid => guid.ToString("D"),
            string text when !string.IsNullOrWhiteSpace(text) => text,
            byte[] bytes => ConvertBytes(bytes, columnName),
            _ => throw new InvalidDataException(
                $"MariaDB identifier column '{columnName}' has unsupported CLR type '{value.GetType().FullName}'.")
        };
    }

    private static string ConvertBytes(
        byte[] bytes,
        string columnName)
    {
        if (bytes.Length == 16)
        {
            return new Guid(bytes).ToString("D");
        }

        string text;

        try
        {
            text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"MariaDB identifier column '{columnName}' contains an unsupported binary value.",
                exception);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        throw new InvalidDataException(
            $"MariaDB identifier column '{columnName}' contains an unsupported empty binary value.");
    }
}
