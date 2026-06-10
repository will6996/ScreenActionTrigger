using System.Drawing;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface IScreenCaptureService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<byte[]?> CaptureRegionAsync(MonitoredRegion region, CancellationToken ct = default);
    Task<byte[]?> CaptureAreaAsync(int x, int y, int width, int height, CancellationToken ct = default);
    Task<byte[]?> CaptureFullScreenAsync(CancellationToken ct = default);
    Task<byte[]?> CaptureInteractiveAreaAsync(CancellationToken ct = default);
    Rectangle GetPrimaryScreenBounds();
}
