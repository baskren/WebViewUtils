using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
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

    private static object GetNativeWebViewWrapper(this WebView2 webView2)
    {
        if (typeof(CoreWebView2).GetField("_nativeWebView", BindingFlags.Instance | BindingFlags.NonPublic) is not {} nativeWebViewField)
            throw new Exception("Unable to obtain _nativeWebView field information");
        var nativeWebView = nativeWebViewField.GetValue(webView2.CoreWebView2);
        return nativeWebView ?? throw new Exception("Unable to obtain native webview");
    }
    
    public static async Task PrintAsync(this WebView2 webView2, CancellationToken token = default)
    {
            await webView2.WaitForDocumentLoadedAsync(token);
#if __ANDROID__
            var nativeWebViewWrapper = webView2.GetNativeWebViewWrapper();
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
            var nativeWebViewWrapper = webView2.GetNativeWebViewWrapper();
            if (nativeWebViewWrapper is not WebKit.WKWebView wkWebView)
                throw new Exception("Unable to obtain native webview");

            var result = await wkWebView.PrintAsync();
#else
            await webView2.ExecuteScriptAsync("print();").AsTask(token);

#endif
    }

    public static async Task PrintAsync(UIElement element, string html, CancellationToken token = default)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");
        
        await AuxiliaryWebViewAsyncProcessor<bool>.Create(element.XamlRoot, html, PrintFunction, showWebContent: OperatingSystem.IsWindows(), cancellationToken:  token);
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

            var fileName = string.IsNullOrEmpty(options?.Filename)
                ? "document"
                : options.Filename;

            await AuxiliaryWebViewAsyncProcessor<bool>.Create(element.XamlRoot, html, MakePdfFunction,
                cancellationToken: token);
            return;

            async Task<bool> MakePdfFunction(WebView2 webView, CancellationToken localToken)
            {
                var pdfTask =  webView.GeneratePdfAsync(options, localToken);
                await InternalSavePdfAsync(element, pdfTask, fileName, localToken);
                return true;
            }
    }

    public static async Task SavePdfAsync(this WebView2 webView,  PdfOptions? options = null, CancellationToken token = default)
    {
        var pdfTask = webView.GeneratePdfAsync(options, token);

        var fileName = string.IsNullOrEmpty(options?.Filename)
            ? "document"
            : options.Filename;

        await BusyDialog.Create(webView.XamlRoot, "Generating / Saving PDF", MakePdfFunction, cancellationToken: token);
        return;
        
        async Task MakePdfFunction(CancellationToken localToken)
            => await InternalSavePdfAsync(webView, pdfTask, fileName, localToken);


    }

    private static async Task InternalSavePdfAsync(UIElement element, Task<(byte[]? pdf, string error)> pdfTask, string fileName, CancellationToken token)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");

        StorageFile? saveFile = null;
        var fileTask = RequestStorageFileAsync( element.XamlRoot, "PDF", fileName, "pdf");

        try
        {
            await Task.WhenAll(fileTask, pdfTask);
            saveFile = fileTask.Result;
        }
        catch (Exception ex)
        {
            throw;
            return;
        }

        if (token.IsCancellationRequested)
            return;

        if (pdfTask.Result.pdf is null || pdfTask.Result.pdf.Length == 0)
        {
            await ShowErrorDialogAsync(element.XamlRoot, "PDF Generation Error", pdfTask.Result.error);
            return;
        }

        if (token.IsCancellationRequested)
            return;

        try
        {
            CachedFileManager.DeferUpdates(saveFile);
#if __DESKTOP__
            await System.IO.File.WriteAllBytesAsync(saveFile.Path, pdfTask.Result.pdf, token);
#else
            await FileIO.WriteBytesAsync(saveFile, pdfTask.Result.pdf);
#endif
            await CachedFileManager.CompleteUpdatesAsync(saveFile);
        }
        catch (Exception e)
        {
            await ShowExceptionDialogAsync(element.XamlRoot, "File Save", e);
        }
    }
    
    
    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this UIElement element, string html, PdfOptions? options = null, CancellationToken token = default)
    {
        if (element.XamlRoot == null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");
        
        return await AuxiliaryWebViewAsyncProcessor<(byte[]? pdf, string error)>.Create(
            element.XamlRoot, 
            html,
            MakePdfFunction, 
            cancellationToken: token);

        async Task<(byte[]?, string)> MakePdfFunction(WebView2 webView, CancellationToken localToken)
            => await webView.GeneratePdfAsync(options, localToken);
        
    }

    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this WebView2 webView2, PdfOptions? options = null, CancellationToken token = default)
    {
        try
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
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            return (null, ex.ToString());
        }
    }

    public static async Task<StorageFile?> RequestStorageFileAsync(XamlRoot xamlRoot, string type,  string suggestedName, params List<string> suffixes)
    {

        if (!string.IsNullOrEmpty(suggestedName) && suggestedName.Contains('.'))
        {
            var extension = System.IO.Path.GetExtension(suggestedName);
            suffixes.Insert(0, extension);
            suggestedName = System.IO.Path.GetFileNameWithoutExtension(suggestedName);
        }
        
        #if __IOS__
        var picker = new CustomFileSavePicker(xamlRoot)
        #else
        var picker = new FileSavePicker
        #endif
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = suggestedName,
        };

        if (suffixes is { Count: > 0 })
        {
            for (var i = suffixes.Count - 1; i >= 0; i--)
            {
                var suffix = suffixes[i].TrimStart('.');
                if (string.IsNullOrWhiteSpace(suffix))
                    suffixes.RemoveAt(i);
                else
                    suffixes[i] = $".{suffix}";
            }

            picker.FileTypeChoices.Add(type, suffixes);
        }



