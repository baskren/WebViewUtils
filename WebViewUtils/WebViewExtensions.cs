using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Uno.UI.Extensions;
using WebViewUtils;
using Windows.Storage.Pickers;
using Microsoft.Web.WebView2.Core;

namespace P42.Uno;

public static class WebViewExtensions
{

    /// <summary>
    /// Get content of a WebView as HTML
    /// </summary>
    /// <param name="webView"></param>
    /// <returns></returns>
    public static async Task<string> GetHtmlAsync(this WebView2 webView)
        => await webView.ExecuteScriptAsync("document.documentElement.outerHTML;") ?? string.Empty;

    public static async Task WaitForDocumentLoadedAsync(this WebView2 webView2, CancellationToken token = default)
        => await webView2.WaitForVariableValue("document.readyState", "complete", token);

    private static async Task<object?> GetNativeWebViewWrapper(this WebView2 webView2)
    {
        if (typeof(CoreWebView2).GetField("_nativeWebView", BindingFlags.Instance | BindingFlags.NonPublic) is not {} nativeWebViewField)
            throw new Exception("Unable to obtain _nativeWebView field information");
        var nativeWebView = nativeWebViewField.GetValue(webView2.CoreWebView2);
        return nativeWebView ?? throw new Exception("Unable to obtain native webview");
    }
    
    public static async Task PrintAsync(this WebView2 webView2, CancellationToken token = default)
    {
        try
        {
            await webView2.WaitForDocumentLoadedAsync(token);
#if __ANDROID__
            var nativeWebViewWrapper = await webView2.GetNativeWebViewWrapper();
            var type = nativeWebViewWrapper.GetType();
            if (type.GetProperty
            (
                "WebView", 
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )
            ?.GetValue(nativeWebViewWrapper) is not Android.Webkit.WebView droidWebView)
            throw new Exception("Unable to obtain native webview");

            await droidWebView.PrintAsync(cancellationToken: token);
#elif __IOS__
            var nativeWebViewWrapper = await webView2.GetNativeWebViewWrapper();
            if (nativeWebViewWrapper is not WebKit.WKWebView wkWebView)
                throw new Exception("Unable to obtain native webview");

            var result = await wkWebView.PrintAsync();
#else
            await webView2.ExecuteScriptAsync("window.print();").AsTask(token);
#endif
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

    public static async Task PrintAsync(UIElement element, string html, CancellationToken token = default)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");
        
        var processor = new AuxiliaryWebViewAsyncProcessor<bool>(element.XamlRoot, html, token);
        await processor.ProcessAsync(PrintFunction);
        return;

        static async Task<bool> PrintFunction(WebView2 webView, CancellationToken localToken)
        {
            await webView.PrintAsync(localToken);
            return true;
        }
    }
    
    public static async Task SavePdfAsync(UIElement element, string html, PdfOptions? options = null, CancellationToken token = default)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        var pdfTask = GeneratePdfAsync(element, html, options, token);
        await SavePdfAsync(element, pdfTask);
    }

    public static async Task SavePdfAsync(this WebView2 webView, PdfOptions? options = null, CancellationToken token = default)
    {
        var pdfTask = webView.GeneratePdfAsync(options, token);
        await SavePdfAsync(webView, pdfTask);
    }

    public static async Task SavePdfAsync(UIElement element, Task<(byte[]? pdf, string error)> pdfTask)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        var fileTask = RequestStorageFileAsync( element.XamlRoot, "PDF", "pdf");
        await Task.WhenAll(fileTask, pdfTask);

        if (fileTask.Result is not { } saveFile)
            return;

        if (!string.IsNullOrWhiteSpace(pdfTask.Result.error)
            || pdfTask.Result.pdf is null 
            || pdfTask.Result.pdf.Length == 0)
        {
            ContentDialog cd = new ()
            {
                XamlRoot = element.XamlRoot,
                Title = "PDF Generation Error",
                Content = string.IsNullOrWhiteSpace(pdfTask.Result.error)
                    ? "Unknown failure"
                    : pdfTask.Result.error,
                PrimaryButtonText = "OK"
            };
            await cd.ShowAsync();
            return;
        }

