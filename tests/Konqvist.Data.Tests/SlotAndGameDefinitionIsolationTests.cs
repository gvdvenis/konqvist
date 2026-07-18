using Konqvist.Data.Infrastructure;
using Konqvist.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Konqvist.Data.Tests;

/// <summary>
///   Verifies <see cref="SqlGameplayStateStore"/> isolates persisted state by the
///   composite (Slot, GameDefinitionId) key, and that resume semantics work when
///   switching between game definitions within the same slot. Uses the EF Core
///   in-memory provider so no live SQL Server is required. The in-memory provider
///   does not enforce CHECK constraints (ISJSON) or unique indexes the way SQL
///   Server does, but it does support queries and upserts, which is sufficient
///   to verify the store's keying logic at the unit level.
/// </summary>
public class SlotAndGameDefinitionIsolationTests : IAsyncLifetime
{
    private GameplayStateDbContext _db = null!;

    private static GameplayState MakeState(string marker, int round = 1)
        => new(
            GameDefinitionHash: marker,
            CurrentRoundNumber: round,
            Districts: [new DistrictGameplayState(marker, "Bravo", false)],
            Teams: [new TeamGameplayState(marker, new OpenLayers.Blazor.Coordinate(0, 0), true, new ResourcesData(), [], [], [])]);

    private GameplayStateDbContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<GameplayStateDbContext>(opts =>
            opts.UseInMemoryDatabase($"isolation-test-{Guid.NewGuid()}"));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<GameplayStateDbContext>();
    }

    public Task InitializeAsync()
    {
        _db = CreateContext();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public void Read_Should_Return_Null_For_Missing_Key()
    {
        // Arrange
        var store = new SqlGameplayStateStore(_db, "slot-a", "def-1");

        // Act
        var read = store.Read();

        // Assert
        Assert.Null(read);
    }

    [Fact]
    public void Different_Slots_Should_Not_Overwrite_Each_Other()
    {
        // Arrange
        var stateA = MakeState("marker-A");
        var stateB = MakeState("marker-B");
        var storeSlot1 = new SqlGameplayStateStore(_db, "slot-1", "def-1");
        var storeSlot2 = new SqlGameplayStateStore(_db, "slot-2", "def-1");

        // Act
        storeSlot1.Write(stateA);
        storeSlot2.Write(stateB);

        // Assert: reading (slot-1, def-1) returns state A, not B.
        var readSlot1 = storeSlot1.Read();
        Assert.NotNull(readSlot1);
        Assert.Equal("marker-A", readSlot1!.GameDefinitionHash);
    }

    [Fact]
    public void Different_Definitions_In_Same_Slot_Should_Not_Overwrite_Each_Other()
    {
        // Arrange
        var state1 = MakeState("marker-1");
        var state2 = MakeState("marker-2");
        var storeDef1 = new SqlGameplayStateStore(_db, "slot-1", "def-1");
        var storeDef2 = new SqlGameplayStateStore(_db, "slot-1", "def-2");

        // Act
        storeDef1.Write(state1);
        storeDef2.Write(state2);

        // Assert: each key returns its own state.
        var readDef1 = storeDef1.Read();
        var readDef2 = storeDef2.Read();
        Assert.NotNull(readDef1);
        Assert.NotNull(readDef2);
        Assert.Equal("marker-1", readDef1!.GameDefinitionHash);
        Assert.Equal("marker-2", readDef2!.GameDefinitionHash);
    }

    [Fact]
    public void Re_Activating_Old_Definition_Should_Resume_Its_Retained_State()
    {
        // Arrange
        var state1 = MakeState("marker-1", round: 5);
        var state2 = MakeState("marker-2", round: 9);
        var storeDef1 = new SqlGameplayStateStore(_db, "slot-1", "def-1");
        var storeDef2 = new SqlGameplayStateStore(_db, "slot-1", "def-2");

        // Act: write def-1, then switch to def-2, then come back to def-1.
        storeDef1.Write(state1);
        storeDef2.Write(state2);

        // Assert: def-1's original state is still retained (resume semantics).
        var resumedDef1 = storeDef1.Read();
        Assert.NotNull(resumedDef1);
        Assert.Equal("marker-1", resumedDef1!.GameDefinitionHash);
        Assert.Equal(5, resumedDef1.CurrentRoundNumber);
    }

    [Fact]
    public void Clear_Should_Only_Clear_The_Scoped_Key()
    {
        // Arrange
        var state1 = MakeState("marker-1");
        var state2 = MakeState("marker-2");
        var storeDef1 = new SqlGameplayStateStore(_db, "slot-1", "def-1");
        var storeDef2 = new SqlGameplayStateStore(_db, "slot-1", "def-2");
        storeDef1.Write(state1);
        storeDef2.Write(state2);

        // Act: clear only def-1.
        storeDef1.Clear();

        // Assert: def-1 is gone, def-2 is untouched.
        Assert.Null(storeDef1.Read());
        var remainingDef2 = storeDef2.Read();
        Assert.NotNull(remainingDef2);
        Assert.Equal("marker-2", remainingDef2!.GameDefinitionHash);
    }

    [Fact]
    public void Write_Should_Upsert_Existing_Key_In_Place()
    {
        // Arrange
        var store = new SqlGameplayStateStore(_db, "slot-1", "def-1");
        var first = MakeState("first", round: 1);
        var second = MakeState("second", round: 2);

        // Act
        store.Write(first);
        store.Write(second);

        // Assert: one row, latest value.
        var read = store.Read();
        Assert.NotNull(read);
        Assert.Equal("second", read!.GameDefinitionHash);
        Assert.Equal(2, read.CurrentRoundNumber);
        int rowCount = _db.GameplayStates.Count(e => e.Slot == "slot-1" && e.GameDefinitionId == "def-1");
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task Read_Should_Swallow_Exceptions_And_Return_Null()
    {
        // Arrange: use a disposed context so querying throws.
        var disposedDb = CreateContext();
        await disposedDb.DisposeAsync();
        var store = new SqlGameplayStateStore(disposedDb, "slot-1", "def-1");

        // Act
        var read = store.Read();

        // Assert: exception swallowed, null returned (restart/restore fallback).
        Assert.Null(read);
    }

    [Fact]
    public async Task Write_Should_Propagate_Exceptions()
    {
        // Arrange: use a disposed context so SaveChanges throws.
        var disposedDb = CreateContext();
        await disposedDb.DisposeAsync();
        var store = new SqlGameplayStateStore(disposedDb, "slot-1", "def-1");

        // Act & Assert: write must NOT swallow exceptions.
        Assert.ThrowsAny<Exception>(() => store.Write(MakeState("marker")));
    }

    [Fact]
    public async Task Clear_Should_Propagate_Exceptions()
    {
        // Arrange: pre-seed a row so the query finds something, then dispose
        // the context so Remove+SaveChanges throws.
        var seedDb = CreateContext();
        var seedStore = new SqlGameplayStateStore(seedDb, "slot-1", "def-1");
        seedStore.Write(MakeState("marker"));
        await seedDb.DisposeAsync();

        var store = new SqlGameplayStateStore(seedDb, "slot-1", "def-1");

        // Act & Assert: clear must NOT swallow exceptions.
        Assert.ThrowsAny<Exception>(() => store.Clear());
    }

    [Fact]
    public void Clear_On_Missing_Key_Should_Be_Noop()
    {
        // Arrange
        var store = new SqlGameplayStateStore(_db, "slot-missing", "def-missing");

        // Act & Assert: no throw when nothing to remove.
        store.Clear();
        Assert.Null(store.Read());
    }
}
