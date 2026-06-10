using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Vision.Services;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<ScreenCaptureService> _logger;
    private bool _initialized;

    // GDI32 / User32 imports
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern int  GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private const uint SRCCOPY = 0x00CC0020;

    public ScreenCaptureService(ILogger<ScreenCaptureService> logger) => _logger = logger;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    public Rectangle GetPrimaryScreenBounds()
        => new(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

    public Task<byte[]?> CaptureRegionAsync(MonitoredRegion region, CancellationToken ct = default)
        => CaptureAreaAsync(region.X, region.Y, region.Width, region.Height, ct);

    public Task<byte[]?> CaptureFullScreenAsync(CancellationToken ct = default)
    {
        var b = GetPrimaryScreenBounds();
        return CaptureAreaAsync(b.X, b.Y, b.Width, b.Height, ct);
    }

    public async Task<byte[]?> CaptureAreaAsync(int x, int y, int width, int height, CancellationToken ct = default)
    {
        if (width <= 0 || height <= 0) return null;
        ct.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            IntPtr desktopDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            IntPtr desktop = GetDesktopWindow();

            try
            {
                desktopDc = GetDC(desktop);
                memDc = CreateCompatibleDC(desktopDc);
                bitmap = CreateCompatibleBitmap(desktopDc, width, height);
                oldBitmap = SelectObject(memDc, bitmap);

                BitBlt(memDc, 0, 0, width, height, desktopDc, x, y, SRCCOPY);

                using var bmp = Image.FromHbitmap(bitmap);
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Capture failed at [{X},{Y} {W}x{H}]", x, y, width, height);
                return null;
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (desktopDc != IntPtr.Zero) ReleaseDC(desktop, desktopDc);
            }
        }, ct);
    }

    public async Task<byte[]?> CaptureInteractiveAreaAsync(CancellationToken ct = default)
    {
        // Interactive capture: overlay a semi-transparent window and let user drag
        // Simplified: capture the full screen; UI layer handles region selection
        return await CaptureFullScreenAsync(ct);
    }

    public ValueTask DisposeAsync()
    {
        _initialized = false;
        return ValueTask.CompletedTask;
    }
}
