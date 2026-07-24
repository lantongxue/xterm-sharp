using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using XtermSharp.Maui.Input;

namespace XtermSharp.Maui.Hosting;

internal sealed class AndroidTerminalInputEntryHandler : EntryHandler
{
    protected override MauiAppCompatEditText CreatePlatformView() =>
        new AndroidTerminalEditText(Context);

    protected override void ConnectHandler(MauiAppCompatEditText platformView)
    {
        base.ConnectHandler(platformView);
        ((AndroidTerminalEditText)platformView).BackspaceRequested += OnBackspaceRequested;
    }

    protected override void DisconnectHandler(MauiAppCompatEditText platformView)
    {
        ((AndroidTerminalEditText)platformView).BackspaceRequested -= OnBackspaceRequested;
        base.DisconnectHandler(platformView);
    }

    private void OnBackspaceRequested(int count)
    {
        if (VirtualView is TerminalInputEntry inputEntry)
        {
            inputEntry.RequestBackspace(count);
        }
    }
}

internal sealed class AndroidTerminalEditText(Context context) : MauiAppCompatEditText(context)
{
    public event Action<int>? BackspaceRequested;

    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
    {
        IInputConnection? connection = base.OnCreateInputConnection(outAttrs);
        return connection is null
            ? null
            : new TerminalInputConnection(connection, this);
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (keyCode == Keycode.Del && TryRequestBackspace(1, 0))
        {
            return true;
        }
        return base.OnKeyDown(keyCode, e);
    }

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e)
    {
        if (keyCode == Keycode.Del && IsSentinelSelected())
        {
            return true;
        }
        return base.OnKeyUp(keyCode, e);
    }

    private bool TryRequestBackspace(int beforeLength, int afterLength)
    {
        int count = MauiTextInputTranslator.GetNativeBackspaceCount(
            Text,
            beforeLength,
            afterLength);
        if (count == 0)
        {
            return false;
        }
        BackspaceRequested?.Invoke(count);
        return true;
    }

    private bool IsSentinelSelected() =>
        MauiTextInputTranslator.GetNativeBackspaceCount(Text, 1, 0) != 0;

    private sealed class TerminalInputConnection(
        IInputConnection target,
        AndroidTerminalEditText editText) : InputConnectionWrapper(target, false)
    {
        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
        {
            return editText.TryRequestBackspace(beforeLength, afterLength) ||
                base.DeleteSurroundingText(beforeLength, afterLength);
        }

        public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
        {
            return editText.TryRequestBackspace(beforeLength, afterLength) ||
                base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
        }

        public override bool SendKeyEvent(KeyEvent? e)
        {
            if (e?.KeyCode != Keycode.Del || !editText.IsSentinelSelected())
            {
                return base.SendKeyEvent(e);
            }
            if (e.Action == KeyEventActions.Down)
            {
                _ = editText.TryRequestBackspace(1, 0);
            }
            return true;
        }
    }
}
