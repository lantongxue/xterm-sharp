namespace XtermSharp.Avalonia.Demo.SSH.Services.Operations;

internal sealed record ResizeOperation(int Columns, int Rows) : ShellOperation;
