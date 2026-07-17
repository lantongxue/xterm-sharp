namespace XtermSharp.Avalonia.Demo.SSH.Services.Operations;

internal sealed record WriteOperation(byte[] Data) : ShellOperation;
