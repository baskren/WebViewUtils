using Android.Content;
using Android.Runtime;
using Android.Views;
using System.Reflection;
using Android.Print;

namespace P42.Uno;

internal static class AndroidWebViewExtensions
{
    private static MethodInfo? _computeHorizontalScrollRangeMethodInfo;
    private static MethodInfo? ComputeHorizontalScrollRangeMethodInfo => _computeHorizontalScrollRangeMethodInfo ??= typeof(Android.Webkit.WebView).GetMethod("ComputeHorizontalScrollRange", BindingFlags.NonPublic | BindingFlags.Instance);


    public static int ContentWidth(this Android.Webkit.WebView webView)
    {
        if (ComputeHorizontalScrollRangeMethodInfo is not { } method)
            return 0;
        
        return method.Invoke(webView, []) is int width 
            ? width 
            : 0;
    }

    private static MethodInfo? _computeVerticalScrollRangeMethodInfo;
    private static MethodInfo? ComputeVerticalScrollRangeMethodInfo => _computeVerticalScrollRangeMethodInfo ??= typeof(Android.Webkit.WebView).GetMethod("ComputeVerticalScrollRange", BindingFlags.NonPublic | BindingFlags.Instance);
    
    public static int ContentHeight(this Android.Webkit.WebView webView)
    {
        if (ComputeVerticalScrollRangeMethodInfo is not { } method)
            return 0;

        return method.Invoke(webView, []) is int height
            ? (int)(height / Display.Scale) + webView.MeasuredHeight
            : webView.MeasuredHeight;
    }

    public static async Task<Java.Lang.Object?> EvaluateJavaScriptAsync(this Android.Webkit.WebView webView, string script)
    {
        using JavaScriptEvaluator evaluator = new (webView, script);
        return await evaluator.TaskCompletionSource.Task;
    }

    private static Android.App.Activity? Activity => ContextHelper.Current as Android.App.Activity;

    
    public static async Task PrintAsync(this Android.Webkit.WebView droidWebView, string jobName = "", CancellationToken cancellationToken = default)
    {
        droidWebView.Settings.JavaScriptEnabled = true;
        droidWebView.Settings.DomStorageEnabled = true;
        droidWebView.SetLayerType(LayerType.Software, null);

        // Only valid for API 19+
        if (string.IsNullOrWhiteSpace(jobName))
        {
            var javaResult = await droidWebView.EvaluateJavaScriptAsync("document.title");
            jobName = javaResult?.ToString() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(jobName))
            jobName = AppInfo.Name;

        PrintManager?.Print(jobName, droidWebView.CreatePrintDocumentAdapter(jobName), null);

        await Task.CompletedTask;
    }

    private static PrintManager? PrintManager => Activity?.GetSystemService(Context.PrintService) as PrintManager;
    
    private static class AppInfo
    {
        public static string Name
        {
            get
            {
                if (Activity?.ApplicationInfo is not { } appInfo 
                    || Activity.PackageManager is not { } packageManager)
                    return string.Empty;
                
                return appInfo.LoadLabel(packageManager);
            }
        }
    }

}

internal class JavaScriptEvaluator : Java.Lang.Object, Android.Webkit.IValueCallback
{
    public readonly TaskCompletionSource<Java.Lang.Object?> TaskCompletionSource = new();

    public JavaScriptEvaluator(Android.Webkit.WebView webView, string script)
        => webView.EvaluateJavascript(script, this);
    
    public void OnReceiveValue(Java.Lang.Object? value)
        => TaskCompletionSource.SetResult(value);

}

internal static class Display
{
    private static Android.App.Activity? Activity => ContextHelper.Current as Android.App.Activity;

    public static double Scale
    {
        get
        {
            if (Activity is null)
                return 1.0;
            
            if (Android.OS.Build.VERSION.SdkInt >= (Android.OS.BuildVersionCodes)31)
                return Activity.Resources?.DisplayMetrics?.Density ?? 1.0;
            
            using Android.Util.DisplayMetrics displayMetrics = new ();
            using var service = ContextHelper.Current.GetSystemService(Context.WindowService);
            using var windowManager = service?.JavaCast<IWindowManager>();
            if (windowManager?.DefaultDisplay is not {} display)
                return 1.0;
            
#pragma warning disable CA1422 // Validate platform compatibility
            display.GetRealMetrics(displayMetrics);
#pragma warning restore CA1422 // Validate platform compatibility
            return displayMetrics.Density;

        }
    }
}
