using CoreGraphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace XtermSharp.Maui.Hosting;

internal sealed class IosTerminalInputEntryHandler : EntryHandler
{
    private UIView? _emptyInputAccessoryView;

    protected override void ConnectHandler(MauiTextField platformView)
    {
        base.ConnectHandler(platformView);
        platformView.InputAssistantItem.LeadingBarButtonGroups = [];
        platformView.InputAssistantItem.TrailingBarButtonGroups = [];
        _emptyInputAccessoryView = new UIView(CGRect.Empty);
        platformView.InputAccessoryView = _emptyInputAccessoryView;
    }

    protected override void DisconnectHandler(MauiTextField platformView)
    {
        platformView.InputAccessoryView = null;
        _emptyInputAccessoryView?.Dispose();
        _emptyInputAccessoryView = null;
        base.DisconnectHandler(platformView);
    }
}
