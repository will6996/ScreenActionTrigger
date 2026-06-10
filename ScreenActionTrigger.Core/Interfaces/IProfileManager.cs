using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IProfileManager
{
    ExecutionProfile? CurrentProfile { get; }

    Task<ExecutionProfile?> LoadAsync(string path);
    Task SaveAsync(ExecutionProfile profile, string path);
    Task<string> ExportAsync(ExecutionProfile profile, string destinationPath);
    Task<ExecutionProfile?> ImportAsync(string sourcePath);
    ExecutionProfile CreateNew(string name = "Novo Perfil");
    IEnumerable<string> GetRecentPaths();
    void AddRecentPath(string path);
}
