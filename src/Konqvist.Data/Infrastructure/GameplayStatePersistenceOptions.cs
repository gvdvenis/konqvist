namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Persistence options for the gameplay-state store, bound from the
///   "GameplayStatePersistence" configuration section. The save interval
///   defaults to 1 second and must stay within [1s, 60s].
///   NOTE: This is the minimal version created by #20 so startup config
///   compiles. #18 owns the full implementation and may extend this class
///   (additional bounds, data annotations, buffered-write settings, etc.).
/// </summary>
public sealed class GameplayStatePersistenceOptions
{
    /// <summary>Minimum allowed save interval.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    /// <summary>Maximum allowed save interval.</summary>
    public static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(60);

    private TimeSpan _saveInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    ///   Interval between persistence writes. Default 1s; bounds [1s, 60s].
    /// </summary>
    public TimeSpan SaveInterval
    {
        get => _saveInterval;
        set => _saveInterval = value;
    }

    /// <summary>
    ///   Logical persistence slot key. Defaults to "default". #19 owns
    ///   slot/game-definition isolation semantics and will refine this.
    /// </summary>
    public string Slot { get; set; } = "default";

    /// <summary>
    ///   Hard upper bound used by <see cref="BufferedGameplayStateWriter.FlushAsync"/>
    ///   and <see cref="BufferedGameplayStateWriter.ShutdownAsync"/> when no
    ///   explicit timeout is supplied. Default 5 seconds per the #18 spec.
    /// </summary>
    public TimeSpan ShutdownFlushTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///   Returns a validated, clamped copy of the interval so the writer never
    ///   sees a value outside [1s, 60s] even if a caller misconfigures it.
    /// </summary>
    public TimeSpan ClampInterval()
    {
        if (_saveInterval < MinInterval) return MinInterval;
        if (_saveInterval > MaxInterval) return MaxInterval;
        return _saveInterval;
    }

    /// <summary>
    ///   Validates the configured bounds. Returns <c>false</c> and sets
    ///   <paramref name="error" /> when invalid.
    /// </summary>
    public bool IsValid(out string error)
    {
        if (_saveInterval < MinInterval || _saveInterval > MaxInterval)
        {
            error = $"GameplayStatePersistence:SaveInterval must be between 1s and 60s (inclusive), but was '{_saveInterval}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
