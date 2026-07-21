using Android.App;
using Android.Runtime;

namespace XtermSharp.Maui.Demo.SSH;

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
