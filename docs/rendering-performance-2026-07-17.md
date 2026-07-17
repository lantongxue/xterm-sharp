# Rendering performance optimization log — 2026-07-17

## Summary

This change addresses low Avalonia rendering throughput during sustained output and full-screen
terminal applications such as `btop`. Before the optimization, interactive observation showed the
control struggling to exceed roughly 20 FPS under these workloads.

The work covers all three rendering layers:

- backend-neutral display-list compilation in `XtermSharp.Rendering`;
- retained drawing and text execution in `XtermSharp.Rendering.Skia`;
- frame scheduling, invalidation and binding notification in `XtermSharp.Avalonia`.

On the repeatable 120-column by 40-row dashboard benchmark added with this change, the final Release
build reaches 129.37 FPS and emits 519 display commands per frame. The old per-cell command model
would emit approximately 9,640 commands for the same styled-cell layout, so run batching removes
about 94.6% of the display commands before they reach Skia.

## Main bottlenecks

### Per-cell text shaping

Every non-empty terminal cell previously became an independent `TerminalTextCommand`. Recording a
changed row therefore created a font and paint, measured the text and invoked HarfBuzz once for each
cell. A typical 120-column dashboard could perform thousands of shaping operations for one frame.

### Per-cell background drawing

Styled backgrounds were also emitted one rectangle at a time, even when adjacent cells shared the
same resolved color. Full-screen process monitors commonly use long runs with identical foreground
and background attributes, making this overhead especially visible.

### Work on the Avalonia render path

`SKPicture` rows were recorded lazily from the custom draw operation. The Avalonia compositor thread
therefore paid the cost of font selection, text shaping and picture recording before it could replay
the frame.

### Broad invalidation

Focus, cursor blink, text blink and IME preedit changes could force content rows to be rebuilt more
widely than necessary. Frames with no actual pixel changes were still treated as full damage, and
the custom draw operation never compared equal to an existing operation.

### Binding notifications

Viewport position, scroll extent, columns and rows were plain properties. Consumers either lacked
change notifications or needed additional polling and synchronization around frame publication.

## Implemented changes

### Display-list batching and damage tracking

- Adjacent printable ASCII cells with compatible style, color and geometry are compiled into one
  `TerminalTextCommand` with an explicit terminal-cell count.
- Adjacent cells with the same non-default background are compiled into one fill command.
- Unchanged rows continue to reuse immutable line snapshots and cached display rows.
- Dirty-row ranges are merged by revision, while configuration and blink generations prevent a
  concurrent invalidation from being lost during asynchronous frame preparation.
- Cursor overlays are cached separately from row content.
- Blink phase changes rebuild only rows that actually contain blinking text.
- `TerminalDamage.Empty` represents frames that do not change pixels.

### Skia retained rendering

- Font metrics, `SKFont` objects and common fill paints are cached.
- Printable fixed-pitch ASCII runs use Skia's direct text path, preserving the prior non-ligature
  cell behavior without paying HarfBuzz shaping cost.
- Complex text and fallback clusters continue through HarfBuzz.
- Changed rows are recorded into `SKPicture` objects during frame preparation instead of during the
  Avalonia draw callback.
- Stale row pictures are evicted without discarding pictures that are still used by the current
  frame.

### Avalonia scheduling and bindings

- Snapshot acquisition, display-list compilation and Skia picture preparation run on the worker
  pool.
- The UI thread publishes completed immutable frames and invalidates the visual only when damage is
  non-empty.
- The custom draw operation compares equal when the retained backend, frame and bounds are unchanged.
- Scroll position, scroll maximum, columns and rows are direct Avalonia properties, with notifications
  raised only when their values change.
- Terminal detach and replacement still cancel pending preparation and do not dispose the externally
  owned terminal.

## Repeatable benchmark

The benchmark is part of `benchmarks/XtermSharp.Benchmarks` and runs with:

```bash
dotnet run --project benchmarks/XtermSharp.Benchmarks/XtermSharp.Benchmarks.csproj -c Release
```

The rendering workload uses:

- 120 columns and 40 rows;
- six independently colored segments per row;
- a full-screen content update on every iteration;
- 120 measured iterations after warm-up;
- terminal parsing, viewport snapshot creation, display-list compilation, `SKPicture` preparation,
  canvas replay and flush inside the measured path.

Final result on the development macOS arm64 environment:

| Measurement | Result |
| --- | ---: |
| Full dashboard rendering | 129.37 FPS |
| Display commands per frame | 519 |
| Allocated over 120 frames | 398.07 MiB |
| Approximate old per-cell commands | 9,640 |
| Approximate command reduction | 94.6% |

The benchmark is deliberately CPU-heavy and headless. Actual Avalonia presentation rate remains
bounded by display refresh, compositor behavior, DPI, font fallback, PTY chunking and the mix of
ASCII and complex Unicode text. Its purpose is to provide a stable regression workload rather than
promise a fixed application FPS on every system.

## Verification

The final tree was verified with:

- zero build warnings and errors;
- 1,425/1,425 main tests passing;
- 1/1 reference infrastructure test passing;
- 21/21 rendering, Skia and Avalonia tests passing;
- 1,307 unique upstream bindings verified;
- the sample differential scenario reporting `MATCH`;
- all 76 escape-sequence fixtures matching the pinned xterm.js headless oracle.

New regressions specifically cover compatible text/background run batching, empty damage for
unchanged frames, blink-only row rebuilding and retained Skia picture reuse.

## Remaining opportunities

The benchmark still allocates about 3.3 MiB per full-screen iteration, including generated terminal
input, immutable snapshots, command strings and display-list objects. Future work can investigate
row-local picture reuse during scrolling, pooled run builders, packed snapshot cells and more
targeted Unicode-run batching without weakening fallback or cell-clipping semantics.
