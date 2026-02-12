namespace mPrismaMapsWPF.Services;

public interface IDeployService
{
    Task<bool> HasMapAsync(string connectionString, string storeId, string floor);
    Task<string?> GetMapAsync(string connectionString, string storeId, string floor);
    Task<(bool success, string? errorMessage)> DeployMapAsync(string connectionString, string storeId, string floor, string mappingDataJson);
}
