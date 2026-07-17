namespace XtermSharp.Parsing;

public readonly record struct FunctionIdentifier(char Final, char? Prefix = null, string Intermediates = "");
