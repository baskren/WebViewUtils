using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using HtmlAgilityPack;
using Microsoft.Web.WebView2.Core;
using P42.Uno;
using Windows.Storage.Pickers;
using Microsoft.UI.Dispatching;

#if __IOS__
using Foundation;
#endif
namespace WebViewUtils;

public sealed partial class MainPage : Page
{
    WebView2 _webView;
    public static DispatcherQueue MainThreadDispatchQueue { get; private set; }

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
                    .RowDefinitions("*,50")
                    .Children
                    (
                        new TextBlock()
                            .Text("Hello World!")
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center),
                        new WebView2()
                            .Name(out _webView)
                            .DefaultBackgroundColor(Colors.Pink)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .VerticalAlignment(VerticalAlignment.Stretch),
                        new StackPanel()
                            .Grid(row:1)
                            .Orientation(Orientation.Horizontal)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new Button()
                                    .Name(out var htmlPdfButton)
                                    .Content("HTML PDF")
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new Button()
                                    .Name(out var webViewPdfButton)
                                    .Content("WV2 PDF")
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new Button()
                                    .Name(out var htmlPrintButton)
                                    .Content("HTML PRINT")
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new Button()
                                    .Name(out var webViewPrintButton)
                                    .Content("WV2 PRINT")
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            )



                    )
            );

        htmlPdfButton.Click += OnHtmlPdfButtonClick;
        webViewPdfButton.Click += OnWebViewPdfButtonClick;
        htmlPrintButton.Click += OnHtmlPrintButtonClick;
        webViewPrintButton.Click += OnWebViewPrintButtonClick;

        //_webView.Source = new Uri("https://platform.uno");

        Loaded += OnLoaded;

        MainThreadDispatchQueue = DispatcherQueue.GetForCurrentThread();
    }

    private async void OnWebViewPrintButtonClick(object sender, RoutedEventArgs e)
    {
        await _webView.EnsureCoreWebView2Async();
        await _webView.PrintAsync();
    }


    private async void OnHtmlPrintButtonClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var html = await WebViewExtensions.ReadResourceAsTextAsync("WebViewUtils.Resources.Html5TestPage.html");
            await WebViewExtensions.PrintAsync(this, html);
        }
        catch (Exception ex)
        {
            var cd = new ContentDialog
            {
                Title = "Print Error",
                Content = ex.Message,
                PrimaryButtonText = "OK"
            };
            await cd.ShowAsync();
        }
    }

    private async void OnWebViewPdfButtonClick(object sender, RoutedEventArgs e)
    {
        // options is broken in WASM
        var options = new PdfOptions([30, 30, 30, 30],
            Html2canvas: new Html2CanvasOptions(Scale: 2),
            JsPDF: new JsPdfOptions(Unit: PdfUnits.Pt, Format: PdfPageSize.Letter));
        await _webView.SavePdfAsync(options);
    }

    async void OnHtmlPdfButtonClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var html = await WebViewExtensions.ReadResourceAsTextAsync("WebViewUtils.Resources.Html5TestPage.html");
            await WebViewExtensions.SavePdfAsync(this, html);
        }
        catch (Exception ex)
        {
            var cd = new ContentDialog
            {
                Title = "Pdf Error",
                Content = ex.Message,
                PrimaryButtonText = "OK"
            };
            await cd.ShowAsync();
        }
    }


    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _webView.EnsureCoreWebView2Async();


#if BROWSERWASM
        var page = $"/{GetBootstrapBase()}/WebContent/CltInstall.html";
        var msg = $"FramePage: [{page}]";
        Console.WriteLine(msg);
        System.Diagnostics.Debug.WriteLine(msg);
        _webView.CoreWebView2.Navigate(page);
        return;
#endif

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping
        (
            "WebContent",
            "WebContent",
            CoreWebView2HostResourceAccessKind.Allow
        );

        _webView.CoreWebView2.Navigate("http://WebContent/CltInstall.html");
        
        //var html = await WebViewExtensions.ReadResourceAsTextAsync("WebViewUtils.Resources.Html5TestPage.html");
        //_webView.CoreWebView2.NavigateToString(html);
        
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

#if BROWSERWASM

    [JSImport("globalThis.P42_GetPageUrl")]
    public static partial string GetPageUrl();

    [JSImport("globalThis.P42_BootstrapBase")]
    public static partial string GetBootstrapBase();

    [JSImport("globalThis.P42_HtmlPrint")]
    public static partial string HtmlPrint(string html);

#endif

}

