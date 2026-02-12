namespace mPrismaMapsWPF.Services;

public class BackupInfo
{
    public required string FilePath { get; init; }
    public required string StoreId { get; init; }
    public required string Floor { get; init; }
    public required DateTime Timestamp { get; init; }
    public required long FileSizeBytes { get; init; }

    public string DisplayName => $"{StoreId} / {Floor} - {Timestamp:yyyy-MM-dd HH:mm:ss}";

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}

public interface IBackupService
{
    Task SaveBackupAsync(string storeId, string floor, string json);
    Task<List<BackupInfo>> ListBackupsAsync();
    Task<string> ReadBackupAsync(string filePath);
    Task DeleteBackupAsync(string filePath);
}
