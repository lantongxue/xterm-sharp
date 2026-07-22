# Architecture

```text
WriteAsync / ResizeAsync / ResetAsync
                  |
        pending-byte limiter
                  |
       ordered command channel
                  |
 UTF decoder -> VT parser -> terminal engine
                  |
       normal/alternate buffers
                  |
      immutable snapshots + events
```

Each `Terminal` owns one processor task. The mutable parser, modes, cursor and
buffers are accessed only by that task. This preserves terminal stream ordering
without exposing locks or live buffer objects to consumers.

The engine and public parser facade share one `EscapeSequenceParser` instance, so
the VT500 state machine exercised by parser conformance tests is also the production
write path. Input remains Rune-streamed and decoder state is preserved between writes.

Raw byte input uses a streaming decoder compatible with xterm.js: incomplete
UTF-8 sequences continue in the next write and malformed sequences are dropped.
String input separately preserves split surrogate pairs between writes.

Parser handlers are tried newest-first. Returning `false` falls through to an
older handler and finally the built-in implementation. A handler may await;
the parser remains at the current sequence until it completes. A handler exception
fails the current write without falling through, aborts active string handlers and
resets the parser to Ground so the next queued command can proceed. Any prefix that
was already executed is committed to the immutable snapshot with a new revision and
its events are dispatched, but the failed write does not emit `WriteParsed`.

Public terminals have an effective minimum width of two columns, matching xterm.js'
wide-cell invariant. A requested `Columns` option of one remains visible as the raw
option value, while buffers, snapshots and resize events report two effective columns.

Events are raised after a command commits and carry the resulting revision.
They run on the terminal processor task without capturing a synchronization
context. Subscriber exceptions are logged and do not stop other subscribers.

Optional UI rendering is implemented in separate packages. The rendering
controller turns immutable viewport snapshots into backend-neutral display
lists, the Skia package executes those lists with HarfBuzz shaping, and the
Avalonia and Windows Forms controls own platform dispatch, DPI, input, clipboard and IME. See
[rendering-architecture.md](rendering-architecture.md) for the detailed contract.