        CachedFileManager.DeferUpdates(saveFile);
        await FileIO.WriteBytesAsync(saveFile, pdfTask.Result.pdf);
        await CachedFileManager.CompleteUpdatesAsync(saveFile);
    }
    
    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this UIElement element, string html, PdfOptions? options = null, CancellationToken token = default)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        var processor = new AuxiliaryWebViewAsyncProcessor<(byte[]? pdf, string error)>(element.XamlRoot, html, token);
        return await processor.ProcessAsync(MakePdfFunction);

        async Task<(byte[]?, string)> MakePdfFunction(WebView2 webView, CancellationToken localToken)
        {
            var result = await webView.GeneratePdfAsync(options, localToken);
            return result;
        }
    }

    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this WebView2 webView2, PdfOptions? options = null, CancellationToken token = default)
    {
        await webView2.WaitForDocumentLoadedAsync(token);
        await webView2.AssurePdfScriptsAsync(token);

        var result = string.Empty;
        var error = string.Empty;


        var jsonOptions = options == null
            ? ""
            : JsonSerializer.Serialize(options, PdfOptionsSourceGenerationContext.Default.PdfOptions).Trim('"');

        await webView2.ExecuteScriptAsync($"p42_makePdf({jsonOptions})").AsTask(token);
        while (string.IsNullOrWhiteSpace(result) && string.IsNullOrWhiteSpace(error))
        {
            error = await webView2.CoreWebView2.ExecuteScriptAsync("window.p42_makeP42_error").AsTask(token) ?? "";
            //Debug.WriteLine($"error:[{error}]");
            //Console.WriteLine($"error:[{error}]");
            error = error.Trim('"').Trim('"');
            if (error == "null")
                error = string.Empty;
            if (!string.IsNullOrEmpty(error))
                return (null, error);

            result = await webView2.CoreWebView2.ExecuteScriptAsync("window.p42_makePdf_result").AsTask(token) ?? "";
            result = result.Trim('"').Trim('"');
            //Console.WriteLine($"result: [{result}]");
            if (result == "null")
                result = string.Empty;
            else if (!string.IsNullOrEmpty(result))
                Debug.WriteLine("bingo");

            await Task.Delay(500, token);
        }

        var bytes = Convert.FromBase64String(result);
        return (bytes, "");
    }

    public static async Task<StorageFile?> RequestStorageFileAsync(XamlRoot xamlRoot, string type, params List<string> suffixes)
    {
        #if __IOS__
        var picker = new CustomFileSavePicker(xamlRoot)
        #else
        var picker = new FileSavePicker
        #endif
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };

        if (suffixes.Count > 0)
        {
            for (var i = suffixes.Count - 1; i >= 0; i--)
            {
                var suffix = suffixes[i].TrimStart('.');

                if (string.IsNullOrWhiteSpace(suffix))
                    suffixes.RemoveAt(i);
                else
                    suffixes[i] = $".{suffix}";
            }

            if (suffixes.FirstOrDefault() is { } primarySuffix)
                picker.SuggestedFileName = $"document{primarySuffix}";
        }

        picker.FileTypeChoices.Add(type, suffixes);


