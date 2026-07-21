# Progress addon

`XtermSharp.Addons.Progress` ports the pinned xterm.js 6.0.0 `addon-progress` behavior to .NET. It
tracks ConEmu OSC 9;4 sequences without adding rendering or platform dependencies to the headless
core package.

## Usage

```csharp
using XtermSharp.Addons.Progress;

var addon = new ProgressAddon();
terminal.LoadAddon(addon);

addon.ProgressChanged += (_, args) =>
{
    ProgressType state = args.State;
    int percentage = args.Value;
    // Update an application-owned taskbar, badge or progress control.
};
```

The terminal sequence is `ESC ] 9 ; 4 ; <state> ; <value> BEL` (the string terminator form is also
accepted by the core parser). State values have the same meaning as upstream:

- `Remove` (0) removes progress and resets the value to zero.
- `Set` (1) reports normal percentage-based progress.
- `Error` (2) reports failure and preserves the previous value when the supplied value is empty or
  zero.
- `Indeterminate` (3) reports activity without a known percentage and preserves the previous value.
- `Pause` (4) reports a paused or warning state and preserves the previous value when the supplied
  value is empty or zero.

Values greater than 100 are clamped to 100. Sequence values use strict decimal parsing; malformed
values and unsupported states are consumed without changing progress or raising an event.

## Programmatic state

`ProgressAddon.Progress` can be read or assigned to reset a stuck indicator or restore application
state. Programmatic values are clamped to 0 through 100 and valid assignments raise the same event
as terminal sequences.

```csharp
ProgressState saved = addon.Progress;
addon.Progress = new ProgressState(ProgressType.Remove, 0);
addon.Progress = saved;
```

Disposing the addon unregisters its OSC handler and clears its event subscriptions. OSC 9 payloads
that do not begin with `4;` fall through to older registered handlers.
