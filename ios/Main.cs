// bibmd on iPhone and iPad. The app and its one scene; the reader is Host's.
using Foundation;
using UIKit;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("ios15.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("maccatalyst15.0")]

namespace BibMd;

public static class Program
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;
}

/// <summary>
/// The scene: its window is the reader's. A document it is asked to open (from the Files
/// app, or shared to it) is opened in place; without one, the last document read, or the
/// sourdough notebook the first time.
/// </summary>
[Register("SceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    private Host? _host;

    [Export("window")]
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene) return;
        _host = new Host(windowScene);
        Window = _host.Window.View.Window;
        if (connectionOptions.UrlContexts?.AnyObject is UIOpenUrlContext context) _host.OpenUrl(context.Url);
        else _host.OpenDefault();
    }

    [Export("scene:openURLContexts:")]
    public void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        if (urlContexts.AnyObject is { } context) _host?.OpenUrl(context.Url);
    }
}