#if WINDOWS
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)App.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
#elif BROWSERWASM
        if (!Directory.Exists("/cache"))
            Directory.CreateDirectory("/cache");
#endif
        
        return await picker.PickSaveFileAsync() ??  throw new TaskCanceledException();

    }

    internal static Task ShowExceptionDialogAsync(XamlRoot xamlRoot, string title, Exception e)
        => ShowErrorDialogAsync(xamlRoot, title, e is TaskCanceledException ? "Task Cancelled" : e.ToString());
    internal static async Task ShowErrorDialogAsync(XamlRoot xamlRoot, string title, string? error)
    {
        ContentDialog cd = new ()
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = string.IsNullOrWhiteSpace(error)
                ? "Unknown failure"
                : new ScrollViewer()
                    .Content
                        (
                            new TextBlock()
                                .Text(error)
                                .TextWrapping(TextWrapping.WrapWholeWords)
                                .MaxLines(1000)
                            ),
            PrimaryButtonText = "OK"
        };
        await cd.ShowAsync();
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
        await webView2.AssureResourceFunctionLoadedAsync("html2pdf", "WebViewUtils.Resources.html2pdf.bundle.js", token);
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

internal class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    : ICommand
{
    private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal class BusyDialog
{
    #region  Fields

    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly CancellationToken _cancellationToken;

    private readonly Func<CancellationToken, Task> _function;
    
    private readonly ContentDialog _contentDialog;
    private readonly ProgressRing _progressRing;
    
    private readonly XamlRoot _xamlRoot;
    private readonly string _title;
    #endregion

    public static async Task Create(
        XamlRoot xamlRoot, 
        string title,
        Func<CancellationToken, Task> processFunction, 
        bool hasCancelButton = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(title);
        var processor = new BusyDialog(xamlRoot, title, processFunction, hasCancelButton, cancellationToken);
        await processor.ProcessAsync();
    }
    
    private BusyDialog(XamlRoot xamlRoot, string title, Func<CancellationToken, Task> processFunction,
        bool hasCancelButton = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(processFunction);
        
        
        CancellationTokenSource uiCancellationTokenSource = new ();
        _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(uiCancellationTokenSource.Token, cancellationToken).Token;
        
        _xamlRoot = xamlRoot;
        _function = processFunction;
        _title = title;
        
        _contentDialog = new ContentDialog
        {            
            XamlRoot = _xamlRoot,
            Title = _title,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            Content =  new ProgressRing()
                .Name(out _progressRing)
                .IsActive(true),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        _contentDialog.Loaded += OnLoaded;

        if (!hasCancelButton)
            return;

        _contentDialog.CloseButtonText = "CANCEL";
        _contentDialog.CloseButtonCommand = new RelayCommand((_) => uiCancellationTokenSource.Cancel());
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _function(_cancellationToken);
            _contentDialog.Content = "COMPLETED";
            _contentDialog.CloseButtonText = "OK";
            _tcs.TrySetResult(true);
            await Task.Delay(3000, _cancellationToken);
        }
        /*
        catch (TaskCanceledException tce)
        {
            _contentDialog.Content = "CANCELLED";
            _tcs.TrySetException(tce);
        }
        */
        catch (Exception ex)
        {
            /*
            _contentDialog.Title = $"{_title} Error";
            _contentDialog.Content = new ScrollViewer()
                .Content
                (
                    new TextBlock()
                        .Text(ex.ToString())
                        .TextWrapping(TextWrapping.WrapWholeWords)
                        .MaxLines(1000)
                );
                */
            _tcs.TrySetException(ex);
        }
        finally
        {
            _contentDialog.CloseButtonText = "OK";
            _contentDialog.Hide();
            _contentDialog.Loaded -= OnLoaded;
        }
    }

    private async Task ProcessAsync()
    {
        await _contentDialog.ShowAsync();
        await _tcs.Task;
    }

}

internal class AuxiliaryWebViewAsyncProcessor<T> 
{
    
    
    #region  Fields

    private readonly TaskCompletionSource<T> _tcs = new();
    private readonly CancellationToken _cancellationToken;

    private readonly Func<WebView2, CancellationToken, Task<T>> _function;
    private readonly Func<WebView2, CancellationToken, Task> _loadContentAction;
    
