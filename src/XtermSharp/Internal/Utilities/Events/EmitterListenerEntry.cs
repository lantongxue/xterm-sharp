namespace XtermSharp.Internal.Utilities.Events;

internal sealed record EmitterListenerEntry<T>(Action<T> Listener);
