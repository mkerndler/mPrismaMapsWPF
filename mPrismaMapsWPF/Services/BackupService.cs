using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace mPrismaMapsWPF.Services;

public partial class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupDir;

    [GeneratedRegex(@"^(.+?)_(.+?)_(\d{4}-\d{2}-\d{2}_\d{6})\.json$")]
    private static partial Regex BackupFilenameRegex();

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger;
        _backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mPrismaMaps", "backups");
    }

    public async Task SaveBackupAsync(string storeId, string floor, string json)
    {
        Directory.CreateDirectory(_backupDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var fileName = $"{storeId}_{floor}_{timestamp}.json";
        var filePath = Path.Combine(_backupDir, fileName);

        await File.WriteAllTextAsync(filePath, json);
        _logger.LogInformation("Saved backup to {FilePath}", filePath);
    }

    public Task<List<BackupInfo>> ListBackupsAsync()
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(_backupDir))
            return Task.FromResult(backups);

        var regex = BackupFilenameRegex();

        foreach (var file in Directory.GetFiles(_backupDir, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var match = regex.Match(fileName);
            if (!match.Success)
            {
                _logger.LogDebug("Skipping malformed backup filename: {FileName}", fileName);
                continue;
            }

            if (!DateTime.TryParseExact(match.Groups[3].Value, "yyyy-MM-dd_HHmmss",
                    null, System.Globalization.DateTimeStyles.None, out var timestamp))
                continue;

            var fileInfo = new FileInfo(file);
            backups.Add(new BackupInfo
            {
                FilePath = file,
                StoreId = match.Groups[1].Value,
                Floor = match.Groups[2].Value,
                Timestamp = timestamp,
                FileSizeBytes = fileInfo.Length
            });
        }

        backups.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return Task.FromResult(backups);
    }

    public async Task<string> ReadBackupAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    public Task DeleteBackupAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted backup {FilePath}", filePath);
        }
        return Task.CompletedTask;
    }
}
