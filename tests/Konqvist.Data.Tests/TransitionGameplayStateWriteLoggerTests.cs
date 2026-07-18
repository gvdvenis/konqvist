using Konqvist.Data.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Konqvist.Data.Tests;

/// <summary>
///   Captures log entries emitted via <see cref="ILogger"/> so tests can
///   assert on log levels, messages, and exceptions without pulling in a
///   testing-logger NuGet package. Not thread-safe beyond the logger contract;
///   the transition logger is itself guarded by a lock.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public sealed class Entry
    {
        public LogLevel Level { get; init; }
        public EventId EventId { get; init; }
        public object? State { get; init; }
        public Exception? Exception { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    private readonly List<Entry> _entries = new();
    private readonly object _gate = new();

    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (_gate) { return _entries.ToArray(); }
        }
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var entry = new Entry
        {
            Level = logLevel,
            EventId = eventId,
            State = state,
            Exception = exception,
            Message = formatter(state, exception)
        };
        lock (_gate) { _entries.Add(entry); }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

public class TransitionGameplayStateWriteLoggerTests
{
    private const string TestSlot = "test-slot";

    private const string ConnectionStringWithSecret =
        "Server=tcp:secret-server.database.windows.net,1433;" +
        "Initial Catalog=konqvist-prod;" +
        "User Id=sa;" +
        "Password=SuperSecretP@ssw0rd!123;" +
        "Encrypt=True;TrustServerCertificate=False;";

    private const string SafeConnectionString =
        "Server=tcp:safe-server.database.windows.net,1433;" +
        "Initial Catalog=konqvist-prod;" +
        "Encrypt=True;";

    private static TransitionGameplayStateWriteLogger CreateLogger(CapturingLogger<TransitionGameplayStateWriteLogger> capture, string? connectionString = SafeConnectionString)
        => new(connectionString, TestSlot, capture);

    [Fact]
    public void First_Failure_Should_Log_Error_With_Exception_Details()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture);
        var ex = new InvalidOperationException("boom");

        // Act
        logger.LogFailure(ex);

        // Assert
        Assert.Single(capture.Entries);
        var entry = capture.Entries[0];
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(ex, entry.Exception);
        Assert.Contains(TestSlot, entry.Message);
    }

    [Fact]
    public void First_Failure_Should_Include_Allowlisted_Connection_Fields()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture, SafeConnectionString);
        var ex = new InvalidOperationException("boom");

        // Act
        logger.LogFailure(ex);

        // Assert
        Assert.Single(capture.Entries);
        var message = capture.Entries[0].Message;
        Assert.Contains("safe-server.database.windows.net", message);
        Assert.Contains("konqvist-prod", message);
        Assert.Contains("Encrypt", message);
    }

    [Fact]
    public void Repeated_Failures_Within_Same_Outage_Should_Be_Suppressed()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture);

        // Act
        logger.LogFailure(new InvalidOperationException("first"));
        logger.LogFailure(new InvalidOperationException("second"));
        logger.LogFailure(new InvalidOperationException("third"));

        // Assert
        Assert.Single(capture.Entries);
        Assert.Equal("first", capture.Entries[0].Exception?.Message);
    }

    [Fact]
    public void Recovery_After_Outage_Should_Log_Warning_With_Slot_And_Duration()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture);
        logger.LogFailure(new InvalidOperationException("boom"));

        // Act
        logger.LogSuccess();

        // Assert: exactly two entries — the initial error and the recovery warning.
        Assert.Equal(2, capture.Entries.Count);
        var recovery = capture.Entries[1];
        Assert.Equal(LogLevel.Warning, recovery.Level);
        Assert.Contains(TestSlot, recovery.Message);
    }

    [Fact]
    public void No_Log_On_Healthy_Success_When_Not_In_Outage()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture);

        // Act
        logger.LogSuccess();
        logger.LogSuccess();

        // Assert
        Assert.Empty(capture.Entries);
    }

    [Fact]
    public void New_Outage_Cycle_After_Recovery_Should_Log_Failure_Again()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture);

        // First outage + recovery.
        logger.LogFailure(new InvalidOperationException("first-outage"));
        logger.LogSuccess();
        Assert.Equal(2, capture.Entries.Count);

        // Act: a second, independent outage.
        logger.LogFailure(new InvalidOperationException("second-outage"));

        // Assert: a new ERROR entry was emitted for the new cycle.
        Assert.Equal(3, capture.Entries.Count);
        var secondError = capture.Entries[2];
        Assert.Equal(LogLevel.Error, secondError.Level);
        Assert.Contains("second-outage", secondError.Exception?.Message);
    }

    [Fact]
    public void Secrets_In_Connection_String_Should_Never_Appear_In_Log_Messages()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture, ConnectionStringWithSecret);

        // Act: trigger a failure (emits ERROR) then a recovery (emits WARNING).
        logger.LogFailure(new InvalidOperationException("boom"));
        logger.LogSuccess();

        // Assert: the password must never appear in any emitted message.
        Assert.Equal(2, capture.Entries.Count);
        foreach (var entry in capture.Entries)
        {
            Assert.DoesNotContain("SuperSecretP@ssw0rd!123", entry.Message);
            Assert.DoesNotContain("User Id=sa", entry.Message);
        }
    }

    [Fact]
    public void Empty_Connection_String_Should_Log_Unknown_Server_And_Database()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = CreateLogger(capture, connectionString: null);

        // Act
        logger.LogFailure(new InvalidOperationException("boom"));

        // Assert
        Assert.Single(capture.Entries);
        var message = capture.Entries[0].Message;
        Assert.Contains("(unknown)", message);
    }

    [Fact]
    public void Null_Slot_Should_Default_To_Default()
    {
        // Arrange
        var capture = new CapturingLogger<TransitionGameplayStateWriteLogger>();
        var logger = new TransitionGameplayStateWriteLogger(SafeConnectionString, null!, capture);

        // Act
        logger.LogFailure(new InvalidOperationException("boom"));

        // Assert
        Assert.Single(capture.Entries);
        Assert.Contains("default", capture.Entries[0].Message);
    }
}
