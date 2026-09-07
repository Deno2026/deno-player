using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public sealed class DoubleClickGestureTrackerTests
{
    [Fact]
    public void SinglePressDoesNotCompleteGesture()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.False(Press(tracker, 0));
    }

    [Fact]
    public void FourPressesProduceTwoSeparatePairs()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.False(Press(tracker, 100));
        Assert.True(Press(tracker, 200));
        Assert.False(Press(tracker, 300));
        Assert.True(Press(tracker, 400));
    }

    [Theory]
    [InlineData(500u, 104.0, 104.0, true)]
    [InlineData(500u, 96.0, 96.0, true)]
    [InlineData(501u, 100.0, 100.0, false)]
    [InlineData(200u, 104.1, 100.0, false)]
    [InlineData(200u, 100.0, 104.1, false)]
    public void PairRequiresTimeAndBothDistanceLimits(
        uint elapsed,
        double secondX,
        double secondY,
        bool expected)
    {
        var tracker = new DoubleClickGestureTracker();
        Assert.False(Press(tracker, 100));

        Assert.Equal(expected, Press(tracker, 100 + elapsed, secondX, secondY));
    }

    [Fact]
    public void PressOutsidePairLimitsStartsANewPair()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.False(Press(tracker, 100));
        Assert.False(Press(tracker, 700, 250, 250));
        Assert.True(Press(tracker, 800, 251, 249));
    }

    [Fact]
    public void ResetDiscardsOnlyTheIncompletePair()
    {
        var tracker = new DoubleClickGestureTracker();
        Assert.False(Press(tracker, 100));
        Assert.True(tracker.TryAcceptGesture(100));

        tracker.ResetPress();

        Assert.False(Press(tracker, 200));
        Assert.True(Press(tracker, 300));
        Assert.False(tracker.TryAcceptGesture(100));
        Assert.False(tracker.TryAcceptGesture(99));
        Assert.True(tracker.TryAcceptGesture(300));
    }

    [Fact]
    public void DuplicateOrLatePressCannotCompleteOrReplacePendingPair()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.False(Press(tracker, 100));
        Assert.False(Press(tracker, 100));
        Assert.False(Press(tracker, 90, 500, 500));
        Assert.True(Press(tracker, 200));
        Assert.False(Press(tracker, 200));
        Assert.False(Press(tracker, 300));
        Assert.True(Press(tracker, 400));
    }

    [Fact]
    public void TimestampWrapDoesNotBreakPairOrGestureOrder()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.False(Press(tracker, uint.MaxValue - 100));
        Assert.True(Press(tracker, 99));
        Assert.True(tracker.TryAcceptGesture(uint.MaxValue - 100));
        Assert.True(tracker.TryAcceptGesture(99));
        Assert.False(tracker.TryAcceptGesture(uint.MaxValue - 50));
        Assert.False(tracker.TryAcceptGesture(99));
        Assert.True(tracker.TryAcceptGesture(100));
    }

    [Fact]
    public void SecondInputChannelCannotAcceptTheSameOrAnOlderGesture()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.True(tracker.TryAcceptGesture(100));
        Assert.False(tracker.TryAcceptGesture(100));
        Assert.False(tracker.TryAcceptGesture(99));
        Assert.True(tracker.TryAcceptGesture(200));
        Assert.False(tracker.TryAcceptGesture(100));
    }

    [Fact]
    public void ANewRapidGestureHasNoCooldown()
    {
        var tracker = new DoubleClickGestureTracker();

        Assert.True(tracker.TryAcceptGesture(0));
        Assert.True(tracker.TryAcceptGesture(1));
        Assert.True(tracker.TryAcceptGesture(100));
        Assert.True(tracker.TryAcceptGesture(200));
    }

    private static bool Press(
        DoubleClickGestureTracker tracker,
        uint timestamp,
        double x = 100,
        double y = 100) =>
        tracker.RecordPress(timestamp, x, y, 500, 4, 4);
}
