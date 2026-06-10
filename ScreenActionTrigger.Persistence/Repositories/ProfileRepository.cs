using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Persistence.Serializers;

namespace ScreenActionTrigger.Persistence.Repositories;

public sealed class ProfileRepository : IProfileManager
{
    private readonly ILogger<ProfileRepository> _logger;
    private readonly List<string> _recentPaths = new();
    private const int MaxRecent = 10;

    public ExecutionProfile? CurrentProfile { get; private set; }

    public ProfileRepository(ILogger<ProfileRepository> logger) => _logger = logger;

    public async Task<ExecutionProfile?> LoadAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("Profile file not found: {Path}", path);
                return null;
            }

            await using var stream = File.OpenRead(path);
            var profile = await JsonProfileSerializer.DeserializeAsync(stream);
            if (profile is not null)
            {
                CurrentProfile = profile;
                AddRecentPath(path);
                _logger.LogInformation("Profile '{Name}' loaded from {Path}", profile.Name, path);
            }
            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile from {Path}", path);
            return null;
        }
    }

    public async Task SaveAsync(ExecutionProfile profile, string path)
    {
        try
        {
            profile.UpdatedAt = DateTime.UtcNow;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await using var stream = File.Create(path);
            await JsonProfileSerializer.SerializeAsync(profile, stream);

            CurrentProfile = profile;
            AddRecentPath(path);
            _logger.LogInformation("Profile '{Name}' saved to {Path}", profile.Name, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profile to {Path}", path);
            throw;
        }
    }

    public async Task<string> ExportAsync(ExecutionProfile profile, string destinationPath)
    {
        // Export includes templates as base64 embedded data
        await SaveAsync(profile, destinationPath);
        return destinationPath;
    }

    public async Task<ExecutionProfile?> ImportAsync(string sourcePath)
    {
        return await LoadAsync(sourcePath);
    }

    public ExecutionProfile CreateNew(string name = "Novo Perfil") => new()
    {
        Name = name,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public IEnumerable<string> GetRecentPaths() => _recentPaths.AsReadOnly();

    public void AddRecentPath(string path)
    {
        _recentPaths.Remove(path);
        _recentPaths.Insert(0, path);
        while (_recentPaths.Count > MaxRecent)
            _recentPaths.RemoveAt(_recentPaths.Count - 1);
    }
}
