using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Engines;

public sealed class ActionQueue : IDisposable
{
    private readonly PriorityQueue<ActionQueueItem, int> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<ActionQueue> _logger;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public int Count { get; private set; }

    public event EventHandler<ActionQueueItem>? ItemDequeued;

    public ActionQueue(ILogger<ActionQueue> logger) => _logger = logger;

    public async Task EnqueueAsync(ActionQueueItem item, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _queue.Enqueue(item, -item.Priority); // negative for max-priority
            Count++;
            _signal.Release();
        }
        finally { _lock.Release(); }
    }

    public async Task<ActionQueueItem?> DequeueAsync(CancellationToken ct = default)
    {
        while (true)
        {
            await _signal.WaitAsync(ct);
            await _lock.WaitAsync(ct);
            try
            {
                if (_queue.TryDequeue(out var item, out _))
                {
                    Count--;
                    if (!item.IsCancelled)
                    {
                        ItemDequeued?.Invoke(this, item);
                        return item;
                    }
                }
            }
            finally { _lock.Release(); }
        }
    }

    public async Task CancelAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            while (_queue.Count > 0)
                _queue.Dequeue().IsCancelled = true;
            Count = 0;
        }
        finally { _lock.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _signal.Dispose();
        _lock.Dispose();
    }
}
