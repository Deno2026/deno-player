using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public sealed class EdgePanelHoverControllerTests
{
    [Theory]
    [InlineData(0, 100, EdgePanelSide.Recent)]
    [InlineData(9.99, 100, EdgePanelSide.Recent)]
    [InlineData(10, 100, EdgePanelSide.Recent)]
    [InlineData(19.99, 100, EdgePanelSide.Recent)]
    [InlineData(20, 100, EdgePanelSide.None)]
    [InlineData(100, 100, EdgePanelSide.None)]
    [InlineData(179.99, 100, EdgePanelSide.None)]
    [InlineData(180, 100, EdgePanelSide.Playlist)]
    [InlineData(189.99, 100, EdgePanelSide.Playlist)]
    [InlineData(190, 100, EdgePanelSide.Playlist)]
    [InlineData(199.99, 100, EdgePanelSide.Playlist)]
    [InlineData(200, 100, EdgePanelSide.None)]
    [InlineData(-0.01, 100, EdgePanelSide.None)]
    [InlineData(1, -1, EdgePanelSide.None)]
    [InlineData(1, 35.99, EdgePanelSide.None)]
    [InlineData(1, 36, EdgePanelSide.Recent)]
    [InlineData(1, 163.99, EdgePanelSide.Recent)]
    [InlineData(1, 164, EdgePanelSide.None)]
    [InlineData(199, 36, EdgePanelSide.Playlist)]
    [InlineData(199, 164, EdgePanelSide.None)]
    [InlineData(199, 200, EdgePanelSide.None)]
    public void HitTestOnlyAcceptsPaddedCenterZonesInsideTheRoot(double x, double y, EdgePanelSide expected)
    {
        Assert.Equal(expected, EdgePanelHoverController.HitTest(x, y, 200, 200));
    }

    [Theory]
    [InlineData(double.NaN, 100, 200, 200)]
    [InlineData(0, double.NaN, 200, 200)]
    [InlineData(0, 100, double.NaN, 200)]
    [InlineData(0, 100, 200, double.NaN)]
    [InlineData(double.PositiveInfinity, 100, 200, 200)]
    [InlineData(0, double.NegativeInfinity, 200, 200)]
    [InlineData(0, 100, double.PositiveInfinity, 200)]
    [InlineData(0, 100, 200, double.PositiveInfinity)]
    [InlineData(0, 50, 39, 200)]
    [InlineData(0, 50, 200, 127)]
    [InlineData(0, 0, 0, 0)]
    public void HitTestRejectsNonFiniteAndUndersizedGeometry(double x, double y, double width, double height)
    {
        Assert.Equal(EdgePanelSide.None, EdgePanelHoverController.HitTest(x, y, width, height));
    }

    [Fact]
    public void SmallestValidRootKeepsLeftAndRightRegionsDistinct()
    {
        Assert.Equal(EdgePanelSide.Recent, EdgePanelHoverController.HitTest(19.99, 0, 40, 128));
        Assert.Equal(EdgePanelSide.Playlist, EdgePanelHoverController.HitTest(20, 127.99, 40, 128));
        Assert.Equal(EdgePanelSide.None, EdgePanelHoverController.HitTest(20, 128, 40, 128));
    }

    [Fact]
    public void HoverTargetGrowsWithoutChangingVisualHandleSize()
    {
        Assert.Equal(4, EdgePanelHoverController.HandleWidth);
        Assert.Equal(96, EdgePanelHoverController.HandleHeight);
        Assert.Equal(20, EdgePanelHoverController.HotZoneWidth);
        Assert.Equal(128, EdgePanelHoverController.HotZoneHeight);
        Assert.Equal(120, EdgePanelHoverController.OpenDelayMs);
        Assert.Equal(180, EdgePanelHoverController.CloseDelayMs);
    }

    [Theory]
    [InlineData(EdgePanelSide.Recent, EdgePanelAction.OpenRecent)]
    [InlineData(EdgePanelSide.Playlist, EdgePanelAction.OpenPlaylist)]
    public void StableHandleRequiresFullDwellAndAcknowledgedOpen(EdgePanelSide side, EdgePanelAction expected)
    {
        var controller = new EdgePanelHoverController();
        Assert.Equal(EdgePanelAction.None, Tick(controller, side, 0));
        Assert.Equal(EdgePanelAction.None, Tick(controller, side, 119));
        Assert.Equal(expected, Tick(controller, side, 120));
        Assert.Equal(EdgePanelSide.None, controller.HoverOpenedSide);
        Assert.Equal(expected, Tick(controller, side, 121));
        controller.Opened(side, byHover: true);
        Assert.Equal(side, controller.HoverOpenedSide);
        Assert.Equal(EdgePanelAction.None, Tick(controller, side, 122));
    }

    [Fact]
    public void LeavingOrChangingHandleRestartsTheDwell()
    {
        var controller = new EdgePanelHoverController();
        Tick(controller, EdgePanelSide.Recent, 0);
        Tick(controller, EdgePanelSide.None, 119);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 120));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 239));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 358));
        Assert.Equal(EdgePanelAction.OpenPlaylist, Tick(controller, EdgePanelSide.Playlist, 359));
    }

    [Theory]
    [InlineData(EdgePanelSide.Recent, EdgePanelAction.CloseRecent)]
    [InlineData(EdgePanelSide.Playlist, EdgePanelAction.ClosePlaylist)]
    public void HoverPanelClosesOnlyAfterFullOutsideDelay(EdgePanelSide side, EdgePanelAction expected)
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(side, byHover: true);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 100));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 279));
        Assert.Equal(expected, Tick(controller, EdgePanelSide.None, 280));
        Assert.Equal(side, controller.HoverOpenedSide);
        Assert.Equal(expected, Tick(controller, EdgePanelSide.None, 281));
        controller.Closed(side);
        Assert.Equal(EdgePanelSide.None, controller.HoverOpenedSide);
    }

    [Fact]
    public void ReturningToHandleOrPanelRestartsCloseDelay()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Recent, byHover: true);
        Tick(controller, EdgePanelSide.None, 0);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 179));
        Tick(controller, EdgePanelSide.None, 200);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 379, insidePanel: true));
        Tick(controller, EdgePanelSide.None, 400);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 579));
        Assert.Equal(EdgePanelAction.CloseRecent, Tick(controller, EdgePanelSide.None, 580));
    }

    [Fact]
    public void HoldProtectsPanelAndBlocksOppositeHandleUntilAFullFreshDwell()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Recent, byHover: true);
        Tick(controller, EdgePanelSide.None, 0);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 400, holdOpen: true));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 1000, holdOpen: true));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 1001));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 1120));
        Assert.Equal(EdgePanelAction.OpenPlaylist, Tick(controller, EdgePanelSide.Playlist, 1121));
    }

    [Fact]
    public void OppositeHandleCanSwitchBeforeTheOldPanelCloseDelay()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Recent, byHover: true);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 0));
        Assert.Equal(EdgePanelAction.OpenPlaylist, Tick(controller, EdgePanelSide.Playlist, 120));
        Assert.Equal(EdgePanelSide.Recent, controller.HoverOpenedSide);
        controller.Closed(EdgePanelSide.Recent);
        controller.Opened(EdgePanelSide.Playlist, byHover: true);
        Assert.Equal(EdgePanelSide.Playlist, controller.HoverOpenedSide);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 121));
    }

    [Fact]
    public void ClosingUnderAStationaryHandleRequiresExitBeforeReopening()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Recent, byHover: true);
        controller.Closed(EdgePanelSide.Recent);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 0));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 1000));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 2000));
        Tick(controller, EdgePanelSide.None, 2001);
        Tick(controller, EdgePanelSide.Recent, 2002);
        Assert.Equal(EdgePanelAction.OpenRecent, Tick(controller, EdgePanelSide.Recent, 2122));
    }

    [Fact]
    public void ManualOpenDoesNotAcquireHoverOwnershipOrAutoClose()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Recent, byHover: false);
        Assert.Equal(EdgePanelSide.None, controller.HoverOpenedSide);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 0));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 1000));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 2000));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 3000));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 4000, enabled: false));
    }

    [Fact]
    public void ExplicitOpenCanPinAnAlreadyHoverOpenedPanelAndClearRearm()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Recent, byHover: true);
        controller.Closed(EdgePanelSide.Recent);
        controller.Opened(EdgePanelSide.Recent, byHover: false);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 0));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 1000));
        Assert.Equal(EdgePanelSide.None, controller.HoverOpenedSide);
        Tick(controller, EdgePanelSide.Playlist, 1001);
        Assert.Equal(EdgePanelAction.OpenPlaylist, Tick(controller, EdgePanelSide.Playlist, 1121));
    }

    [Fact]
    public void ClosingAnotherPanelDoesNotDropCurrentHoverOwnership()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Playlist, byHover: true);
        controller.Closed(EdgePanelSide.Recent);
        Assert.Equal(EdgePanelSide.Playlist, controller.HoverOpenedSide);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 0));
    }

    [Fact]
    public void DisabledImmediatelyDismissesOnlyHoverPanelsUnlessHeld()
    {
        var controller = new EdgePanelHoverController();
        controller.Opened(EdgePanelSide.Playlist, byHover: true);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Playlist, 0, holdOpen: true, enabled: false));
        Assert.Equal(EdgePanelAction.ClosePlaylist, Tick(controller, EdgePanelSide.Playlist, 1, enabled: false));
        Assert.Equal(EdgePanelAction.ClosePlaylist, Tick(controller, EdgePanelSide.Playlist, 2, enabled: false));
        Assert.Equal(EdgePanelSide.Playlist, controller.HoverOpenedSide);
    }

    [Fact]
    public void DisabledHandleRequiresExitAndFreshDwellAfterReenable()
    {
        var controller = new EdgePanelHoverController();
        Tick(controller, EdgePanelSide.Recent, 0);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 120, enabled: false));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 121));
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 1000));
        Tick(controller, EdgePanelSide.None, 1001);
        Tick(controller, EdgePanelSide.Recent, 1002);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 1121));
        Assert.Equal(EdgePanelAction.OpenRecent, Tick(controller, EdgePanelSide.Recent, 1122));
    }

    [Fact]
    public void LeavingWhileDisabledRearmsWithoutAnExtraEnabledExit()
    {
        var controller = new EdgePanelHoverController();
        Tick(controller, EdgePanelSide.Recent, 0, enabled: false);
        Tick(controller, EdgePanelSide.None, 1, enabled: false);
        Tick(controller, EdgePanelSide.Playlist, 2);
        Assert.Equal(EdgePanelAction.OpenPlaylist, Tick(controller, EdgePanelSide.Playlist, 122));
    }

    [Fact]
    public void RewindingClockRestartsPendingDurations()
    {
        var controller = new EdgePanelHoverController();
        Tick(controller, EdgePanelSide.Recent, 1000);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.Recent, 0));
        Assert.Equal(EdgePanelAction.OpenRecent, Tick(controller, EdgePanelSide.Recent, 120));
        controller.Opened(EdgePanelSide.Recent, byHover: true);
        Tick(controller, EdgePanelSide.None, 1000);
        Assert.Equal(EdgePanelAction.None, Tick(controller, EdgePanelSide.None, 0));
        Assert.Equal(EdgePanelAction.CloseRecent, Tick(controller, EdgePanelSide.None, 180));
    }

    private static EdgePanelAction Tick(
        EdgePanelHoverController controller,
        EdgePanelSide target,
        long now,
        bool insidePanel = false,
        bool holdOpen = false,
        bool enabled = true) =>
        controller.Update(target, insidePanel, holdOpen, enabled, now);
}
