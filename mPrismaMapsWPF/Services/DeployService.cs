using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace mPrismaMapsWPF.Services;

public class DeployService : IDeployService
{
    private readonly ILogger<DeployService> _logger;

    public DeployService(ILogger<DeployService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> HasMapAsync(string connectionString, string storeId, string floor)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Maps WHERE StoreId = @StoreId AND [Floor] = @Floor", connection);
        cmd.Parameters.AddWithValue("@StoreId", storeId);
        cmd.Parameters.AddWithValue("@Floor", floor);

        var count = (int)(await cmd.ExecuteScalarAsync())!;
        _logger.LogDebug("HasMap check for {StoreId}/{Floor}: {Count}", storeId, floor, count);
        return count > 0;
    }

    public async Task<string?> GetMapAsync(string connectionString, string storeId, string floor)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT MappingData FROM Maps WHERE StoreId = @StoreId AND [Floor] = @Floor", connection);
        cmd.Parameters.AddWithValue("@StoreId", storeId);
        cmd.Parameters.AddWithValue("@Floor", floor);

        var result = await cmd.ExecuteScalarAsync();
        if (result is string json)
        {
            _logger.LogDebug("Retrieved existing map data for {StoreId}/{Floor} ({Length} chars)", storeId, floor, json.Length);
            return json;
        }

        _logger.LogDebug("No existing map data found for {StoreId}/{Floor}", storeId, floor);
        return null;
    }

    public async Task<(bool success, string? errorMessage)> DeployMapAsync(
        string connectionString, string storeId, string floor, string mappingDataJson)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                bool exists = await HasMapInternalAsync(connection, transaction, storeId, floor);

                if (exists)
                {
                    await using var updateCmd = new SqlCommand(
                        "UPDATE Maps SET MappingData = @MappingData WHERE StoreId = @StoreId AND [Floor] = @Floor",
                        connection, transaction);
                    updateCmd.Parameters.AddWithValue("@MappingData", mappingDataJson);
                    updateCmd.Parameters.AddWithValue("@StoreId", storeId);
                    updateCmd.Parameters.AddWithValue("@Floor", floor);
                    await updateCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Updated existing map for {StoreId}/{Floor}", storeId, floor);
                }
                else
                {
                    await using var insertCmd = new SqlCommand(
                        "INSERT INTO Maps (StoreId, [Floor], MappingData) VALUES (@StoreId, @Floor, @MappingData)",
                        connection, transaction);
                    insertCmd.Parameters.AddWithValue("@StoreId", storeId);
                    insertCmd.Parameters.AddWithValue("@Floor", floor);
                    insertCmd.Parameters.AddWithValue("@MappingData", mappingDataJson);
                    await insertCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Inserted new map for {StoreId}/{Floor}", storeId, floor);
                }

                await transaction.CommitAsync();
                return (true, null);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error deploying map for {StoreId}/{Floor}", storeId, floor);
            return (false, $"Database error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying map for {StoreId}/{Floor}", storeId, floor);
            return (false, $"Error: {ex.Message}");
        }
    }

    private static async Task<bool> HasMapInternalAsync(SqlConnection connection, SqlTransaction transaction, string storeId, string floor)
    {
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Maps WHERE StoreId = @StoreId AND [Floor] = @Floor",
            connection, transaction);
        cmd.Parameters.AddWithValue("@StoreId", storeId);
        cmd.Parameters.AddWithValue("@Floor", floor);

        var count = (int)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }
}
