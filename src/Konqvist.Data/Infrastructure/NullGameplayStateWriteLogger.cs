namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Default no-op <see cref="IGameplayStateWriteLogger"/> used when no
///   transition-based logging is wired (e.g. in tests or non-web hosts).
///   Thread-safe singleton via <see cref="Instance"/>.
/// </summary>
internal sealed class NullGameplayStateWriteLogger : IGameplayStateWriteLogger
{
    public static NullGameplayStateWriteLogger Instance { get; } = new();

    private NullGameplayStateWriteLogger() { }

    public void LogSuccess() { }

    public void LogFailure(Exception ex) { }
}
