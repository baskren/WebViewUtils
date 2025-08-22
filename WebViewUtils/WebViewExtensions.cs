using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Uno.UI.Extensions;
using Uno.UI.RemoteControl.Messaging.IdeChannel;
using WebViewUtils;
using Windows.Storage.Pickers;
using Windows.UI.WebUI;




namespace P42.Uno.HtmlExtensions;

public static partial class WebViewExtensions
{

    /// <summary>
    /// Get content of a WebView as HTML
    /// </summary>
    /// <param name="webView"></param>
    /// <returns></returns>
    public static async Task<string> GetHtmlAsync(this Microsoft.UI.Xaml.Controls.WebView2 webView)
    {
        var html = await webView.ExecuteScriptAsync("document.documentElement.outerHTML;");
        return html;
    }


    struct TryResult<T>
    {
        public bool IsSuccess { get; set; }

        public T? Value { get; set; }

        public TryResult(bool success, T? value = default)
        {
            IsSuccess = success;
            Value = value;
        }
    }

    public static async Task WaitForDocumentLoadedAsync(this Microsoft.UI.Xaml.Controls.WebView2 webView2, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;
        await webView2.WaitForVariableValue("document.readyState", "complete", token);
    }

    public static async Task PrintAsync(UIElement element, string html, CancellationTokenSource? cts = null)
    {
        if (element?.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        static async Task function(WebView2 webView, CancellationToken token)
        {
            await webView.PrintAsync(token);
        }

        var processor = new AuxiliaryWebViewAsyncProcessor(element.XamlRoot, html, cts);
        await processor.ProcessAsync(function);
    }

    public static async Task SavePdfAsync(UIElement element, string html, PdfOptions? options = null, CancellationTokenSource? cts = null)
    {
        if (element?.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        var pdfTask = WebViewExtensions.GeneratePdfAsync(element, html);
        await SavePdfAsync(element, pdfTask, options, cts);
    }

    public static async Task SavePdfAsync(this WebView2 webView, PdfOptions? options = null, CancellationTokenSource? cts = null)
    {
        var pdfTask = webView.GeneratePdfAsync(options);
        await SavePdfAsync(webView, pdfTask, options, cts);
    }

    public static async Task SavePdfAsync(UIElement element, Task<(byte[]? pdf, string error)> pdfTask, PdfOptions? options = null, CancellationTokenSource? cts = null)
    {
        if (element?.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        var fileTask = RequestStorageFileAsync("document", "PDF", "pdf");
        await Task.WhenAll(fileTask, pdfTask);

        if (fileTask.Result is not StorageFile saveFile)
            return;

        if (!string.IsNullOrWhiteSpace(pdfTask.Result.error))
        {
            var cd = new ContentDialog()
            {
                XamlRoot = element.XamlRoot,
                Title = "PDF Generation Error",
                Content = pdfTask.Result.error,
                PrimaryButtonText = "OK",
            };
            await cd.ShowAsync();
            return;
        }

        if (pdfTask.Result.pdf is null || pdfTask.Result.pdf.Length == 0)
        {
            var cd = new ContentDialog()
            {
                XamlRoot = element.XamlRoot,
                Title = "PDF Generation Error",
                Content = "Unknown failure",
                PrimaryButtonText = "OK",
            };
            await cd.ShowAsync();
            return;
        }

        CachedFileManager.DeferUpdates(saveFile);

        // Save file was picked, you can now write in it
        await FileIO.WriteBytesAsync(saveFile, pdfTask.Result.pdf);

        await CachedFileManager.CompleteUpdatesAsync(saveFile);

    }


    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this UIElement element, string html, PdfOptions? options = null, CancellationTokenSource? cts = null)
    {
        if (element?.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        async Task<(byte[]?, string)> function(WebView2 webView, CancellationToken token)
        {
            var result = await webView.GeneratePdfAsync(options, token);
            return result;
        }

        var processor = new AuxiliaryWebViewAsyncProcessor<(byte[]? pdf, string error)>(element.XamlRoot, html, cts);
        return await processor.ProcessAsync(function);
    }

    public static async Task PrintAsync(this WebView2 webView2, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;
        try
        {
            await webView2.WaitForDocumentLoadedAsync();
            var result = await webView2.ExecuteScriptAsync("print();").AsTask(token);
        }
        catch (Exception ex)
        {
            var cd = new ContentDialog
            {
                Title = "Print Error",
                Content = ex.Message,
                PrimaryButtonText = "OK",
            };
            await cd.ShowAsync();
        }
    }

    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this WebView2 webView2, PdfOptions? options = null, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        await webView2.WaitForDocumentLoadedAsync(token);
        await webView2.AssurePdfScriptsAsync(token);

        string result = "";
        string error = "";


        var jsonOptions = options == null
            ? ""
            : JsonSerializer.Serialize(options, PdfOptionsSourceGenerationContext.Default.PdfOptions).Trim('"');

        await webView2.ExecuteScriptAsync($"p42_makePdf({jsonOptions})").AsTask(token);
        while (string.IsNullOrWhiteSpace(result) && string.IsNullOrWhiteSpace(error))
        {
            error = await webView2.CoreWebView2.ExecuteScriptAsync("window.p42_makeP42_error").AsTask(token) ?? "";
            System.Diagnostics.Debug.WriteLine($"error:[{error}]");
            Console.WriteLine($"error:[{error}]");
            error = error.Trim('"').Trim('"');
            if (error == "null")
                error = string.Empty;
            if (!string.IsNullOrEmpty(error))
                return (null, error);

            result = await webView2.CoreWebView2.ExecuteScriptAsync("window.p42_makePdf_result").AsTask(token) ?? "";
            result = result.Trim('"').Trim('"');
            Console.WriteLine($"result: [{result}]");
            if (result == "null")
                result = string.Empty;
            else if (!string.IsNullOrEmpty(result))
                System.Diagnostics.Debug.WriteLine("bingo");

            await Task.Delay(500);
        }

        var bytes = Convert.FromBase64String(result);
        return (bytes, "");
    }

    public static async Task<StorageFile> RequestStorageFileAsync(string suggestedName, string type, params List<string> suffixes)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
        };

        if (suffixes.Count > 0)
        {
            for (int i = suffixes.Count - 1; i >= 0; i--)
            {
                var suffix = suffixes[i].TrimStart('.');

                if (string.IsNullOrWhiteSpace(suffix))
                    suffixes.RemoveAt(i);
                else
                    suffixes[i] = $".{suffix}";
            }

            if (suffixes.FirstOrDefault() is string primarySuffix)
                picker.SuggestedFileName = suggestedName;
        }

        picker.FileTypeChoices.Add(type, suffixes);


#if WINDOWS
        // Get the current window's HWND by passing a Window object
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)App.Current).MainWindow);
        // Associate the HWND with the file picker
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
#elif BROWSERWASM
    if (!Directory.Exists("/cache"))
        Directory.CreateDirectory("/cache");
