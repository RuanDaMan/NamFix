using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;

namespace NamFix.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Route the hardware back button through the web app first: close an open notification popup /
    // nav drawer, else navigate SPA history back. Only when the web app reports it did nothing do we
    // fall back to sending the app to the background (instead of the default, which just exits).
    public override void OnBackPressed()
    {
        var webView = FindWebView(Window?.DecorView);
        if (webView is null)
        {
            base.OnBackPressed();
            return;
        }

        webView.EvaluateJavascript(
            "(window.namfix && namfix.handleBack) ? namfix.handleBack() : false",
            new BackResultCallback(handled =>
            {
                if (!handled)
                    RunOnUiThread(() => MoveTaskToBack(true));
            }));
    }

    private static Android.Webkit.WebView? FindWebView(Android.Views.View? view)
    {
        if (view is Android.Webkit.WebView webView)
            return webView;

        if (view is Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                var found = FindWebView(group.GetChildAt(i));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private sealed class BackResultCallback : Java.Lang.Object, IValueCallback
    {
        private readonly Action<bool> _onResult;
        public BackResultCallback(Action<bool> onResult) => _onResult = onResult;

        // EvaluateJavascript returns the result as a JSON string ("true"/"false").
        public void OnReceiveValue(Java.Lang.Object? value) =>
            _onResult(string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
