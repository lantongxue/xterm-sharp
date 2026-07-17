namespace XtermSharp.Avalonia.Demo.SSH;

internal sealed record WriteOperation(byte[] Data) : ShellOperation;
