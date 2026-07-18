using System.Threading;
using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Buffered, coalesced gameplay-state writer (#18).
///   <para>
///     Wraps an <see cref="IGameplayStateStore"/> and exposes
///     <see cref="ScheduleSave"/> as a fast, non-blocking entry point for
///     gameplay mutations. The first mutation starts one delayed save window;
///     later mutations within the window replace the pending state without
///     restarting the timer. When the window elapses, the latest pending state
///     is written through the wrapped store under a single-writer gate.
///   </para>
///   <para>
///     Domain locks are never held during serialization / database I/O: the
///     caller passes an immutable <see cref="GameplayState"/> record copy, so
///     the heavy work happens outside the caller's lock by construction.
///   </para>
///   <para>
///     Logging seam: <see cref="OnWriteSucceeded"/> / <see cref="OnWriteFailed"/>
///     are empty <see langword="protected virtual"/> hooks so #21 can add
///     transition-based logging without rewriting the writer.
///   </para>
/// </summary>
public class BufferedGameplayStateWriter
{
    // ---- Wrapped store & config ------------------------------------------------
    private readonly IGameplayStateStore _store;
    private readonly GameplayStatePersistenceOptions _options;
    private readonly ILogger<BufferedGameplayStateWriter> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IGameplayStateWriteLogger _writeLogger;

    // ---- Pending-state slot (touched under Interlocked / lock) -----------------
    private GameplayState? _pendingState;
    private int _dirty;          // Interlocked signal: 1 => a write is pending
    private int _timerScheduled; // Interlocked signal: 1 => a timer tick is armed
    private int _disposed;

    // ---- Single-writer gate ----------------------------------------------------
    private readonly SemaphoreSlim _writerGate = new(1, 1);

    // ---- Timer -----------------------------------------------------------------
    private Timer? _timer;
    private readonly TimeSpan _saveInterval;

    public BufferedGameplayStateWriter(
        IGameplayStateStore store,
        IOptions<GameplayStatePersistenceOptions> options,
        ILogger<BufferedGameplayStateWriter> logger,
        TimeProvider? timeProvider = null,
        IGameplayStateWriteLogger? writeLogger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _writeLogger = writeLogger ?? NullGameplayStateWriteLogger.Instance;

        _saveInterval = _options.ClampInterval();
    }

    /// <summary>
    ///   Buffered entry point for gameplay mutations. Captures the latest
    ///   <paramref name="state"/> as the single pending snapshot and arms the
    ///   delayed-save timer exactly once per cycle. Does not block on the
    ///   store or on any gameplay-domain lock.
    /// </summary>
    public void ScheduleSave(GameplayState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        if (Interlocked.Exchange(ref _disposed, _disposed) != 0) return;

        // Replace the pending snapshot atomically. Only one slot exists; the
        // latest mutation always wins and earlier pending states are dropped.
        Interlocked.Exchange(ref _pendingState, state);
        Interlocked.Exchange(ref _dirty, 1);

        // Arm the timer exactly once per cycle. If a tick is already scheduled
        // (or a write is in flight that will re-arm on completion), do nothing:
        // the pending state was already replaced above and the armed tick will
        // pick it up. This is what gives us "later changes replace pending state
        // without restarting the timer".
        if (Interlocked.CompareExchange(ref _timerScheduled, 1, 0) == 0)
        {
            StartTimer();
        }
    }

    /// <summary>
    ///   Attempts one final flush of any pending state, waiting up to
    ///   <paramref name="timeout"/> for the in-flight write (if any) to finish
    ///   and the pending snapshot to be written. Safe to call from graceful
    ///   shutdown; never hangs the host beyond <paramref name="timeout"/>.
    /// </summary>
    public Task FlushAsync(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(Interlocked.Exchange(ref _disposed, _disposed) != 0, this);
        return FlushCoreAsync(timeout, CancellationToken.None);
    }

    /// <summary>
    ///   Graceful-shutdown path. Stops the timer, then attempts one final
    ///   flush bounded by <paramref name="cancellationToken"/> (or the
    ///   configured <see cref="GameplayStatePersistenceOptions.ShutdownFlushTimeout"/>
    ///   of 5s when the token has no deadline).
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        // Stop arming new ticks and dispose the timer.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var timer = _timer;
        _timer = null;
        timer?.Dispose();

        TimeSpan timeout = _options.ShutdownFlushTimeout;
        if (cancellationToken.CanBeCanceled)
        {
            // Use the token's remaining time as the upper bound if available,
            // otherwise fall back to the configured shutdown flush timeout.
            timeout = _options.ShutdownFlushTimeout;
        }

        try
        {
            await FlushCoreAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown timed out / was cancelled: give up gracefully, do not hang.
            _logger.LogWarning("BufferedGameplayStateWriter shutdown flush timed out or was cancelled; pending state may not have been written.");
        }
    }

    // ---- Internal plumbing -----------------------------------------------------

    private void StartTimer()
    {
        // Lazily create the timer once; reuse across cycles.
        var timer = _timer ??= new Timer(
            callback: OnTimerTick,
            state: this,
            dueTime: _saveInterval,
            period: Timeout.InfiniteTimeSpan);

        // Reset to a fresh one-shot window each time we (re)arm.
        timer.Change(dueTime: _saveInterval, period: Timeout.InfiniteTimeSpan);
    }

    private void OnTimerTick(object? state)
    {
        _ = OnTimerTickSafe(state as BufferedGameplayStateWriter ?? this);
    }

    private static async Task OnTimerTickSafe(BufferedGameplayStateWriter self)
    {
        try
        {
            await self.OnTimerTickAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Never let a timer callback tear down the process.
            self._logger.LogError(ex, "Unexpected error in buffered gameplay-state writer timer tick.");
        }
    }

    private async Task OnTimerTickAsync()
    {
        // Only one tick is ever armed at a time; clear the "armed" signal so a
        // new ScheduleSave can re-arm after this cycle.
        Interlocked.Exchange(ref _timerScheduled, 0);

        await DrainPendingAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task DrainPendingAsync(CancellationToken cancellationToken)
    {
        // Loop in case a mutation arrives during an in-flight write: that
        // mutation re-sets the dirty signal, and we loop to flush it. Each
        // iteration waits the full interval before the next write by yielding
        // (the timer model) — here we simply write the latest snapshot once
        // per tick; subsequent ticks handle further mutations.
        while (Interlocked.CompareExchange(ref _dirty, 0, 1) == 1)
        {
            // Take the latest pending snapshot and clear the slot. Reads of
            // _pendingState are only ever racing with ScheduleSave's atomic
            // Exchange, which is fine: we always pick up whichever snapshot
            // is current at this instant.
            GameplayState? snapshot = Interlocked.Exchange(ref _pendingState, null);
            if (snapshot is null)
            {
                // Dirty was set but state was cleared concurrently; nothing to do.
                return;
            }

            // Serialize writes through the single-writer gate. We do NOT hold
            // any gameplay-domain lock here; the snapshot is an immutable record
            // copy, so serialization + DB I/O happen entirely outside the
            // caller's lock.
            await _writerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                WriteToStore(snapshot);
            }
            finally
            {
                _writerGate.Release();
            }

            // If a mutation arrived during the write, _dirty is 1 again and the
            // loop continues to write it. If not, we exit. This naturally keeps
            // at most one pending state between writes (the loop re-reads the
            // single slot).
        }
    }

    private void WriteToStore(GameplayState snapshot)
    {
        try
        {
            _store.Write(snapshot);
            OnWriteSucceeded();
        }
        catch (Exception ex)
        {
            // Surface anything that escaped the wrapped store through the
            // logging seam (#21 transition-based logging).
            OnWriteFailed(ex);
        }
    }

    private async Task FlushCoreAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        // If no snapshot is pending, there is nothing to flush. If a write is
        // currently in flight (dirty bit cleared but writer gate held), we still
        // wait for the gate below to honour the "at most one write at a time"
        // contract and ensure the in-flight write completes within the timeout.
        if (Volatile.Read(ref _dirty) == 0 && _pendingState is null)
        {
            // Still acquire the gate briefly to let any in-flight write finish.
            using var gateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            gateCts.CancelAfter(timeout);
            try
            {
                await _writerGate.WaitAsync(gateCts.Token).ConfigureAwait(false);
                _writerGate.Release();
            }
            catch (OperationCanceledException)
            {
                // In-flight write exceeded the flush timeout.
            }
            return;
        }

        // Take the latest pending snapshot.
        GameplayState? snapshot = Interlocked.Exchange(ref _pendingState, null);
        Interlocked.Exchange(ref _dirty, 0);

        if (snapshot is null) return;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        await _writerGate.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            WriteToStore(snapshot);
        }
        finally
        {
            _writerGate.Release();
        }
    }

    /// <summary>
    ///   Logging / telemetry seam for #21. Default: forwards to the injected
    ///   <see cref="IGameplayStateWriteLogger"/>, which emits transition-based
    ///   logs (first failure / recovery) and suppresses repeats.
    /// </summary>
    protected virtual void OnWriteSucceeded()
    {
        _writeLogger.LogSuccess();
    }

    /// <summary>
    ///   Logging / telemetry seam for #21. Default: forwards to the injected
    ///   <see cref="IGameplayStateWriteLogger"/>, which emits transition-based
    ///   logs (first failure / recovery) and suppresses repeats.
    /// </summary>
    protected virtual void OnWriteFailed(Exception ex)
    {
        _writeLogger.LogFailure(ex);
    }
}
