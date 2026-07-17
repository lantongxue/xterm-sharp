namespace XtermSharp;

public readonly record struct FunctionIdentifier(char Final, char? Prefix = null, string Intermediates = "");
