using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.System;
using Foundation;
using UIKit;
using WebKit;

namespace WebViewUtils;

public static class IosWebViewExtensions
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

public partial class CustomFileSavePicker(XamlRoot xamlRoot) : FileSavePicker
{
    ContentDialog contentDialog;
    private bool enterPressed;
    
    public new async Task<StorageFile?> PickSaveFileAsync()
    {
        enterPressed = false;
        
        var folderPicker = new FolderPicker()
        {
            SuggestedStartLocation = SuggestedStartLocation,
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
            PlaceholderText = SuggestedFileName,
        };

        textBox.BeforeTextChanging += OnFileNameBeforeTextChanging;
        contentDialog = new ContentDialog
        {
            Title = $"File name to be saved in {storageFolder.DisplayName}",
            Content = textBox,
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            XamlRoot = xamlRoot
        };



        var tcs = new TaskCompletionSource<ContentDialogResult>();
        
        contentDialog.DispatcherQueue.TryEnqueue(async() =>
        {
            var result = await contentDialog.ShowAsync();    
            tcs.TrySetResult(result);
        });

        if ( await tcs.Task != ContentDialogResult.Primary && !enterPressed)
            return null;

        var fileName = textBox.Text;
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        
        if (string.IsNullOrWhiteSpace(System.IO.Path.GetExtension(fileName)) 
            && FileTypeChoices.FirstOrDefault() is KeyValuePair<string, IList<string>> defaultFileType 
            && defaultFileType.Value is IList<string> extensions 
            && extensions.FirstOrDefault() is string defaultExtension 
            && !string.IsNullOrWhiteSpace(defaultExtension))
            fileName += defaultExtension;
        
            return await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            
    }
    
    private void OnFileNameBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (args.NewText is not { } newText || string.IsNullOrWhiteSpace(newText))
            return;

        if (newText.Contains('\n'))
        {
            enterPressed = true;
            args.Cancel = true;
            contentDialog.Hide();
        }
        else
        {
            enterPressed = false;
        }
        
        var hasSpecial = MyRegex().IsMatch(args.NewText);
        
        args.Cancel = hasSpecial;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9._ ]")]
    private static partial Regex MyRegex();

}

public partial class CustomFileSavePickerExtension(object owner, XamlRoot xamlRoot) : Uno.Extensions.Storage.Pickers.IFileSavePickerExtension
{
    public async Task<StorageFile?> PickSaveFileAsync(CancellationToken token)
    {
        if (owner is not FileSavePicker filePicker)
            return null;
        
        var folderPicker = new FolderPicker()
        {
            SuggestedStartLocation = filePicker.SuggestedStartLocation,
        };

        if (await folderPicker.PickSingleFolderAsync() is not { } storageFolder)
            return null;


        var textBox = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalTextAlignment = TextAlignment.Right,
            AcceptsReturn = false,
            IsSpellCheckEnabled = true,
            PlaceholderText = filePicker.SuggestedFileName
        };

        textBox.BeforeTextChanging += OnFileNameBeforeTextChanging;
        
        ContentDialog cd = new ContentDialog
        {
            Title = $"File name to be saved in {storageFolder.DisplayName}",
            Content = "textBox",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            XamlRoot = xamlRoot
        };
        
        var result = await cd.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return null;

        if (!string.IsNullOrWhiteSpace(textBox.Text))
            return await storageFolder.CreateFileAsync(textBox.Text, CreationCollisionOption.ReplaceExisting);

        if (string.IsNullOrWhiteSpace(filePicker.SuggestedFileName))
            return null;
        
        return await storageFolder.CreateFileAsync(filePicker.SuggestedFileName, CreationCollisionOption.GenerateUniqueName);
    }

    private static void OnFileNameBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (args.NewText is not { } newText || string.IsNullOrWhiteSpace(newText))
            return;
        
        var hasSpecial = MyRegex().IsMatch(args.NewText);
        
        args.Cancel = hasSpecial;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9._ ]")]
    private static partial Regex MyRegex();
}
