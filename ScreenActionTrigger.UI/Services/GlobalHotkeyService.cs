using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.UI.Infrastructure;

namespace ScreenActionTrigger.UI.Services;

/// <summary>
/// Registra atalhos globais via RegisterHotKey numa janela oculta (HWND_MESSAGE),
/// independente da janela principal do app.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);

    private const int WmHotkey    = 0x0312;
    private const int IdStartStop = 1;
    private const int IdPause     = 2;
    private const uint ModNoRepeat = 0x4000;

    private readonly ILogger<GlobalHotkeyService> _logger;

    private HwndSource? _messageSource;
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

    public void Initialize()
    {
        if (_initialized)
            return;

        var parameters = new HwndSourceParameters("ScreenActionTrigger.Hotkeys")
        {
            Width               = 0,
            Height              = 0,
            PositionX           = 0,
            PositionY           = 0,
            WindowStyle         = 0,
            ExtendedWindowStyle = 0,
            ParentWindow        = HwndMessage,
            UsesPerPixelOpacity = false
        };

        _messageSource = new HwndSource(parameters);
        _messageSource.AddHook(WndProc);
        _hwnd = _messageSource.Handle;
        _initialized = true;

        _logger.LogDebug("Janela de atalhos criada (HWND={Hwnd})", _hwnd);
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
        _messageSource?.RemoveHook(WndProc);
        _messageSource?.Dispose();
        _messageSource = null;
        _hwnd          = IntPtr.Zero;
        _initialized   = false;
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
