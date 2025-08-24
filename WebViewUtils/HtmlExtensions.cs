using WebViewUtils;

namespace P42.Uno;

public static class HtmlExtensions
{
    public static async Task PrintAsync(UIElement element, string html, CancellationToken token = default)
    {
        if (element.XamlRoot is null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");
        
        await DialogExtensions.AuxiliaryWebViewAsyncProcessor<bool>.Create(element.XamlRoot, html, PrintFunction, showWebContent: OperatingSystem.IsWindows(), cancellationToken:  token);
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

        await DialogExtensions.AuxiliaryWebViewAsyncProcessor<bool>.Create(element.XamlRoot, html, MakePdfFunction,
            cancellationToken: token);
        return;

        async Task<bool> MakePdfFunction(WebView2 webView, CancellationToken localToken)
        {
            var pdfTask =  webView.GeneratePdfAsync(options, localToken);
            await WebViewExtensions.InternalSavePdfAsync(element, pdfTask, fileName, localToken);
            return true;
        }
    }

    public static async Task<(byte[]? pdf, string error)> GeneratePdfAsync(this UIElement element, string html, PdfOptions? options = null, CancellationToken token = default)
    {
        if (element.XamlRoot == null)
            throw new ArgumentNullException($"{nameof(element)}.{nameof(element.XamlRoot)}");
        
        return await DialogExtensions.AuxiliaryWebViewAsyncProcessor<(byte[]? pdf, string error)>.Create(
            element.XamlRoot, 
            html,
            MakePdfFunction, 
            cancellationToken: token);

        async Task<(byte[]?, string)> MakePdfFunction(WebView2 webView, CancellationToken localToken)
            => await webView.GeneratePdfAsync(options, localToken);
        
    }

}