#if WINDOWS
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)App.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
#endif

        
        return await picker.PickSaveFileAsync();
        
    }

    private static async Task WaitForVariableValue(this WebView2 webView2, string variable, string value, CancellationToken token = default)
    {
        await webView2.EnsureCoreWebView2Async().AsTask(token);

        var result = string.Empty;
        while (result != value)
        {
            result = await webView2.CoreWebView2.ExecuteScriptAsync(variable).AsTask(token);
            result = result?.Trim('"');

            await Task.Delay(500, token);
        }
        
    }

    private static async Task AssurePdfScriptsAsync(this WebView2 webView2, CancellationToken token = default)
    {
        await webView2.AssureResourceFunctionLoadedAsync("html2pdf", "WebViewUtils.Resources.html2pdf.bundle.min.js", token);
        await webView2.AssureResourceFunctionLoadedAsync("p42_makePdf", "WebViewUtils.Resources.p42_makePdf.js", token);
    }

    private static async Task AssureResourceFunctionLoadedAsync(this WebView2 webView2, string functionName, string resourceId, CancellationToken token = default)
    {
        if (await webView2.IsFunctionLoadedAsync(functionName, token))
            return;

        var script = await ReadResourceAsTextAsync(resourceId).WaitAsync(token);
        await webView2.ExecuteScriptAsync(script).AsTask(token);

        if (await webView2.IsFunctionLoadedAsync(functionName, token))
            return;

        throw new Exception($"Failed to load JavaScript function [{functionName}]");
    }

    public static async Task<bool> IsFunctionLoadedAsync(this WebView2 webView2, string functionName, CancellationToken token = default)
    {
        var type = await webView2.ExecuteScriptAsync($"typeof {functionName};").AsTask(token);
        type = type?.Trim('"').Trim('"');
        return type?.Contains("function") ?? false;
    }

    public static async Task<string> ReadResourceAsTextAsync(string resourceId)
    {
        await using var stream = typeof(WebViewExtensions).Assembly.GetManifestResourceStream(resourceId) ?? throw new InvalidOperationException("Resource not found");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
    
    private record TryResult<T>(bool IsSuccess, T? Value = default);

    private static async Task<TryResult<T>> TryExecuteScriptAsync<T>(this WebView2 webView2, string script) where T : IParsable<T>, ISpanParsable<T>, INumber<T>
    {
        try
        {
            var result = await webView2.ExecuteScriptAsync(script);
            if (T.TryParse(result, null, out var v))
                return new TryResult<T>(true, v);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebViewExtensions.TryExecuteIntScriptAsync {ex.GetType()} : {ex.Message} \n{ex.StackTrace} ");
        }

        return await Task.FromResult(new TryResult<T>(false));
    }

    /*
    private static async Task<TryResult<int>> TryExecuteIntScriptAsync(this WebView2 webView2, string script)
    {
        try
        {
            var result = await webView2.ExecuteScriptAsync(script);
            if (int.TryParse(result, out var v))
                return new TryResult<int>(true, v);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebViewExtensions.TryExecuteIntScriptAsync {ex.GetType()} : {ex.Message} \n{ex.StackTrace} ");
        }

        return await Task.FromResult(new TryResult<int>(false));
    }

    private static async Task<TryResult<double>> TryExecuteDoubleScriptAsync(this WebView2 webView2, string script)
    {
//#if __WASM__ || !NET7_0_OR_GREATER
        try
        {
            var result = await webView2.ExecuteScriptAsync(script);
            if (double.TryParse(result, out var v))
                return new TryResult<double>(true, v);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebViewExtensions.TryExecuteDoubleScriptAsync {ex.GetType()} : {ex.Message} \n{ex.StackTrace} ");
        }
//#endif
        return await Task.FromResult(new TryResult<double>(false));
    }
    */

    private static async Task<double> TryUpdateIfLarger(this WebView2 webView2, string script, double source)
    {
        if (await webView2.TryExecuteScriptAsync<double>(script) is { IsSuccess: true } r1 && r1.Value > source)
            return r1.Value;

        return source;
    }

    /// <summary>
    /// Get the size of a WebView's current content
    /// </summary>
    /// <param name="webView"></param>
    /// <param name="depth"></param>
    /// <param name="callerName"></param>
    /// <returns></returns>
    public static async Task<Windows.Foundation.Size> WebViewContentSizeAsync(this WebView2 webView, int depth = 0, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        ArgumentNullException.ThrowIfNull(webView);

        double contentWidth = -1;
        double contentHeight = -1;

        switch (depth)
        {
            case > 50:
                return new Windows.Foundation.Size(contentWidth, contentHeight);
            case > 0:
                await Task.Delay(100);
                break;
        }

        try
        {
            contentWidth = await webView.TryUpdateIfLarger("document.documentElement.scrollWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.documentElement.scrollHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.documentElement.offsetWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.documentElement.offsetHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.documentElement.getBoundingClientRect().width", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.documentElement.getBoundingClientRect().height", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.documentElement.clientWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.documentElement.clientHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.documentElement.innerWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.documentElement.innerHeight", contentHeight);



            contentWidth = await webView.TryUpdateIfLarger("self.scrollWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("self.scrollHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("self.offsetWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("self.offsetHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("self.getBoundingClientRect().width", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("self.getBoundingClientRect().height", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("self.clientWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("self.clientHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("self.innerWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("self.innerHeight", contentHeight);



            contentWidth = await webView.TryUpdateIfLarger("document.body.scrollWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.body.scrollHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.body.offsetWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.body.offsetHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.body.getBoundingClientRect().width", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.body.getBoundingClientRect().height", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.body.clientWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.body.clientHeight", contentHeight);

            contentWidth = await webView.TryUpdateIfLarger("document.body.innerWidth", contentWidth);
            contentHeight = await webView.TryUpdateIfLarger("document.body.innerHeight", contentHeight);
            
        }
        catch (Exception e)
        {
            var message = $"WebViewExtensions.WebViewContentSizeAsync FAIL: {e.Message}";
            Debug.WriteLine(message);
            Console.WriteLine(message);
            return await WebViewContentSizeAsync(webView, depth + 1, callerName);
        }
        return new Windows.Foundation.Size(contentWidth, contentHeight);
    }
    
}


internal abstract record BaseAuxiliaryWebView
{
    public XamlRoot XamlRoot { get;  }

    protected CancellationTokenSource CancellationTokenSource { get;}

    protected WebView2 WebView2 { get;  }

    protected ContentDialog ContentDialog { get;  }

    public bool HideContent { get; set; }

    protected string Html { get;  }

    protected ProgressRing ProgressRing { get; }

    protected CancellationToken ParentCancellationToken { get; }
    
    protected CancellationTokenSource UiCancellationTokenSource { get; }

    protected BaseAuxiliaryWebView(XamlRoot xamlRoot, string html, CancellationToken cancellationTokenSource = default)
    {
        XamlRoot = xamlRoot;
        Html = html;
        UiCancellationTokenSource = new ();
        ParentCancellationToken = cancellationTokenSource;
        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(UiCancellationTokenSource.Token, ParentCancellationToken);

        WebView2 = new ()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ContentDialog = new()
        {            
            XamlRoot = xamlRoot,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0),
            Padding = new Thickness(0)
        };

#if WINDOWS
        if (!HideContent)
        {
            ContentDialog.Resources["ContentDialogPadding"] = new Thickness(4);
            ContentDialog.Resources["ContentDialogMaxWidth"] = XamlRoot.Size.Width * 0.75;
            ContentDialog.Resources["ContentDialogMaxHeight"] = XamlRoot.Size.Height * 0.75;
            ContentDialog.Resources["ContentDialogMinWidth"] = XamlRoot.Size.Width * 0.75;
            ContentDialog.Resources["ContentDialogMinHeight"] = XamlRoot.Size.Height * 0.75;
            ContentDialog.Resources["HorizontalContentAlignment"] = HorizontalAlignment.Stretch;
            ContentDialog.Resources["VerticalContentAlignment"] = VerticalAlignment.Stretch;
        }
#endif
        ProgressRing = new ProgressRing();
        var grid = new Grid();
        if (HideContent)
        {
            grid.Children
            (
                WebView2,

                new Rectangle()
                    .Name(out var rectangle)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .VerticalAlignment(VerticalAlignment.Stretch)
                    .Fill((Brush)Application.Current.Resources["ContentDialogBackground"]),


                ProgressRing,

                new Button()
                    .Margin(5)
                    .Name(out var cancelButton)
                    .Content("CANCEL")
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Bottom)

            );
            cancelButton.Click += (_, _) => CancellationTokenSource.Cancel(true);
        }
        else
            grid.Children(
                WebView2,
                ProgressRing
                );
        ContentDialog.Content = grid;
    }

    protected async Task ShowAsync()
    {
        ProgressRing.IsActive = true;
        await ContentDialog.ShowAsync();
    }


}

internal record AuxiliaryWebViewAsyncProcessor<T> : BaseAuxiliaryWebView
{
    private Func<WebView2, CancellationToken, Task<T>>? _function;

    public TaskCompletionSource<T> TaskCompletionSource { get; }

    public AuxiliaryWebViewAsyncProcessor(XamlRoot xamlRoot, string html, CancellationToken cancellationToken = default) : base(xamlRoot, html, cancellationToken)
    {
        TaskCompletionSource = new TaskCompletionSource<T>();
    }

    public async Task<T> ProcessAsync(Func<WebView2, CancellationToken, Task<T>> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _function = function;
        WebView2.Loaded += OnLoaded;
        //ContentDialog.Loaded += OnContentDialogLoaded;
        await ShowAsync();
        return await TaskCompletionSource.Task;
    }

    public async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_function is null)
                throw new ArgumentNullException(nameof(_function));
            await WebView2.EnsureCoreWebView2Async().AsTask(CancellationTokenSource.Token);

            WebView2.CoreWebView2.NavigateToString(Html);
            await WebView2.WaitForDocumentLoadedAsync();
            if (!HideContent)
                ProgressRing.Visibility = Visibility.Collapsed;

            var result = await _function(WebView2, CancellationTokenSource.Token);
            TaskCompletionSource.SetResult(result);
        }
        catch (Exception ex)
        {
            TaskCompletionSource.TrySetException(ex);
        }
        finally
        {
            ContentDialog.Hide();
            ContentDialog.Loaded -= OnLoaded;
        }
    }


}

