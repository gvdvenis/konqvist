using Konqvist.Data.Infrastructure;

namespace Konqvist.Data.Tests;

public class GameplayStatePersistenceOptionsTests
{
    [Fact]
    public void ClampInterval_Should_Return_Minimum_Of_One_Second_For_Below_Bound()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromMilliseconds(50)
        };

        // Act
        TimeSpan clamped = options.ClampInterval();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(1), clamped);
    }

    [Fact]
    public void ClampInterval_Should_Return_Maximum_Of_Sixty_Seconds_For_Above_Bound()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromMinutes(5)
        };

        // Act
        TimeSpan clamped = options.ClampInterval();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(60), clamped);
    }

    [Fact]
    public void ClampInterval_Should_Return_Original_Value_When_Within_Bounds()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromSeconds(15)
        };

        // Act
        TimeSpan clamped = options.ClampInterval();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(15), clamped);
    }

    [Fact]
    public void ClampInterval_Should_Preserve_Default_Of_One_Second()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions();

        // Act
        TimeSpan clamped = options.ClampInterval();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(1), clamped);
    }

    [Fact]
    public void IsValid_Should_Return_False_For_Interval_Below_One_Second()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromMilliseconds(50)
        };

        // Act
        bool valid = options.IsValid(out string error);

        // Assert
        Assert.False(valid);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_Should_Return_False_For_Interval_Above_Sixty_Seconds()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromSeconds(90)
        };

        // Act
        bool valid = options.IsValid(out string error);

        // Assert
        Assert.False(valid);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_Should_Return_True_For_Interval_Within_Bounds()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromSeconds(30)
        };

        // Act
        bool valid = options.IsValid(out string error);

        // Assert
        Assert.True(valid);
        Assert.True(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_Should_Return_True_For_Default_Interval()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions();

        // Act
        bool valid = options.IsValid(out string error);

        // Assert
        Assert.True(valid);
        Assert.True(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_Should_Return_True_For_Exact_Boundaries()
    {
        // Arrange
        var minOptions = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromSeconds(1)
        };
        var maxOptions = new GameplayStatePersistenceOptions
        {
            SaveInterval = TimeSpan.FromSeconds(60)
        };

        // Act
        bool minValid = minOptions.IsValid(out _);
        bool maxValid = maxOptions.IsValid(out _);

        // Assert
        Assert.True(minValid);
        Assert.True(maxValid);
    }

    [Fact]
    public void Defaults_Should_Match_Spec()
    {
        // Arrange
        var options = new GameplayStatePersistenceOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(1), options.SaveInterval);
        Assert.Equal("default", options.Slot);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownFlushTimeout);
    }
}
