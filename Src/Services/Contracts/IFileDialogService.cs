using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace FeedDesk.Services.Contracts;

public interface IFileDialogService
{

    Task<StorageFile?> GetOpenOpmlFileDialog(IntPtr hwnd);

    Task<StorageFile?> GetSaveOpmlFileDialog(IntPtr hwnd);
}
