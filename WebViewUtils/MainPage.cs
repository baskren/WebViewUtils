using System.Net.Http;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Web.WebView2.Core;
using P42.Uno.HtmlExtensions;
using Uno.UI.Extensions;
using Uno.UI.RemoteControl.Messaging.IdeChannel;
using Windows.Storage.Pickers;
using Windows.Storage.Pickers.Provider;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                            .VerticalAlignment(VerticalAlignment.Stretch),
                        new StackPanel()
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

    }

    private async void OnWebViewPrintButtonClick(object sender, RoutedEventArgs e)
    {
        await _webView.PrintAsync();
    }

    private async void OnHtmlPrintButtonClick(object sender, RoutedEventArgs e)
    {
        var html = await WebViewExtensions.ReadResourceAsTextAsync("WebViewUtils.Resources.Html5TestPage.html");
        await WebViewExtensions.PrintAsync(this, html);
    }

    private async void OnWebViewPdfButtonClick(object sender, RoutedEventArgs e)
    {
        // options is broken in WASM
        var options = new PdfOptions(new Thickness(20), Unit: PdfUnits.In, Format: PdfPageSize.Letter, Scale: 4);
        await _webView.SavePdfAsync(options);
    }

    async void OnHtmlPdfButtonClick(object sender, RoutedEventArgs e)
    {
        var html = await WebViewExtensions.ReadResourceAsTextAsync("WebViewUtils.Resources.Html5TestPage.html");
        await WebViewExtensions.SavePdfAsync(this, html);
    }



    async void OnButtonClickText(object sender, RoutedEventArgs e)
    {
        //var html = "THIS IS A TEST";
        //HtmlPrint(html);

        //UriPrint("https://platform.uno");

        var tmpWebView = new WebView2 ();
        var tmpContent = this.Content as UIElement;
        this.Content = tmpWebView;

        await tmpWebView.EnsureCoreWebView2Async ();
        //tmpWebView.Source = new Uri("https://platform.uno");

        //tmpWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
        //tmpWebView.NavigationCompleted += OnTmpWebViewNavigationCompleted;

        var html = "THIS IS A <b>TEST</b> <i>STRING</i>.";
        var doc = new HtmlDocument();
        doc.OptionFixNestedTags = true;
        doc.LoadHtml(html);

        if (doc.DocumentNode.SelectSingleNode("//html") is not HtmlNode htmlNode)
        {
            htmlNode = doc.CreateElement("html");
            foreach (var child in doc.DocumentNode.ChildNodes)
                htmlNode.AppendChild(child.Clone());
            doc.DocumentNode.RemoveAllChildren();
            doc.DocumentNode.AppendChild(htmlNode);
        }

        if (htmlNode.SelectSingleNode("head") is not HtmlNode headNode)
        {
            headNode = doc.CreateElement("head");
            htmlNode.PrependChild(headNode);
        }

        if (htmlNode.SelectSingleNode("body") is not HtmlNode bodyNode)
        {
            bodyNode = doc.CreateElement("body");
            // Move non-head children into <body>
            foreach (var child in htmlNode.ChildNodes.ToArray())
            {
                if (child != headNode)
                {
                    htmlNode.RemoveChild(child);
                    bodyNode.AppendChild(child);
                }
            }
            htmlNode.AppendChild(bodyNode);
        }

        // document.addEventListener("DOMContentLoaded", function () {
        //      console.log("DOM is fully loaded and parsed.");
        // });

        /*
        var loadedScriptNode = doc.CreateElement("script");
        //scriptNode.SetAttributeValue("src", "myscript.js");
        loadedScriptNode.InnerHtml = """
            document.addEventListener("DOMContentLoaded", function() {
                console.log("DOM is fully loaded and parsed");
                document.p42_DomIsLoaded = "true";
            });
            window.addEventListener("load", function () {
                console.log("Entire page and all resources are fully loaded.");
                window.p42_PageIsLoaded = "true";
            });
            """;
        headNode.AppendChild(loadedScriptNode);
        */

        var html2pdfScriptNode = doc.CreateElement("script");

        var resources = typeof(MainPage).Assembly.GetManifestResourceNames();
        foreach (var resource in resources)
            System.Diagnostics.Debug.WriteLine($" = {resource}");

        using var html2pdfScriptStream = typeof(MainPage).Assembly.GetManifestResourceStream("WebViewUtils.Resources.html2pdf.bundle.min.js") ?? throw new InvalidOperationException("Resource not found");
        using var reader = new StreamReader(html2pdfScriptStream);
        var html2pdfScript = reader.ReadToEnd();
        //html2pdfScriptNode.InnerHtml = html2pdfScript; // does not work - truncates script 
        html2pdfScriptNode.InnerHtml = "[[[REPLACE ME WITH HTML2PDF SCRIPT]]]";  // setting InnerHtml to html2pdfScript seems to truncate the script.
        //html2pdfScriptNode.SetAttributeValue("src", "https://cdnjs.cloudflare.com/ajax/libs/html2pdf.js/0.10.1/html2pdf.bundle.min.js"); // this WORKS (but not offline)
        headNode.AppendChild(html2pdfScriptNode);

        var makePdfScript = doc.CreateElement("script");
        /*
        makePdfScript.InnerHtml = """
            function p42_makePdf() {
                html2pdf()
                    .from(document.documentElement)
                    .set({
                        margin: 10,
                        filename: 'document.pdf',
                        html2canvas: { scale: 2 },
                        jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
                    })
                    .save();
            }
            """;
        */


        using var makePdfScriptStream = typeof(MainPage).Assembly.GetManifestResourceStream("WebViewUtils.Resources.p42_makePdf.js") ?? throw new InvalidOperationException("Resource not found");
        using var reader1 = new StreamReader(makePdfScriptStream);
        makePdfScript.InnerHtml = reader1.ReadToEnd();
        headNode.AppendChild(makePdfScript);

        var buttonNode = doc.CreateElement("button");
        buttonNode.Id = "P42_DownloadPdfButton";
        buttonNode.InnerHtml = "DOWNLOAD PDF";
        bodyNode.PrependChild(buttonNode);
        var buttonClickScript = doc.CreateElement("script");
        buttonClickScript.InnerHtml = """
            document.getElementById("P42_DownloadPdfButton"),addEventListener("click", p42_makePdf);
            """;
        bodyNode.AppendChild(buttonClickScript);

        var fixedHtml = doc.DocumentNode.OuterHtml;
        fixedHtml = fixedHtml.Replace("[[[REPLACE ME WITH HTML2PDF SCRIPT]]]", html2pdfScript);
        tmpWebView.NavigateToString(fixedHtml);
        /*
        tmpWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
        tmpWebView.CoreWebView2.WebMessageReceived += OnCoreWebView2_WebMessageReceived;

        

        var js = "window.chrome.webview.postMessage('Hello from JS Land');";
        await tmpWebView.ExecuteScriptAsync(js);
        */

        /*
        await Task.Delay(5000);

        string js = @"
    window.myAsyncResult = null;
    async function startAsync() {
        const result = await new Promise((resolve) => { 
            setTimeout(() => { 
                console.log('TIME OUT');
                //window.myAsyncResult = 'FINISHED';
                resolve('Finished!'); 
                //document.documentElement.outerHTML = 'DONE!!!';
            }, 2000);
        });
        window.myAsyncResult = result;
    }
    startAsync().then(() => console.log('Async function complete'));
    console.log('DONE');
";

        System.Diagnostics.Debug.WriteLine("EXECUTING");
        Console.WriteLine("EXECUTING");
        await tmpWebView.CoreWebView2.ExecuteScriptAsync(js);
        System.Diagnostics.Debug.WriteLine("EXECUTED");
        Console.WriteLine("EXECUTED");
        */


        /*
        string? isLoaded = null;
        while (string.IsNullOrWhiteSpace(isLoaded) || !isLoaded.Contains("true"))
        {
            isLoaded = await tmpWebView.CoreWebView2.ExecuteScriptAsync("window.p42_PageIsLoaded");
            await Task.Delay(500);
        }
        */

        /*
        string? readyState = "";
        while (readyState != "complete")
        {
            readyState = await tmpWebView.CoreWebView2.ExecuteScriptAsync("document.readyState");
            readyState = readyState?.Trim('"');
            System.Diagnostics.Debug.WriteLine( $"readyState:[{readyState}]");
            Console.WriteLine($"readyState:[{readyState}]");
            await Task.Delay(500);
        }
        */

        await tmpWebView.WaitForDocumentLoadedAsync();
        

        string result = "";
        string error = "";
        await tmpWebView.ExecuteScriptAsync("p42_makePdf()");
        while (string.IsNullOrWhiteSpace(result) && string.IsNullOrWhiteSpace(error))
        {
            error = await tmpWebView.CoreWebView2.ExecuteScriptAsync("window.p42_makeP42_error");
            System.Diagnostics.Debug.WriteLine($"error:[{error}]");
            error = error.Trim('"');
            if (error == "null")
                error = "";
            if (!string.IsNullOrEmpty(error))
            {
                this.Content = tmpContent;
                throw new Exception(error);
            }

            result = await tmpWebView.CoreWebView2.ExecuteScriptAsync("window.p42_makePdf_result");
            System.Diagnostics.Debug.WriteLine($"result:[{result}]");
            result = result.Trim('"');
            if (result == "null")
                result = "";

            await Task.Delay(500);
        }

        var bytes = Convert.FromBase64String(result);
        //System.Diagnostics.Debug.WriteLine(result);  // Outputs: Finished!
        //Console.WriteLine(result);  // Outputs: Finished!

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.Downloads;
        picker.SuggestedFileName = "document.pdf";
        picker.FileTypeChoices.Add("PDF", new List<string>() { ".pdf" });


#if WINDOWS
        // Get the current window's HWND by passing a Window object
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)App.Current).MainWindow);
        // Associate the HWND with the file picker
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
#endif

        StorageFile saveFile = await picker.PickSaveFileAsync();
        if (saveFile != null)
        {
            CachedFileManager.DeferUpdates(saveFile);

            // Save file was picked, you can now write in it
            await FileIO.WriteBytesAsync(saveFile, bytes);

            await CachedFileManager.CompleteUpdatesAsync(saveFile);

        }

        //await tmpWebView.ExecuteScriptAsync("print();");

        this.Content = tmpContent;
        

    }

    private void OnCoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (args.TryGetWebMessageAsString() is  string text && !string.IsNullOrWhiteSpace(text)) 
            _webView.CoreWebView2.NavigateToString(text);
    }
    /*
    static int iterations;
    async void OnTmpWebViewNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        var iteration = iterations++;

        var html = await sender.ExecuteScriptAsync("document.documentElement.outerHTML;");
        var size = await sender.WebViewContentSizeAsync();

        Console.WriteLine($"OnTmpWebViewNavigationCompleted[{iteration}] ENTER : [{sender}] [{sender.Source}] [{args.NavigationId}] [{args.HttpStatusCode}] [{args.IsSuccess}] [{args.WebErrorStatus}]  :  [{size}]  [{html}]");

        if (size.Width < 1 || size.Height < 1)
            return;


        await tmpWebView.ExecuteScriptAsync("window.print();");
        Console.WriteLine($"OnTmpWebViewNavigationCompleted[{iteration}] A");
        tmpWebView.NavigationCompleted -= OnTmpWebViewNavigationCompleted;
        Console.WriteLine($"OnTmpWebViewNavigationCompleted[{iteration}] B");
        this.Content = (UIElement)tmpContent;
        Console.WriteLine($"OnTmpWebViewNavigationCompleted[{iteration}] EXIT");
    }
    */

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _webView.EnsureCoreWebView2Async();


#if BROWSERWASM
        var page = $"/{GetBootstrapBase()}/WebContent/CltInstall.html";
        //var pageUrl = $"{GetPageUrl()}{page}";
        Console.WriteLine($"FramePage: [{page}]");
        System.Diagnostics.Debug.WriteLine($"FramePage: [{page}]");
        _webView.CoreWebView2.Navigate(page);
        //_webView.Source = new Uri( pageUrl );
        return;
#endif

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping
        (
            "WebContent",
            "WebContent",
            CoreWebView2HostResourceAccessKind.Allow
        );

        _webView.CoreWebView2.Navigate("http://WebContent/CltInstall.html");
        
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

