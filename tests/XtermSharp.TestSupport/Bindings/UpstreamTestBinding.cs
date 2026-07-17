using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XtermSharp.TestSupport.Bindings;

public sealed record UpstreamTestBinding(
    string AssemblyName,
    string TypeName,
    string MethodName,
    string Id,
    string Title)
{
    public string CsharpTest => $"{AssemblyName}:{TypeName}.{MethodName}";
}
