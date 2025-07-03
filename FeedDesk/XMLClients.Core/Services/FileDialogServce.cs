using XmlClients.Core.Contracts.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using WinRT.Interop;

namespace XmlClients.Core.Services;

public class FileDialogService : IFileDialogService
{
    public async Task<StorageFile?> GetOpenOpmlFileDialog(IntPtr hwnd)
    {
        FileOpenPicker picker = new();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".opml");
        picker.FileTypeFilter.Add(".xml");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add("*");
        picker.SettingsIdentifier = "OpmlFileIdentifier";

        //var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();

        return file;
    }

    public async Task<StorageFile?> GetSaveOpmlFileDialog(IntPtr hwnd)
    {
        FileSavePicker picker = new();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeChoices.Add("Opml", new List<string>() { ".opml" });
        picker.FileTypeChoices.Add("Plain xml", new List<string>() { ".xml" });
        picker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
        picker.SuggestedFileName = "Feeds";
        picker.SettingsIdentifier = "OpmlFileIdentifier";
        picker.DefaultFileExtension = ".opml";

        //var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();

        return file;
    }
}