#endif

        return await picker.PickSaveFileAsync();
    }

    static async Task WaitForVariableValue(this WebView2 webView2, string variable, string value, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        await webView2.EnsureCoreWebView2Async().AsTask(token);

        string? result = "";
        while (result != value)
        {
            result = await webView2.CoreWebView2.ExecuteScriptAsync(variable).AsTask(token);
            result = result?.Trim('"');
            await Task.Delay(500, token);
        }
    }

    static async Task AssurePdfScriptsAsync(this Microsoft.UI.Xaml.Controls.WebView2 webView2, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        await webView2.AssureResourceFunctionLoadedAsync("html2pdf", "WebViewUtils.Resources.html2pdf.bundle.js", token);
        await webView2.AssureResourceFunctionLoadedAsync("p42_makePdf", "WebViewUtils.Resources.p42_makePdf.js", token);
    }

    static async Task AssureResourceFunctionLoadedAsync(this WebView2 webView2, string functionName, string resourceId, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        if (await webView2.IsFunctionLoadedAsync(functionName).WaitAsync(token))
            return;

        var script = await ReadResourceAsTextAsync(resourceId).WaitAsync(token);

        try
        {
            var result = await webView2.ExecuteScriptAsync(script).AsTask(token);
        }
        catch (Exception ex)
        {
            var msg = $"AssureResourceFunctionLoadedAsync({functionName}) exception: {ex}";
            Console.WriteLine(msg);
            Debug.WriteLine(msg);
        }

        if (await webView2.IsFunctionLoadedAsync(functionName).WaitAsync(token))
            return;

        throw new Exception($"AssureResourceFunctionLoadedAsync: Failed to load JavaScript function [{functionName}]");
    }

    public static async Task<bool> IsFunctionLoadedAsync(this WebView2 webView2, string functionName, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        var type = await webView2.ExecuteScriptAsync($"typeof {functionName};").AsTask(token);
        type = type?.Trim('"').Trim('"');
        return type?.Contains("function") ?? false;
    }

    public static async Task<string> ReadResourceAsTextAsync(string resourceId)
    {
        using var stream = typeof(WebViewExtensions).Assembly.GetManifestResourceStream(resourceId) ?? throw new InvalidOperationException("Resource not found");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    static string GenerateB64Guid()
    {
        var guid = Guid.NewGuid();
        return Convert.ToBase64String(guid.ToByteArray());
    }

    static async Task<TryResult<int>> TryExecuteIntScriptAsync(this Microsoft.UI.Xaml.Controls.WebView2 webView2, string script)
    {
//#if __WASM__ || !NET7_0_OR_GREATER
        try
        {
            var result = await webView2.ExecuteScriptAsync(script);
            if (int.TryParse(result, out int v))
                return new TryResult<int>(true, v);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebViewExtensions.TryExecuteIntScriptAsync {ex.GetType()} : {ex.Message} \n{ex.StackTrace} ");
        }
//#endif
        return await Task.FromResult(new TryResult<int>(false));
    }

    static async Task<TryResult<double>> TryExecuteDoubleScriptAsync(this Microsoft.UI.Xaml.Controls.WebView2 webView2, string script)
    {
//#if __WASM__ || !NET7_0_OR_GREATER
        try
        {
            var result = await webView2.ExecuteScriptAsync(script);
            System.Diagnostics.Debug.WriteLine($"WebViewExtensions.TryExecuteDoubleScriptAsync : [{script}] : [{result}]");
            if (double.TryParse(result, out var v))
                return new TryResult<double>(true, v);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebViewExtensions.TryExecuteDoubleScriptAsync {ex.GetType()} : {ex.Message} \n{ex.StackTrace} ");
        }
//#endif
        return await Task.FromResult(new TryResult<double>(false));
    }

    static async Task<double> TryUpdateIfLarger(this Microsoft.UI.Xaml.Controls.WebView2 webView2, string script, double source)
    {
        if (await webView2.TryExecuteDoubleScriptAsync(script) is TryResult<double> r1 && r1.IsSuccess && r1.Value > source)
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
    public static async Task<Windows.Foundation.Size> WebViewContentSizeAsync(this Microsoft.UI.Xaml.Controls.WebView2 webView, int depth = 0, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        if (webView is null)
            throw new ArgumentNullException(nameof(webView));

        //var hzAlign = webView.HorizontalAlignment;
        //webView.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;

        //System.Diagnostics.Debug.WriteLine("WebViewExtensions. webView Actual=[" + webView.ActualWidth + ", " + webView.ActualHeight + "]");
        //System.Diagnostics.Debug.WriteLine("WebViewExtensions. webView Desired=[" + webView.DesiredSize + "]");

        //var contentWidth = PageSize.Default.Width;
        //var contentHeight = PageSize.Default.Height;
        double contentWidth = -1;
        double contentHeight = -1;

        if (depth > 50)
            return new Windows.Foundation.Size(contentWidth, contentHeight);
        if (depth > 0)
            await Task.Delay(100);



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

            /*
            var docScrollWidth = await webView.ExecuteScriptAsync("document.documentElement.scrollWidth.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.documentElement.scrollWidth " + docScrollWidth);
            //if (double.TryParse(docScrollWidth, out var w1) && w1 > 0)
            //    contentWidth = w1;

            var bodyScrollWidth = await webView.ExecuteScriptAsync("document.body.scrollWidth.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.body.scrollWidth " + bodyScrollWidth);
            //if (double.TryParse(bodyScrollWidth, out var w2) && w2 > 0 && w2 > contentWidth)
            //    contentWidth = w2;



            var clientRectWidth = await webView.ExecuteScriptAsync("document.documentElement.getBoundingClientRect().width.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.documentElement.getBoundingClientRect().width " + clientRectWidth);
            var clientWidth = await webView.ExecuteScriptAsync("document.documentElement.clientWidth.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.documentElement.clientWidth " + clientWidth);
            var innerWidth = await webView.ExecuteScriptAsync("self.innerWidth.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. self.innerWidth " + innerWidth);
            var offsetWidth = await webView.ExecuteScriptAsync("document.body.offsetWidth.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.body.offsetWidth " + offsetWidth);


            System.Diagnostics.Debug.WriteLine("elementHeight = " + webView.Height);

            //var rect = await webView.InvokeScriptAsync("pizzx", new[] { "document.getElementById( 'rasta' ).clientHeight.toString()" });
            // ask the content its height
            var docScrollHeight = await webView.ExecuteScriptAsync("document.documentElement.scrollHeight.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.documentElement.scrollHeight " + docScrollHeight);
            
            var bodyScrollHeight = await webView.ExecuteScriptAsync("document.body.scrollHeight.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.body.scrollHeight " + bodyScrollHeight);
            var clientRectHeight = await webView.ExecuteScriptAsync("document.documentElement.getBoundingClientRect().height.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.documentElement.getBoundingClientRect().height " + clientRectHeight);
            var clientHeight = await webView.ExecuteScriptAsync("document.documentElement.clientHeight.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.documentElement.clientHeight " + clientHeight);
            var innerHeight = await webView.ExecuteScriptAsync("self.innerHeight.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. self.innerHeight " + innerHeight);
            var offsetHeight = await webView.ExecuteScriptAsync("document.body.offsetHeight.toString()");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions. document.body.offsetHeight " + offsetHeight);
            
            //var heightString = await webView.ExecuteScriptAsync("Math.max(document.body.scrollHeight, document.body.offsetHeight, document.documentElement.clientHeight, document.documentElement.scrollHeight ).toString()" });//, document.documentElement.offsetHeight ).toString()" });
            var heightString = docScrollHeight;
            double.TryParse(docScrollHeight, out contentHeight);
            */
        }
        catch (Exception e)
        {
            //await Forms9Patch.Debug.RequestUserHelp(e, "line = " + line + ", callerName=["+callerName+"]");
            System.Diagnostics.Debug.WriteLine("WebViewExtensions.WebViewContentSizeAsync FAIL: " + e.Message);
            Console.WriteLine("WebViewExtensions.WebViewContentSizeAsync FAIL: " + e.Message);
            return await WebViewContentSizeAsync(webView, depth + 1, callerName);
        }
        return new Windows.Foundation.Size(contentWidth, contentHeight);
    }




}

