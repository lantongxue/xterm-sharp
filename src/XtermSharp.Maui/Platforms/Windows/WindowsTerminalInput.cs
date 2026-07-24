using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using XtermSharp.Input;
using XtermSharp.Logging;
using XtermSharp.Maui.Input;
using XtermSharp.WinUI.Input;
using MauiTerminalView = XtermSharp.Maui.Controls.TerminalView;

namespace XtermSharp.Maui.Hosting;

internal static class WindowsTerminalInput
{
    private const string MapperKey = "XtermSharp.Maui.WindowsTerminalInput";
    private static readonly ConditionalWeakTable<FrameworkElement, Adapter> Adapters = new();
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) != 0)
        {
            return;
        }

        ContentViewHandler.Mapper.AppendToMapping(MapperKey, static (handler, view) =>
        {
            if (view is MauiTerminalView terminalView && handler.PlatformView is FrameworkElement platformView)
            {
                _ = Adapters.GetValue(platformView, _ => new Adapter(terminalView, platformView));
            }
        });
    }

    private sealed class Adapter
    {
        private readonly MauiTerminalView _terminalView;
        private readonly FrameworkElement _platformView;
        private readonly HashSet<VirtualKey> _pressedKeys = [];
        private readonly HashSet<VirtualKey> _suppressedReleases = [];

        public Adapter(MauiTerminalView terminalView, FrameworkElement platformView)
        {
            _terminalView = terminalView;
            _platformView = platformView;
            platformView.PreviewKeyDown += OnPreviewKeyDown;
            platformView.PreviewKeyUp += OnPreviewKeyUp;
            platformView.LostFocus += OnLostFocus;
            platformView.PointerWheelChanged += OnPointerWheelChanged;
        }

        private void OnLostFocus(object sender, RoutedEventArgs args)
        {
            _ = sender;
            _ = args;
            _pressedKeys.Clear();
            _suppressedReleases.Clear();
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            _ = sender;
            int delta = args.GetCurrentPoint(_platformView).Properties.MouseWheelDelta;
            if (delta == 0 || _terminalView.Terminal is null)
            {
                return;
            }

            Observe(_terminalView.ScrollWheelAsync(delta));
            args.Handled = true;
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            _ = sender;
            VirtualKey keyCode = args.Key;
            TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
            if (WinUIKeyMapper.ShouldCopy(keyCode, modifiers, _terminalView.HasSelection))
            {
                _suppressedReleases.Add(keyCode);
                Observe(_terminalView.CopySelectionAsync());
                args.Handled = true;
                return;
            }
            if (WinUIKeyMapper.ShouldPaste(keyCode, modifiers))
            {
                _suppressedReleases.Add(keyCode);
                Observe(_terminalView.PasteAsync());
                args.Handled = true;
                return;
            }
            if (WinUIKeyMapper.ShouldSelectAll(keyCode, modifiers))
            {
                _suppressedReleases.Add(keyCode);
                Observe(_terminalView.SelectAllAsync());
                args.Handled = true;
                return;
            }

            Terminal? terminal = _terminalView.Terminal;
            if (terminal is null)
            {
                return;
            }

            bool enhancedKeyboardMode = terminal.Options.AllowProposedApi &&
                (terminal.Modes.KittyKeyboardFlags != TerminalKittyKeyboardFlags.None ||
                 terminal.Modes.Win32InputMode);
            TerminalKeyEventType eventType = !_pressedKeys.Add(keyCode)
                ? TerminalKeyEventType.Repeat
                : TerminalKeyEventType.Press;
            TerminalKeyEvent key = WinUIKeyMapper.Create(
                keyCode,
                WinUIKeyMapper.GetText(keyCode, modifiers),
                modifiers,
                eventType);
            if (MauiTerminalInput.ShouldDeferHardwareKey(key, enhancedKeyboardMode))
            {
                // The focused MAUI Entry raises Completed for an unmodified hardware Enter.
                return;
            }
            if (WinUIKeyMapper.ShouldUseTextInput(key, enhancedKeyboardMode))
            {
                return;
            }

            Observe(_terminalView.SendKeyAsync(key));
            args.Handled = true;
        }

        private void OnPreviewKeyUp(object sender, KeyRoutedEventArgs args)
        {
            _ = sender;
            VirtualKey keyCode = args.Key;
            _pressedKeys.Remove(keyCode);
            if (_suppressedReleases.Remove(keyCode))
            {
                args.Handled = true;
                return;
            }

            Terminal? terminal = _terminalView.Terminal;
            if (terminal is null || !terminal.Options.AllowProposedApi ||
                terminal.Modes.KittyKeyboardFlags == TerminalKittyKeyboardFlags.None &&
                !terminal.Modes.Win32InputMode)
            {
                return;
            }

            TerminalModifiers modifiers = WinUIKeyMapper.GetModifiers();
            TerminalKeyEvent key = WinUIKeyMapper.Create(
                keyCode,
                WinUIKeyMapper.GetText(keyCode, modifiers),
                modifiers,
                TerminalKeyEventType.Release);
            Observe(_terminalView.SendKeyAsync(key));
            args.Handled = true;
        }

        private void Observe(ValueTask operation)
        {
            if (!operation.IsCompletedSuccessfully)
            {
                _ = ObserveAsync(operation);
            }
        }

        private void Observe(ValueTask<string> operation)
        {
            if (!operation.IsCompletedSuccessfully)
            {
                _ = ObserveAsync(operation);
            }
        }

        private async Task ObserveAsync(ValueTask operation)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                _terminalView.Terminal?.Options.Logger?.Log(
                    TerminalLogLevel.Error,
                    "A MAUI terminal input operation failed.",
                    exception);
            }
        }

        private async Task ObserveAsync(ValueTask<string> operation)
        {
            try
            {
                _ = await operation.ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                _terminalView.Terminal?.Options.Logger?.Log(
                    TerminalLogLevel.Error,
                    "A MAUI terminal input operation failed.",
                    exception);
            }
        }
    }
}
