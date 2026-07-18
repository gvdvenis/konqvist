namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Transition-based logging seam for the gameplay-state persistence writer
///   (#21). Implementations track outage state and emit log entries only on
///   state transitions: the first failure of an outage, and the first
///   successful recovery after an outage. Repeated failures within the same
///   outage and routine successes while healthy are suppressed.
/// </summary>
public interface IGameplayStateWriteLogger
{
    /// <summary>
    ///   Called by the writer after a successful store write. Implementations
    ///   log a recovery entry only when a prior outage was active.
    /// </summary>
    void LogSuccess();

    /// <summary>
    ///   Called by the writer when a store write throws. Implementations log
    ///   the first failure of an outage with exception details and allowlisted
    ///   connection-target fields, and suppress repeated failures within the
    ///   same outage.
    /// </summary>
    void LogFailure(Exception ex);
}
