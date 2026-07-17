namespace XtermSharp.Rendering.Skia.Backends;

internal readonly record struct TypefaceKey(string Family, bool Bold, bool Italic, int CodePoint);
