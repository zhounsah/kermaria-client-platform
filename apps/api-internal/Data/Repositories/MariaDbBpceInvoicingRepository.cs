using System.Security.Cryptography;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbBpceInvoicingRepository : IBpceInvoicingRepository
{
    private readonly SqlRuntimeConfiguration _configuration;

    public MariaDbBpceInvoicingRepository(SqlRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<BpceCustomerLink?> GetCustomerLinkAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                customer_id,
                bpce_customer_id,
                bpce_external_id,
                synced_at
            FROM bpce_customers
            WHERE customer_id = @customerId
            """;
        cmd.Parameters.AddWithValue("customerId", customerId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BpceCustomerLink(
            reader.GetString("customer_id"),
            reader.GetString("bpce_customer_id"),
            reader.GetString("bpce_external_id"),
            reader.GetString("synced_at"));
    }

    public async Task UpsertCustomerLinkAsync(
        string customerId,
        string bpceCustomerId,
        string bpceExternalId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bpce_customers
                (id, customer_id, bpce_customer_id, bpce_external_id, synced_at)
            VALUES
                (UUID(), @customerId, @bpceCustomerId, @bpceExternalId, NOW(6))
            ON DUPLICATE KEY UPDATE
                bpce_customer_id = @bpceCustomerId,
                bpce_external_id = @bpceExternalId,
                synced_at = NOW(6)
            """;
        cmd.Parameters.AddWithValue("customerId", customerId);
        cmd.Parameters.AddWithValue("bpceCustomerId", bpceCustomerId);
        cmd.Parameters.AddWithValue("bpceExternalId", bpceExternalId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
        string commercialDocumentId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                commercial_document_id,
                bpce_invoice_id,
                bpce_customer_id,
                status,
                fiscal_number,
                issue_date,
                total_amount_cents,
                currency,
                pdf_hash,
                created_at,
                validated_at
            FROM bpce_invoices
            WHERE commercial_document_id = @documentId
            """;
        cmd.Parameters.AddWithValue("documentId", commercialDocumentId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BpceInvoiceRecord(
            reader.GetString("commercial_document_id"),
            reader.GetString("bpce_invoice_id"),
            reader.GetString("bpce_customer_id"),
            reader.GetString("status"),
            reader.IsDBNull(reader.GetOrdinal("fiscal_number"))
                ? null
                : reader.GetString("fiscal_number"),
            reader.GetString("issue_date"),
            reader.GetInt32("total_amount_cents"),
            reader.GetString("currency"),
            reader.IsDBNull(reader.GetOrdinal("pdf_hash"))
                ? null
                : reader.GetString("pdf_hash"),
            reader.GetString("created_at"),
            reader.IsDBNull(reader.GetOrdinal("validated_at"))
                ? null
                : reader.GetString("validated_at"));
    }

    public async Task CreateInvoiceRecordAsync(
        string commercialDocumentId,
        string bpceInvoiceId,
        string bpceCustomerId,
        string issueDate,
        int totalAmountCents,
        string currency,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bpce_invoices (
                id,
                commercial_document_id,
                bpce_invoice_id,
                bpce_customer_id,
                status,
                fiscal_number,
                issue_date,
                total_amount_cents,
                currency,
                pdf_hash,
                pdf_content,
                created_at,
                validated_at
            ) VALUES (
                UUID(),
                @documentId,
                @bpceInvoiceId,
                @bpceCustomerId,
                'draft',
                NULL,
                @issueDate,
                @totalAmountCents,
                @currency,
                NULL,
                NULL,
                NOW(6),
                NULL
            )
            """;
        cmd.Parameters.AddWithValue("documentId", commercialDocumentId);
        cmd.Parameters.AddWithValue("bpceInvoiceId", bpceInvoiceId);
        cmd.Parameters.AddWithValue("bpceCustomerId", bpceCustomerId);
        cmd.Parameters.AddWithValue("issueDate", issueDate);
        cmd.Parameters.AddWithValue("totalAmountCents", totalAmountCents);
        cmd.Parameters.AddWithValue("currency", currency);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateInvoiceValidatedAsync(
        string commercialDocumentId,
        string? fiscalNumber,
        string status,
        byte[]? pdfContent,
        string? pdfHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE bpce_invoices SET
                fiscal_number   = @fiscalNumber,
                status          = @status,
                pdf_hash        = @pdfHash,
                pdf_content     = @pdfContent,
                validated_at    = NOW(6)
            WHERE commercial_document_id = @documentId
            """;
        cmd.Parameters.AddWithValue("documentId", commercialDocumentId);
        cmd.Parameters.AddWithValue(
            "fiscalNumber",
            (object?)fiscalNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue(
            "pdfHash",
            (object?)pdfHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "pdfContent",
            (object?)pdfContent ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<byte[]?> GetInvoicePdfAsync(
        string commercialDocumentId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT pdf_content
            FROM bpce_invoices
            WHERE commercial_document_id = @documentId
            """;
        cmd.Parameters.AddWithValue("documentId", commercialDocumentId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is DBNull or null
            ? null
            : (byte[])result;
    }
}
