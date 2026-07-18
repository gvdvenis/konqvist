using Microsoft.Data.SqlClient;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Allowlisted connection-target fields extracted from a gameplay-state
///   persistence connection string (#21). Only <see cref="Server"/> (DataSource),
///   <see cref="Database"/> (InitialCatalog), and <see cref="Encrypt"/> are
///   captured; credentials, tokens, and the full connection string are never
///   stored or logged.
/// </summary>
internal sealed record GameplayStateConnectionInfo(string? Server, string? Database, string? Encrypt)
{
    /// <summary>
    ///   Parses only the allowlisted fields from <paramref name="connectionString"/>.
    ///   Returns an instance with null fields when the string is empty/unparseable.
    /// </summary>
    public static GameplayStateConnectionInfo FromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new GameplayStateConnectionInfo(null, null, null);

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return new GameplayStateConnectionInfo(
                Server: string.IsNullOrWhiteSpace(builder.DataSource) ? null : builder.DataSource,
                Database: string.IsNullOrWhiteSpace(builder.InitialCatalog) ? null : builder.InitialCatalog,
                Encrypt: builder["Encrypt"]?.ToString());
        }
        catch (ArgumentException)
        {
            // Unparseable connection string: do not surface any fields.
            return new GameplayStateConnectionInfo(null, null, null);
        }
    }
}
