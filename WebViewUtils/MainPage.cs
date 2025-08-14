using Microsoft.Web.WebView2.Core;

#if __IOS__
using Foundation;
#endif
namespace WebViewUtils;

public sealed partial class MainPage : Page
{
    WebView2 _webView;
    
    public MainPage()
    {
        this
            .Background(ThemeResource.Get<Brush>("ApplicationPageBackgroundThemeBrush"))
            .VerticalAlignment(VerticalAlignment.Stretch)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Content(
                new Grid()
                    .VerticalAlignment(VerticalAlignment.Stretch)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .Children
                    (
                        new TextBlock()
                            .Text("Hello World!")
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center),
                        new WebView2()
                            .Assign(out _webView)
                            .DefaultBackgroundColor(Colors.Pink)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .VerticalAlignment(VerticalAlignment.Stretch)
                            
                    )
            );

        
        //_webView.Source = new Uri("https://platform.uno");

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _webView.EnsureCoreWebView2Async();
        //_webView.NavigateToString("<b>THIS IS THE <i>HTML</i> CONTENT</b>");
        //_webView.Source = new Uri($"https://platform.uno");

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping
        (
            "UnoNativeAssets",
            "WebContent",
            CoreWebView2HostResourceAccessKind.Allow
        );
        _webView.CoreWebView2.Navigate("http://UnoNativeAssets/CltInstall.html");
        
        #if __IOS__
        
        var bundlePath = NSBundle.MainBundle.BundlePath;
        var files = NSFileManager.DefaultManager.GetDirectoryContentRecursive(bundlePath, out var error);
        if (error != null)
        {
            System.Diagnostics.Debug.WriteLine(error);
            Console.WriteLine(error);
        }        
        else
        {
            foreach (var file in files)
            {
                System.Diagnostics.Debug.WriteLine($" - {file}");
            }
        }
        #endif
    }
}
