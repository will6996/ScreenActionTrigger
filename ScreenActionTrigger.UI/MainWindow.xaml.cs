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
        _hotkeys.Initialize();
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

    private Task OnStartStopHotkey() => ViewModel.ToggleMonitoringCommand.ExecuteAsync(null);

    private Task OnPauseHotkey()
    {
        if (!ViewModel.IsMonitoring)
            return Task.CompletedTask;

        return ViewModel.TogglePauseCommand.ExecuteAsync(null);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
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
        // F9/F10 global já tratados pelo RegisterHotKey — aqui só repassa quando foco está em TextBox
        if (e.Handled || e.OriginalSource is not System.Windows.Controls.TextBox)
            return;

        if (e.Key == Key.F9)
        {
            e.Handled = true;
            _ = ViewModel.ToggleMonitoringCommand.ExecuteAsync(null);
        }
        else if (e.Key == Key.F10 && ViewModel.IsMonitoring)
        {
            e.Handled = true;
            _ = ViewModel.TogglePauseCommand.ExecuteAsync(null);
        }
    }
}
