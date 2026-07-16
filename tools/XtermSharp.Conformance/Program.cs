using System.Text.Json;
using XtermSharp;

string json = await Console.In.ReadToEndAsync();
using JsonDocument document = JsonDocument.Parse(json);
JsonElement root = document.RootElement;
JsonElement optionsElement = root.TryGetProperty("options", out JsonElement configuredOptions)
    ? configuredOptions
    : default;

var options = new TerminalOptions
{
    Columns = GetInt(optionsElement, "cols", GetInt(optionsElement, "columns", 80)),
    Rows = GetInt(optionsElement, "rows", 24),
    Scrollback = GetInt(optionsElement, "scrollback", 1000),
    ConvertEol = GetBool(optionsElement, "convertEol", false)
};

await using var terminal = new Terminal(options);
var events = new List<object>();
terminal.Bell += (_, _) => events.Add(new { type = "bell" });
terminal.Data += (_, args) => events.Add(new { type = "data", data = args.Data });
terminal.CursorMoved += (_, _) => events.Add(new { type = "cursor" });
terminal.LineFeed += (_, _) => events.Add(new { type = "lineFeed" });
terminal.Resized += (_, args) => events.Add(new { type = "resize", cols = args.Columns, rows = args.Rows });
terminal.Scrolled += (_, args) => events.Add(new { type = "scroll", viewportY = args.ViewportY });
terminal.TitleChanged += (_, args) => events.Add(new { type = "title", title = args.Title });

if (root.TryGetProperty("operations", out JsonElement operations))
{
    foreach (JsonElement operation in operations.EnumerateArray())
    {
        string type = operation.GetProperty("type").GetString() ?? string.Empty;
        switch (type)
        {
            case "write":
                await terminal.WriteAsync(operation.TryGetProperty("data", out JsonElement text) ? text.GetString() ?? string.Empty : string.Empty);
                break;
            case "writeBytes":
                await terminal.WriteAsync(operation.GetProperty("data").EnumerateArray().Select(static value => value.GetByte()).ToArray());
                break;
            case "resize":
                await terminal.ResizeAsync(operation.GetProperty("columns").GetInt32(), operation.GetProperty("rows").GetInt32());
                break;
            case "reset":
                await terminal.ResetAsync();
                break;
            case "clear":
                await terminal.ClearAsync();
                break;
            case "scrollLines":
                await terminal.ScrollLinesAsync(operation.GetProperty("amount").GetInt32());
                break;
            case "scrollToLine":
                await terminal.ScrollToLineAsync(operation.GetProperty("line").GetInt32());
                break;
            default:
                throw new InvalidOperationException($"Unknown operation '{type}'.");
        }
    }
}

TerminalSnapshot snapshot = await terminal.GetSnapshotAsync(SnapshotScope.AllBuffers);
var result = new
{
    columns = snapshot.Columns,
    rows = snapshot.Rows,
    activeBuffer = BufferName(snapshot.ActiveBufferKind),
    modes = new
    {
        applicationCursorKeysMode = snapshot.Modes.ApplicationCursorKeys,
        applicationKeypadMode = snapshot.Modes.ApplicationKeypad,
        bracketedPasteMode = snapshot.Modes.BracketedPaste,
        insertMode = snapshot.Modes.Insert,
        mouseTrackingMode = MouseName(snapshot.Modes.MouseTracking),
        originMode = snapshot.Modes.Origin,
        reverseWraparoundMode = snapshot.Modes.ReverseWraparound,
        sendFocusMode = snapshot.Modes.SendFocus,
        showCursor = snapshot.Modes.ShowCursor,
        synchronizedOutputMode = snapshot.Modes.SynchronizedOutput,
        win32InputMode = snapshot.Modes.Win32InputMode,
        wraparoundMode = snapshot.Modes.Wraparound
    },
    normal = Normalize(snapshot.NormalBuffer!),
    alternate = Normalize(snapshot.AlternateBuffer!),
    events
};

Console.Write(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false }));

static object Normalize(TerminalBufferSnapshot buffer) => new
{
    kind = BufferName(buffer.Kind),
    cursorX = buffer.CursorX,
    cursorY = buffer.CursorY,
    viewportY = buffer.ViewportY,
    baseY = buffer.BaseY,
    lines = buffer.Lines.Select(line => new
    {
        wrapped = line.IsWrapped,
        cells = line.Cells.Select(cell => new
        {
            text = cell.Text,
            codePoint = cell.CodePoint,
            width = cell.Width,
            foregroundMode = ColorName(cell.Foreground.Mode),
            foreground = ColorValue(cell.Foreground),
            backgroundMode = ColorName(cell.Background.Mode),
            background = ColorValue(cell.Background),
            bold = cell.Attributes.HasFlag(CellAttributes.Bold),
            dim = cell.Attributes.HasFlag(CellAttributes.Dim),
            italic = cell.Attributes.HasFlag(CellAttributes.Italic),
            underline = cell.Attributes.HasFlag(CellAttributes.Underline),
            blink = cell.Attributes.HasFlag(CellAttributes.Blink),
            inverse = cell.Attributes.HasFlag(CellAttributes.Inverse),
            invisible = cell.Attributes.HasFlag(CellAttributes.Invisible),
            strikethrough = cell.Attributes.HasFlag(CellAttributes.Strikethrough),
            overline = cell.Attributes.HasFlag(CellAttributes.Overline)
        })
    })
};

static string BufferName(TerminalBufferKind kind) => kind == TerminalBufferKind.Normal ? "normal" : "alternate";
static string ColorName(TerminalColorMode mode) => mode switch
{
    TerminalColorMode.Palette => "palette",
    TerminalColorMode.Rgb => "rgb",
    _ => "default"
};
static int ColorValue(TerminalColor color) =>
    color.Mode == TerminalColorMode.Default ? -1 : color.Value;
static string MouseName(TerminalMouseTrackingMode mode) => mode switch
{
    TerminalMouseTrackingMode.X10 => "x10",
    TerminalMouseTrackingMode.Vt200 => "vt200",
    TerminalMouseTrackingMode.Drag => "drag",
    TerminalMouseTrackingMode.Any => "any",
    _ => "none"
};
static int GetInt(JsonElement element, string name, int fallback) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : fallback;
static bool GetBool(JsonElement element, string name, bool fallback) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) ? value.GetBoolean() : fallback;
