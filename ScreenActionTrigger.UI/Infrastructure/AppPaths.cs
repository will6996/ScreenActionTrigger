using System.Reflection;

namespace ScreenActionTrigger.UI.Infrastructure;

/// <summary>
/// Caminhos de dados da aplicação.
/// Em modo single-file o executável é extraído para %TEMP%, então usamos
/// o diretório do processo original (não o diretório de extração temporário).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Pasta raiz onde perfis e templates são salvos.
    /// → %APPDATA%\ScreenActionTrigger\
    /// </summary>
    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenActionTrigger");

    /// <summary>Pasta de perfis salvo (.satprofile)</summary>
    public static string ProfilesDir { get; } = Path.Combine(DataRoot, "Profiles");

    /// <summary>Pasta de imagens de templates</summary>
    public static string TemplatesDir { get; } = Path.Combine(DataRoot, "Templates");

    /// <summary>Pasta de logs</summary>
    public static string LogsDir { get; } = Path.Combine(DataRoot, "Logs");

    /// <summary>
    /// Diretório onde o .exe realmente está (caminho original do processo).
    /// Em modo single-file, Process.MainModule.FileName aponta ao .exe real,
    /// não ao diretório de extração temporário.
    /// </summary>
    public static string ExecutableDir { get; } =
        Path.GetDirectoryName(Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? AppContext.BaseDirectory)
        ?? AppContext.BaseDirectory;

    static AppPaths()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(TemplatesDir);
        Directory.CreateDirectory(LogsDir);
    }
}
