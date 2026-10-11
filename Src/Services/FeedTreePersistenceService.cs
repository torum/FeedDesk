using FeedDesk.Helpers;
using FeedDesk.Services.Contracts;
using System.IO;
using System.Xml;
using Windows.Storage;

namespace FeedDesk.Services;

public sealed class FeedTreePersistenceService : IFeedTreePersistenceService
{
    private const string FileName = "Searvies.xml";
    private readonly string _filePath;

    public FeedTreePersistenceService()
    {
        _filePath = RuntimeHelper.IsMSIX
            ? Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName)
            : Path.Combine(FeedDesk.App.AppDataFolder, FileName);
    }

    public XmlDocument? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var document = new XmlDocument();
        document.Load(_filePath);
        return document;
    }

    public void Save(XmlDocument document)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _filePath + ".tmp";
        document.Save(temporaryPath);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}