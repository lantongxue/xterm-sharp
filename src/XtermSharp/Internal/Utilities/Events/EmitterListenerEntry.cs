namespace XtermSharp.Internal.Utilities;

internal sealed record EmitterListenerEntry<T>(Action<T> Listener);
