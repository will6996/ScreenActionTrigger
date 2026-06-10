using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ScreenActionTrigger.UI.Services;
using ScreenActionTrigger.UI.ViewModels;

namespace ScreenActionTrigger.UI;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private readonly GlobalHotkeyService _hotkeys;
    private bool _allowClose;
    private bool _isShuttingDown;

    public MainWindow(MainViewModel viewModel, GlobalHotkeyService hotkeys)
    {
        ViewModel   = viewModel;
        _hotkeys    = hotkeys;
        DataContext = viewModel;
        InitializeComponent();

        _hotkeys.StartStopRequested += (_, _) => DispatchHotkey(OnStartStopHotkey);
        _hotkeys.PauseRequested     += (_, _) => DispatchHotkey(OnPauseHotkey);
        _hotkeys.RegistrationChanged += OnHotkeyRegistrationChanged;

        ViewModel.SettingsVM.PropertyChanged += OnSettingsChanged;
        Loaded  += OnWindowLoaded;
        Closing += OnClosing;

        _hotkeys.AttachToWindow(this);
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        RefreshHotkeys();
        await ViewModel.LoadAutoSaveAsync();
        RefreshHotkeys();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        try
        {
            await ViewModel.ShutdownAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Erro ao fechar: {ex.Message}";
        }
        finally
        {
            _allowClose = true;
            Close();
        }
    }

    private void RefreshHotkeys()
    {
        _hotkeys.SetHotkeys(
            ViewModel.SettingsVM.HotkeyStartStop,
            ViewModel.SettingsVM.HotkeyPause);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.HotkeyStartStop)
                         or nameof(SettingsViewModel.HotkeyPause))
        {
            _hotkeys.SetHotkeys(
                ViewModel.SettingsVM.HotkeyStartStop,
                ViewModel.SettingsVM.HotkeyPause);
        }
    }

    private void OnHotkeyRegistrationChanged(object? sender, HotkeyRegistrationEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (e.StartStopOk && e.PauseOk)
                return;

            var problems = new List<string>();
            if (!e.StartStopOk) problems.Add(e.StartStop);
            if (!e.PauseOk)     problems.Add(e.Pause);

            ViewModel.StatusText =
                $"Atalho(s) não registrado(s): {string.Join(", ", problems)} " +
                "(pode estar em uso por outro app)";
        });
    }

    private void DispatchHotkey(Func<Task> action)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try { await action(); }
            catch (Exception ex) { ViewModel.StatusText = $"Erro no atalho: {ex.Message}"; }
        });
    }

    private Task OnStartStopHotkey() => ViewModel.HotkeyToggleMonitoringCommand.ExecuteAsync(null);

    private Task OnPauseHotkey()
    {
        if (!ViewModel.IsMonitoring)
        {
            ViewModel.StatusText = "Inicie o monitoramento antes de pausar (F9)";
            return Task.CompletedTask;
        }

        return ViewModel.TogglePauseCommand.ExecuteAsync(null);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void TitleBar_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Evita menu do sistema do Windows (Mover/Fechar) na barra de título customizada
        e.Handled = true;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.F9)
        {
            e.Handled = true;
            _ = ViewModel.HotkeyToggleMonitoringCommand.ExecuteAsync(null);
        }
        else if (e.Key == Key.F10)
        {
            e.Handled = true;
            _ = OnPauseHotkey();
        }
    }
}
