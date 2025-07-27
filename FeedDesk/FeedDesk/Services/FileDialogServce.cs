using FeedDesk.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using WinRT.Interop;

namespace FeedDesk.Services;

public class FileDialogService : IFileDialogService
{
    FileOpenPicker? _fileOpenPicker;
    FileSavePicker? _fileSavePicker;

    public async Task<StorageFile?> GetOpenOpmlFileDialog(IntPtr hwnd)
    {
        _fileOpenPicker ??= new FileOpenPicker();

        _fileOpenPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        //_fileOpenPicker.FileTypeFilter.Add("*");
        _fileOpenPicker.FileTypeFilter.Add(".opml");
        _fileOpenPicker.FileTypeFilter.Add(".xml");
        _fileOpenPicker.FileTypeFilter.Add(".txt");
        _fileOpenPicker.SettingsIdentifier = "OpmlFileIdentifier";
        
        //var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(_fileOpenPicker, hwnd);

        var file = await _fileOpenPicker.PickSingleFileAsync();

        return file;
    }

    public async Task<StorageFile?> GetSaveOpmlFileDialog(IntPtr hwnd)
    {
        _fileSavePicker ??= new FileSavePicker();

        _fileSavePicker.SuggestedStartLocation = PickerLocationId.Desktop;
        _fileSavePicker.FileTypeChoices.Add("Opml", [".opml"]);
        _fileSavePicker.FileTypeChoices.Add("Plain xml", [".xml"]);
        _fileSavePicker.FileTypeChoices.Add("Plain Text", [".txt"]);
        _fileSavePicker.SuggestedFileName = "Feeds";
        _fileSavePicker.SettingsIdentifier = "OpmlFileIdentifier";
        _fileSavePicker.DefaultFileExtension = ".opml";

        //var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(_fileSavePicker, hwnd);

        var file = await _fileSavePicker.PickSaveFileAsync();

        return file;
    }
}