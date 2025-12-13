using DrawSound.Core.Utils;

namespace DrawSound.Tests;

public class UpdateThrottlerTests
{
    [Fact]
    public void Constructor_SetsThrottleInterval()
    {
        // Arrange & Act
        var throttler = new UpdateThrottler(100);

        // Assert
        Assert.Equal(100, throttler.ThrottleIntervalMs);
    }

    [Fact]
    public void ShouldUpdate_FirstCall_ReturnsTrue()
    {
        // Arrange
        var throttler = new UpdateThrottler(100);

        // Act
        bool result = throttler.ShouldUpdate();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldUpdate_ImmediateSecondCall_ReturnsFalse()
    {
        // Arrange
        var throttler = new UpdateThrottler(100);
        throttler.ShouldUpdate(); // First call

        // Act
        bool result = throttler.ShouldUpdate(); // Immediate second call

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldUpdate_AfterIntervalElapsed_ReturnsTrue()
    {
        // Arrange
        var throttler = new UpdateThrottler(10); // 10ms interval
        throttler.ShouldUpdate(); // First call
        
        // Wait for interval to pass
        Thread.Sleep(15);

        // Act
        bool result = throttler.ShouldUpdate();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldUpdate_MultipleFastCalls_OnlyFirstReturnsTrue()
    {
        // Arrange
        var throttler = new UpdateThrottler(100);
        
        // Act
        bool first = throttler.ShouldUpdate();
        bool second = throttler.ShouldUpdate();
        bool third = throttler.ShouldUpdate();

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.False(third);
    }

    [Fact]
    public void Reset_AllowsImmediateUpdate()
    {
        // Arrange
        var throttler = new UpdateThrottler(100);
        throttler.ShouldUpdate(); // First call

        // Act
        throttler.Reset();
        bool result = throttler.ShouldUpdate();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsDeferredUpdate_WhenThrottled_ReturnsTrue()
    {
        // Arrange
        var throttler = new UpdateThrottler(100);
        throttler.ShouldUpdate(); // First call - not throttled

        // Act
        throttler.ShouldUpdate(); // Second call - throttled

        // Assert
        Assert.True(throttler.NeedsDeferredUpdate);
    }

    [Fact]
    public void NeedsDeferredUpdate_AfterSuccessfulUpdate_ReturnsFalse()
    {
        // Arrange
        var throttler = new UpdateThrottler(10);
        throttler.ShouldUpdate(); // First call
        throttler.ShouldUpdate(); // Throttled - sets NeedsDeferredUpdate
        
        Thread.Sleep(15);
        throttler.ShouldUpdate(); // After interval - clears flag

        // Assert
        Assert.False(throttler.NeedsDeferredUpdate);
    }

    [Fact]
    public void GetDeferredDelayMs_ReturnsRemainingTime()
    {
        // Arrange
        var throttler = new UpdateThrottler(100);
        throttler.ShouldUpdate(); // First call

        // Act
        int delay = throttler.GetDeferredDelayMs();

        // Assert - should be close to 100ms but slightly less
        Assert.True(delay > 0 && delay <= 100, $"Delay was {delay}ms");
    }
}

