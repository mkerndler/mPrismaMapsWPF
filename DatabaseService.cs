using CommunityToolkit.Maui.Alerts;
using Microsoft.Data.SqlClient;
using MPrismaMaps.Resources.Languages;
using Serilog;

namespace MPrismaMaps.src.Utility
{
    public sealed class DatabaseService
    {
        public static readonly string ConnectionString = $"data source={SOURCE};User ID={USERNAME};Password={PASSWORD};Encrypt=false;initial catalog=MPOL_Maps;";

        public static readonly bool IsDebug = false;

        public static async Task<bool> HasMap(string storeId, string floor)
        {
            try
            {
                using SqlConnection sqlConnection = new(ConnectionString);
                sqlConnection.Open();

                const string query = "SELECT COUNT(*) FROM Maps WHERE StoreId = @StoreId AND [Floor] = @Floor";

                using SqlCommand sqlCommand = new(query, sqlConnection);
                _ = sqlCommand.Parameters.AddWithValue("@StoreId", storeId.ToString());
                _ = sqlCommand.Parameters.AddWithValue("@Floor", floor.ToString());

                using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
                if (sqlDataReader.Read())
                {
                    int count = sqlDataReader.GetInt32(0);
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<DatabaseService>().Error(ex, "Error while selecting entries from database.");
                await Toast.Make(AppResources.DatabaseError).Show();
            }

            return false;
        }

        public static async Task<string> GetMap(string storeId, string floor)
        {
            try
            {
                // Use a using statement to ensure the SqlConnection is properly disposed.
                using SqlConnection sqlConnection = new(ConnectionString);
                await sqlConnection.OpenAsync();

                // Parameterized query to prevent SQL injection.
                // We select the MappingData column directly.
                const string query = "SELECT MappingData FROM Maps WHERE StoreId = @StoreId AND [Floor] = @Floor";

                using SqlCommand sqlCommand = new(query, sqlConnection);
                // Add parameters to the SqlCommand.
                _ = sqlCommand.Parameters.AddWithValue("@StoreId", storeId);
                _ = sqlCommand.Parameters.AddWithValue("@Floor", floor);

                // Execute the query and read the result.
                using SqlDataReader sqlDataReader = await sqlCommand.ExecuteReaderAsync();

                // Check if any rows were returned.
                if (await sqlDataReader.ReadAsync())
                {
                    // Retrieve the string data from the MappingData column (index 0).
                    // Check for DBNull to handle cases where the field might be null in the database.
                    if (!sqlDataReader.IsDBNull(0))
                    {
                        return sqlDataReader.GetString(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<DatabaseService>().Error(ex, "Error retrieving map data for StoreId: {StoreId}, Floor: {Floor}", storeId, floor);
                await Toast.Make(AppResources.DatabaseError).Show();
            }

            return null;
        }

        public static async Task<bool> UpdateMap(string storeId, string floor, string mappingData)
        {
            if (IsDebug) return true;

            try
            {
                using SqlConnection sqlConnection = new(ConnectionString);
                sqlConnection.Open();

                using SqlTransaction sqlTransaction = sqlConnection.BeginTransaction();
                try
                {
                    const string query = "UPDATE Maps SET MappingData = @MappingData WHERE StoreId = @StoreId AND [Floor] = @Floor";
                    using SqlCommand sqlCommand = new(query, sqlConnection, sqlTransaction);
                    _ = sqlCommand.Parameters.AddWithValue("@MappingData", mappingData);
                    _ = sqlCommand.Parameters.AddWithValue("@StoreId", storeId);
                    _ = sqlCommand.Parameters.AddWithValue("@Floor", floor);

                    int rowsAffected = sqlCommand.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        sqlTransaction.Commit();
                        return true;
                    }
                    else
                    {
                        sqlTransaction.Rollback();
                    }
                }
                catch
                {
                    sqlTransaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<DatabaseService>().Error(ex, "Error while updating entries in database.");
                await Toast.Make(AppResources.DatabaseError).Show();
            }
            return false;
        }

        public static async Task<bool> InsertMap(string storeID, string floor, string MappingData)
        {
            if (IsDebug) return true;

            try
            {
                using SqlConnection sqlConnection = new(ConnectionString);
                sqlConnection.Open();

                using SqlTransaction sqlTransaction = sqlConnection.BeginTransaction();
                try
                {
                    const string query = "INSERT INTO Maps (StoreId, [Floor], MappingData) VALUES (@StoreId, @Floor, @MappingData)";
                    using SqlCommand sqlCommand = new(query, sqlConnection, sqlTransaction);
                    _ = sqlCommand.Parameters.AddWithValue("@StoreId", storeID);
                    _ = sqlCommand.Parameters.AddWithValue("@Floor", floor);
                    _ = sqlCommand.Parameters.AddWithValue("@MappingData", MappingData);

                    int rowsAffected = sqlCommand.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        sqlTransaction.Commit();
                        return true;
                    }
                    else
                    {
                        sqlTransaction.Rollback();
                    }
                }
                catch
                {
                    sqlTransaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<DatabaseService>().Error(ex, "Error while inserting entries into database.");
                await Toast.Make(AppResources.DatabaseError).Show();
            }
            return false;
        }
    }
}
