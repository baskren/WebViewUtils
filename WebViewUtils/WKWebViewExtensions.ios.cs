using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Windows.Storage.Pickers;
using Foundation;
using UIKit;
using WebKit;

namespace WebViewUtils;

public static class WKWebViewExtensions
{
    public static Task<(bool Successful, string errorMessage)> PrintAsync(this WKWebView webView, string title = "")
    {
        var tcs = new TaskCompletionSource<(bool Successful, string errorMessage)>();
        
        var printInfo = UIPrintInfo.PrintInfo;
        printInfo.OutputType = UIPrintInfoOutputType.General;
        printInfo.JobName = string.IsNullOrWhiteSpace(title)
            ? $"{GetAppTitle()} Document"
            : title;

        var printController = UIPrintInteractionController.SharedPrintController;
        printController.PrintInfo = printInfo;
        printController.ShowsNumberOfCopies = false;

        // This is the key: use the webView's viewPrintFormatter
        printController.PrintFormatter = webView.ViewPrintFormatter;

        // Present the print dialog
        printController.Present(true, (_, completed, error) =>
        {
                tcs.TrySetResult((completed, completed 
                        ? "success"
                        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                        : error?.LocalizedDescription ?? "cancelled"
                    ));
        });
        
        return tcs.Task;
    }
    
    private static string GetAppTitle()
    {
        var title = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleDisplayName").ToString();
        if (string.IsNullOrWhiteSpace(title))
            title = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleName").ToString();
        if (string.IsNullOrWhiteSpace(title))
            title = "Application";
        
        return title;
    }
}

[SuppressMessage("Compatibility", "Uno0001:Uno type or member is not implemented")]
public partial class CustomFileSavePicker(XamlRoot xamlRoot) : FileSavePicker
{
    private ContentDialog _contentDialog = new();
    
    private bool _enterPressed;
    
    public new async Task<StorageFile?> PickSaveFileAsync()
    {
        _enterPressed = false;
        
        var folderPicker = new FolderPicker
        {
            SuggestedStartLocation = SuggestedStartLocation
        };

        if (await folderPicker.PickSingleFolderAsync() is not { } storageFolder)
            return null;


        var textBox = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            AcceptsReturn = true,
            IsSpellCheckEnabled = true,
            PlaceholderText = SuggestedFileName
        };

        textBox.BeforeTextChanging += OnFileNameBeforeTextChanging;

        _contentDialog
            .Title($"File name to be saved in {storageFolder.DisplayName}")
            .Content(textBox)
            .SecondaryButtonText("Cancel")
            .DefaultButton(ContentDialogButton.Close)
            .PrimaryButtonText("Save")
            .IsPrimaryButtonEnabled(true)
            .IsSecondaryButtonEnabled(true);
        _contentDialog.XamlRoot = xamlRoot;
            

        var tcs = new TaskCompletionSource<ContentDialogResult>();
        
        // ReSharper disable once AsyncVoidLambda
        _contentDialog.DispatcherQueue.TryEnqueue(async() =>
        {
            try
            {
                var result = await _contentDialog.ShowAsync();
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if ( await tcs.Task != ContentDialogResult.Primary && !_enterPressed)
            return null;

        var fileName = textBox.Text;
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        
        if (string.IsNullOrWhiteSpace(System.IO.Path.GetExtension(fileName)) 
            && FileTypeChoices.FirstOrDefault() is { Value: { } extensions } 
            && extensions.FirstOrDefault() is { } defaultExtension
            && !string.IsNullOrWhiteSpace(defaultExtension)
            )
            fileName += defaultExtension;
        
        return await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            
    }
    
    private void OnFileNameBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (args.NewText is not { } newText || string.IsNullOrWhiteSpace(newText))
            return;

        if (newText.Contains('\n'))
        {
            _enterPressed = true;
            args.Cancel = true;
            _contentDialog.Hide();
        }
        else
        {
            _enterPressed = false;
        }
        
        var hasSpecial = MyRegex().IsMatch(args.NewText);
        
        args.Cancel = hasSpecial;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9._ ]")]
    private static partial Regex MyRegex();

}
