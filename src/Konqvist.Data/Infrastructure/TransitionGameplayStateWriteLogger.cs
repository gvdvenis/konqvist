using Microsoft.Extensions.Logging;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Transition-based gameplay-state write logger (#21). Emits log entries
///   only on outage state transitions:
///   <list type="bullet">
///     <item>First failure of an outage: ERROR with exception details and allowlisted connection fields.</item>
///     <item>First successful recovery after an outage: WARNING with slot and outage duration.</item>
///   </list>
///   Repeated failures within the same outage and routine successes while
///   healthy are suppressed. A later outage (after recovery) starts a new
///   failure/recovery cycle. Only server (DataSource), database (InitialCatalog),
///   and the Encrypt setting are ever logged; credentials and the full connection
///   string are never logged.
///   <para>
///   Thread-safe: outage state is guarded by a lock. The
///   <see cref="BufferedGameplayStateWriter"/> calls these methods under its
///   single-writer gate, but the lock keeps the logger safe if reused.
///   </para>
/// </summary>
internal sealed class TransitionGameplayStateWriteLogger : IGameplayStateWriteLogger
{
    private readonly ILogger<TransitionGameplayStateWriteLogger> _logger;
    private readonly string _slot;
    private readonly GameplayStateConnectionInfo _connectionInfo;

    private readonly object _gate = new();
    private bool _inOutage;
    private DateTime _outageStartUtc;

    public TransitionGameplayStateWriteLogger(
        string? connectionString,
        string slot,
        ILogger<TransitionGameplayStateWriteLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _slot = string.IsNullOrWhiteSpace(slot) ? "default" : slot;
        _connectionInfo = GameplayStateConnectionInfo.FromConnectionString(connectionString);
    }

    public void LogFailure(Exception ex)
    {
        bool firstFailure;
        lock (_gate)
        {
            if (_inOutage)
            {
                // Suppress repeated failures within the same outage.
                return;
            }

            _inOutage = true;
            _outageStartUtc = DateTime.UtcNow;
            firstFailure = true;
        }

        if (firstFailure)
        {
            _logger.LogError(
                ex,
                "Gameplay-state persistence outage started for slot '{Slot}' on server '{Server}', database '{Database}' (Encrypt={Encrypt}).",
                _slot,
                _connectionInfo.Server ?? "(unknown)",
                _connectionInfo.Database ?? "(unknown)",
                _connectionInfo.Encrypt ?? "(unknown)");
        }
    }

    public void LogSuccess()
    {
        DateTime outageStart;
        lock (_gate)
        {
            if (!_inOutage)
            {
                // Healthy write: no transition, nothing to log.
                return;
            }

            outageStart = _outageStartUtc;
            _inOutage = false;
        }

        var duration = DateTime.UtcNow - outageStart;
        _logger.LogWarning(
            "Gameplay-state persistence recovered for slot '{Slot}' after outage of {Duration}. Writes resumed on server '{Server}', database '{Database}'.",
            _slot,
            duration,
            _connectionInfo.Server ?? "(unknown)",
            _connectionInfo.Database ?? "(unknown)");
    }
}
