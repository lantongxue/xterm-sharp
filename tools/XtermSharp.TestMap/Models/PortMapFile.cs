namespace XtermSharp.TestMap.Models;

internal sealed record PortMapFile(int SchemaVersion, IReadOnlyList<PortMapEntry> Entries);