    private readonly ContentDialog _contentDialog;
    private readonly WebView2 _webView2;
    private readonly ProgressRing _progressRing;

    private bool _showWebContent;
    #endregion

    public static async Task<T> Create(
        XamlRoot xamlRoot, 
        string html,
        Func<WebView2, CancellationToken, Task<T>> onLoadedFunction, 
        bool showWebContent = false,
        bool hasCancelButton = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentNullException(nameof(html));
        
        var processor = new AuxiliaryWebViewAsyncProcessor<T>(xamlRoot, LoadHtml, onLoadedFunction, showWebContent, hasCancelButton, cancellationToken);
        return await processor.ProcessAsync();
        
        Task LoadHtml(WebView2 webView, CancellationToken localToken)
        {
            webView.CoreWebView2.NavigateToString(html);
            return Task.CompletedTask;
        }

    }

    private AuxiliaryWebViewAsyncProcessor(XamlRoot xamlRoot, Func<WebView2, CancellationToken, Task> loadContentAction, Func<WebView2, CancellationToken, Task<T>> contentLoadedFunction, bool showWebContent, bool hasCancelButton, CancellationToken cancellationToken) 
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(loadContentAction);
        ArgumentNullException.ThrowIfNull(contentLoadedFunction);

        CancellationTokenSource uiCancellationTokenSource = new ();
        _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(uiCancellationTokenSource.Token, cancellationToken).Token;

        _loadContentAction = loadContentAction;
        _function = contentLoadedFunction;
        _showWebContent = showWebContent;

        _contentDialog = new ContentDialog
        {            
            XamlRoot = xamlRoot,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            Content = new Grid()
                .Children
                (
                    new WebView2()
                        .HorizontalAlignment(HorizontalAlignment.Stretch)
                        .VerticalAlignment(VerticalAlignment.Stretch)
                        .Name(out _webView2)
                        .Opacity(showWebContent ? 1 : 0.01),

                    new Rectangle()
                        .HorizontalAlignment(HorizontalAlignment.Stretch)
                        .VerticalAlignment(VerticalAlignment.Stretch)
                        .Fill((Brush)Application.Current.Resources["ContentDialogBackground"])
                        .Visibility(showWebContent ? Visibility.Collapsed : Visibility.Visible),
                
                    new ProgressRing()
                        .Name(out _progressRing)
                        .IsActive(true)

                )
        };

#if WINDOWS
        if (showWebContent)
        {
            _contentDialog.Resources["ContentDialogPadding"] = new Thickness(4);
            _contentDialog.Resources["ContentDialogMaxWidth"] = xamlRoot.Size.Width * 0.75;
            _contentDialog.Resources["ContentDialogMaxHeight"] = xamlRoot.Size.Height * 0.75;
            _contentDialog.Resources["ContentDialogMinWidth"] = xamlRoot.Size.Width * 0.75;
            _contentDialog.Resources["ContentDialogMinHeight"] = xamlRoot.Size.Height * 0.75;
            _contentDialog.Resources["HorizontalContentAlignment"] = HorizontalAlignment.Stretch;
            _contentDialog.Resources["VerticalContentAlignment"] = VerticalAlignment.Stretch;
        }
#endif
        _webView2.Loaded += OnLoaded;
            
        if (!hasCancelButton)
            return;

        _contentDialog.CloseButtonText = "CANCEL";
        _contentDialog.CloseButtonCommand = new RelayCommand((_) => uiCancellationTokenSource.Cancel());
        
    }

    private async Task<T> ProcessAsync()
    {
        await _contentDialog.ShowAsync();
        return await _tcs.Task;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(1000);  // without this, WinSdk version crashes

        try
        {
            await _webView2.EnsureCoreWebView2Async().AsTask(_cancellationToken);
            await _loadContentAction.Invoke(_webView2, _cancellationToken);

            await _webView2.WaitForDocumentLoadedAsync(_cancellationToken);

            if (_showWebContent)
                _progressRing.Visibility = Visibility.Collapsed;

            var result = await _function(_webView2, _cancellationToken);
            _contentDialog.Content = "COMPLETED";
            _contentDialog.CloseButtonText = "OK";
            _tcs.TrySetResult(result);
            await Task.Delay(3000, _cancellationToken);
        }
        /*
        catch (TaskCanceledException tce)
        {
            _contentDialog.Content = "CANCELLED";
            _tcs.TrySetException(tce);
        }
        */
        catch (Exception ex)
        {
            /*
            _contentDialog.Title = "Error"; // $"{_title} Error";
            _contentDialog.Content = new ScrollViewer()
                .Content
                (
                    new TextBlock()
                        .Text(ex.ToString())
                        .TextWrapping(TextWrapping.WrapWholeWords)
                        .MaxLines(1000)
                );
            _contentDialog.CloseButtonText = "OK";
            */
            _tcs.TrySetException(ex);
        }
        finally
        {
            _contentDialog.Hide();
            _contentDialog.Loaded -= OnLoaded;
        }
    }


}

