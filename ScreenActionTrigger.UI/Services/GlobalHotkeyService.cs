using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.UI.Infrastructure;

namespace ScreenActionTrigger.UI.Services;

/// <summary>
/// Registra atalhos globais via RegisterHotKey na janela principal do app.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey    = 0x0312;
    private const int IdStartStop = 1;
    private const int IdPause     = 2;
    private const uint ModNoRepeat = 0x4000;

    private readonly ILogger<GlobalHotkeyService> _logger;

    private HwndSource? _hwndSource;
    private IntPtr      _hwnd;
    private string      _startStop = "F9";
    private string      _pause     = "F10";
    private bool        _startStopRegistered;
    private bool        _pauseRegistered;
    private bool        _initialized;

    public event EventHandler? StartStopRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler<HotkeyRegistrationEventArgs>? RegistrationChanged;

    public bool IsStartStopRegistered => _startStopRegistered;
    public bool IsPauseRegistered     => _pauseRegistered;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger) => _logger = logger;

    public void AttachToWindow(Window window)
    {
        if (window.IsLoaded)
            AttachToHwnd(new WindowInteropHelper(window).Handle);
        else
            window.SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.SourceInitialized -= OnSourceInitialized;
        AttachToHwnd(new WindowInteropHelper(window).Handle);
    }

    private void AttachToHwnd(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        if (_hwndSource is not null && _hwnd != hwnd)
        {
            _hwndSource.RemoveHook(WndProc);
            UnregisterAll();
            _hwndSource = null;
        }

        if (_hwndSource is null)
        {
            _hwndSource = HwndSource.FromHwnd(hwnd);
            if (_hwndSource is null)
            {
                _logger.LogWarning("HwndSource indisponível para HWND={Hwnd}", hwnd);
                return;
            }
            _hwndSource.AddHook(WndProc);
        }

        _hwnd        = hwnd;
        _initialized = true;
        _logger.LogDebug("Atalhos ligados à janela principal (HWND={Hwnd})", _hwnd);
        RegisterAll();
    }

    public void SetHotkeys(string startStop, string pause)
    {
        _startStop = startStop?.Trim() ?? string.Empty;
        _pause     = pause?.Trim()     ?? string.Empty;

        if (_initialized)
        {
            UnregisterAll();
            RegisterAll();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
            return IntPtr.Zero;

        switch (wParam.ToInt32())
        {
            case IdStartStop:
                _logger.LogDebug("Atalho Iniciar/Parar pressionado");
                StartStopRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
            case IdPause:
                _logger.LogDebug("Atalho Pausar/Continuar pressionado");
                PauseRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void RegisterAll()
    {
        _startStopRegistered = TryRegister(IdStartStop, _startStop, "Iniciar/Parar");
        _pauseRegistered     = TryRegister(IdPause, _pause, "Pausar/Continuar");

        RegistrationChanged?.Invoke(this, new HotkeyRegistrationEventArgs(
            _startStopRegistered, _pauseRegistered, _startStop, _pause));
    }

    private bool TryRegister(int id, string hotkey, string label)
    {
        if (!_initialized || _hwnd == IntPtr.Zero)
            return false;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            _logger.LogWarning("Atalho vazio ({Label})", label);
            return false;
        }

        if (!HotkeyParser.TryParse(hotkey, out var mods, out var vk))
        {
            _logger.LogWarning("Atalho inválido ({Label}): {Hotkey}", label, hotkey);
            return false;
        }

        if (!RegisterHotKey(_hwnd, id, mods | ModNoRepeat, vk))
        {
            var err = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "RegisterHotKey falhou para {Label} ({Hotkey}). Win32={Error}",
                label, hotkey, err);
            return false;
        }

        _logger.LogInformation("Atalho registrado: {Label} = {Hotkey}", label, hotkey);
        return true;
    }

    private void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        if (_startStopRegistered)
            UnregisterHotKey(_hwnd, IdStartStop);
        if (_pauseRegistered)
            UnregisterHotKey(_hwnd, IdPause);

        _startStopRegistered = false;
        _pauseRegistered     = false;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
        _hwnd        = IntPtr.Zero;
        _initialized = false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public sealed class HotkeyRegistrationEventArgs : EventArgs
{
    public bool   StartStopOk { get; }
    public bool   PauseOk     { get; }
    public string StartStop   { get; }
    public string Pause       { get; }

    public HotkeyRegistrationEventArgs(bool startStopOk, bool pauseOk, string startStop, string pause)
    {
        StartStopOk = startStopOk;
        PauseOk     = pauseOk;
        StartStop   = startStop;
        Pause       = pause;
    }
}
