using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Engines;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Input.Controllers;
using ScreenActionTrigger.Overlay.Services;
using ScreenActionTrigger.Persistence.Repositories;
using ScreenActionTrigger.UI.Services;
using ScreenActionTrigger.UI.ViewModels;
using ScreenActionTrigger.Vision.Detectors;
using ScreenActionTrigger.Vision.Services;

namespace ScreenActionTrigger.UI;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"UI exception: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                System.Diagnostics.Debug.WriteLine($"Domain exception: {ex}");
        };

        _services = ConfigureServices();
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<GlobalHotkeyService>()?.Dispose();
        if (_services is IDisposable d) d.Dispose();
        base.OnExit(e);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Debug);
        });

        // Vision layer
        services.AddSingleton<ColorDetector>();
        services.AddSingleton<ChangeDetector>();
        services.AddSingleton<TemplateMatcher>();
        services.AddSingleton<IVisionEngine, VisionEngine>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();

        // Input layer
        services.AddSingleton<MouseController>();
        services.AddSingleton<KeyboardController>();
        services.AddSingleton<IActionDispatcher, ActionDispatcher>();

        // Core engines
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<ISequenceEngine, SequenceEngine>();

        // Persistence
        services.AddSingleton<IProfileManager, ProfileRepository>();
        services.AddSingleton<ITemplateLibrary, TemplateRepository>();

        // Overlay
        services.AddSingleton<IOverlayService, OverlayService>();

        // Monitoring service (orchestrator)
        services.AddSingleton<IMonitoringService, MonitoringService>();

        // Update service
        services.AddSingleton<IUpdateService, UpdateService>();

        // Global hotkeys (F9/F10 etc.)
        services.AddSingleton<GlobalHotkeyService>();

        // ViewModels
        services.AddSingleton<UpdateViewModel>(sp => new UpdateViewModel(
            sp.GetRequiredService<IUpdateService>(),
            sp.GetRequiredService<ILogger<UpdateViewModel>>(),
            () => sp.GetRequiredService<MainViewModel>().SaveAutoSaveAsync()));
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<RegionsViewModel>();
        services.AddSingleton<RulesViewModel>();
        services.AddSingleton<SequencesViewModel>();
        services.AddSingleton<TemplatesViewModel>();
        services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<MainViewModel>();

        // Main window
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
