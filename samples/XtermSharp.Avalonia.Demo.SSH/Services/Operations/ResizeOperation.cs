namespace XtermSharp.Avalonia.Demo.SSH;

internal sealed record ResizeOperation(int Columns, int Rows) : ShellOperation;
