namespace DenoVideoPlayer.ViewModels;

public enum EdgePanelSide
{
    None,
    Recent,
    Playlist
}

public enum EdgePanelAction
{
    None,
    OpenRecent,
    OpenPlaylist,
    CloseRecent,
    ClosePlaylist
}

/// <summary>
/// Tracks deliberate edge-handle dwell and only dismisses panels opened by hover.
/// The caller owns rendering and acknowledges completed opens/closes explicitly.
/// </summary>
public sealed class EdgePanelHoverController
{
    public const double HandleWidth = 4;
    public const double HandleHeight = 96;
    // Give the thin visual handle a forgiving target without enlarging its UI.
    public const double HotZoneWidth = 20;
    public const double HotZoneHeight = 128;
    public const long OpenDelayMs = 120;
    // Brief excursions are tolerated; the existing slide handles the actual exit.
    public const long CloseDelayMs = 180;

    private EdgePanelSide _openedSide;
    private EdgePanelSide _pendingSide;
    private long _pendingSince;
    private long? _outsideSince;
    private bool _requiresHandleExit;

    public EdgePanelSide HoverOpenedSide { get; private set; }

    public static EdgePanelSide HitTest(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(width) || !double.IsFinite(height) ||
            width < 2 * HotZoneWidth || height < HotZoneHeight ||
            x < 0 || x >= width || y < 0 || y >= height)
            return EdgePanelSide.None;

        var hotZoneTop = (height - HotZoneHeight) / 2;
        if (y < hotZoneTop || y >= hotZoneTop + HotZoneHeight)
            return EdgePanelSide.None;
        if (x < HotZoneWidth)
            return EdgePanelSide.Recent;
        if (x >= width - HotZoneWidth)
            return EdgePanelSide.Playlist;
        return EdgePanelSide.None;
    }

    public EdgePanelAction Update(
        EdgePanelSide target,
        bool insidePanel,
        bool holdOpen,
        bool enabled,
        long now)
    {
        if (target == EdgePanelSide.None)
            _requiresHandleExit = false;

        if (!enabled)
        {
            ResetPending();
            _outsideSince = null;
            _requiresHandleExit = target != EdgePanelSide.None;
            return HoverOpenedSide != EdgePanelSide.None && !holdOpen
                ? CloseAction(HoverOpenedSide)
                : EdgePanelAction.None;
        }

        if (holdOpen)
        {
            ResetPending();
            _outsideSince = null;
            return EdgePanelAction.None;
        }

        if (!_requiresHandleExit && target != EdgePanelSide.None && target != _openedSide)
        {
            if (_pendingSide != target || now < _pendingSince)
            {
                _pendingSide = target;
                _pendingSince = now;
            }
            else if (now - _pendingSince >= OpenDelayMs)
            {
                // Keep returning the request until the UI confirms it via Opened.
                return OpenAction(target);
            }
        }
        else
        {
            ResetPending();
        }

        if (HoverOpenedSide == EdgePanelSide.None || target == HoverOpenedSide || insidePanel)
        {
            _outsideSince = null;
            return EdgePanelAction.None;
        }

        if (_outsideSince is null || now < _outsideSince.Value)
            _outsideSince = now;
        return now - _outsideSince.Value >= CloseDelayMs
            ? CloseAction(HoverOpenedSide)
            : EdgePanelAction.None;
    }

    public void Opened(EdgePanelSide side, bool byHover)
    {
        _openedSide = side;
        HoverOpenedSide = byHover ? side : EdgePanelSide.None;
        ResetPending();
        _outsideSince = null;
        _requiresHandleExit = false;
    }

    public void Closed(EdgePanelSide side)
    {
        if (_openedSide == side)
            _openedSide = EdgePanelSide.None;
        if (HoverOpenedSide == side)
            HoverOpenedSide = EdgePanelSide.None;
        ResetPending();
        _outsideSince = null;
        _requiresHandleExit = true;
    }

    private void ResetPending() => _pendingSide = EdgePanelSide.None;

    private static EdgePanelAction OpenAction(EdgePanelSide side) => side switch
    {
        EdgePanelSide.Recent => EdgePanelAction.OpenRecent,
        EdgePanelSide.Playlist => EdgePanelAction.OpenPlaylist,
        _ => EdgePanelAction.None
    };

    private static EdgePanelAction CloseAction(EdgePanelSide side) => side switch
    {
        EdgePanelSide.Recent => EdgePanelAction.CloseRecent,
        EdgePanelSide.Playlist => EdgePanelAction.ClosePlaylist,
        _ => EdgePanelAction.None
    };
}
