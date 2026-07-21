using Android.App;
using Android.Content.PM;

namespace XtermSharp.Maui.Demo.SSH;

[Activity(
    MainLauncher = true,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity;
