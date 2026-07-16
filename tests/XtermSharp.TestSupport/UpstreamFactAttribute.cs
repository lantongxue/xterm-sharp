using System.Runtime.CompilerServices;

namespace XtermSharp.TestSupport;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class UpstreamFactAttribute : FactAttribute
{
    public UpstreamFactAttribute(
        string id,
        string title,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException("Upstream IDs must use the XTJS-0001 format.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        UpstreamId = id;
        UpstreamTitle = title;
        DisplayName = $"{id} {title}";
    }

    public string UpstreamId { get; }

    public string UpstreamTitle { get; }

    private static bool IsValidId(string id) =>
        id.Length == 9 &&
        id.StartsWith("XTJS-", StringComparison.Ordinal) &&
        int.TryParse(id.AsSpan(5), out int number) &&
        number is >= 1 and <= 9999;
}