abstract record BaseAuxiliaryWebView
{
    public XamlRoot XamlRoot { get; init; }

    public CancellationTokenSource CancellationTokenSource { get; init; }

    public WebView2 WebView2 { get; init; }

    public ContentDialog ContentDialog { get; init; }

    public bool HideContent { get; set; }

    public string Html { get; init; }

    protected ProgressRing ProgressRing { get; init; }


    public BaseAuxiliaryWebView(XamlRoot xamlRoot, string html, CancellationTokenSource? cancellationTokenSource)
    {
        XamlRoot = xamlRoot;
        Html = html;
        CancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();

        WebView2 = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            //Opacity = 0.01
        };

        ContentDialog = new ContentDialog
        {            
            XamlRoot = xamlRoot,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
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
            cancelButton.Click += (s, e) => CancellationTokenSource.Cancel(true);
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

record AuxiliaryWebViewAsyncProcessor : BaseAuxiliaryWebView
{
    Func<WebView2, CancellationToken, Task>? Function;

    public TaskCompletionSource<bool> TaskCompletionSource { get; init; }

    public AuxiliaryWebViewAsyncProcessor(XamlRoot xamlRoot, string html, CancellationTokenSource? cancellationTokenSource) : base(xamlRoot, html, cancellationTokenSource)
    {
        TaskCompletionSource = new TaskCompletionSource<bool>();
    }

    public async Task ProcessAsync(Func<WebView2, CancellationToken, Task> function)
    {
        if (function is null)
            throw new ArgumentNullException(nameof(function));
        Function = function;
        ContentDialog.Loaded += OnLoaded;
        await ShowAsync();
        await TaskCompletionSource.Task;
    }

    public async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Function is null)
                throw new ArgumentNullException(nameof(Function));
            await WebView2.EnsureCoreWebView2Async().AsTask(CancellationTokenSource.Token);

            WebView2.CoreWebView2.NavigateToString(Html);
            WebView2.CoreWebView2.NavigateToString(Html);
            await WebView2.WaitForDocumentLoadedAsync();
            if (!HideContent)
                ProgressRing.Visibility = Visibility.Collapsed;

            await Function(WebView2, CancellationTokenSource.Token);
            TaskCompletionSource.SetResult(true);
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

record AuxiliaryWebViewAsyncProcessor<T> : BaseAuxiliaryWebView
{
    Func<WebView2, CancellationToken, Task<T>>? Function;

    public TaskCompletionSource<T> TaskCompletionSource { get; init; }

    public AuxiliaryWebViewAsyncProcessor(XamlRoot xamlRoot, string html, CancellationTokenSource? cancellationTokenSource) : base(xamlRoot, html, cancellationTokenSource)
    {
        TaskCompletionSource = new TaskCompletionSource<T>();
    }

    public async Task<T> ProcessAsync(Func<WebView2, CancellationToken, Task<T>> function)
    {
        if (function is null)
            throw new ArgumentNullException(nameof(function));
        Function = function;
        ContentDialog.Loaded += OnLoaded;
        await ShowAsync();
        return await TaskCompletionSource.Task;
    }

    public async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Function is null)
                throw new ArgumentNullException(nameof(Function));
            await WebView2.EnsureCoreWebView2Async().AsTask(CancellationTokenSource.Token);

            WebView2.CoreWebView2.NavigateToString(Html);
            await WebView2.WaitForDocumentLoadedAsync();
            if (!HideContent)
                ProgressRing.Visibility = Visibility.Collapsed;

            var result = await Function(WebView2, CancellationTokenSource.Token);
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

