using XmlClients.Core.Helpers;
using FeedDesk.Contracts.Services;
using Microsoft.UI.Xaml;
using Windows.Storage;

namespace FeedDesk.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    //private readonly ILocalSettingsService _localSettingsService;

    public ThemeSelectorService()//ILocalSettingsService localSettingsService
    {
        //_localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        Theme = LoadThemeFromSettingsAsync();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ElementTheme theme)
    {
        Theme = theme;

        await SetRequestedThemeAsync();
        await SaveThemeInSettingsAsync(Theme);
    }

    public async Task SetRequestedThemeAsync()
    {
        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = Theme;

            TitleBarHelper.UpdateTitleBar(Theme, App.MainWindow);
        }

        await Task.CompletedTask;
    }

    private ElementTheme LoadThemeFromSettingsAsync()
    {
        /*
        var themeName = await _localSettingsService.ReadSettingAsync<string>(SettingsKey);

        if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
        {
            return cacheTheme;
        }
        
        */

        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(SettingsKey, out var obj))
            {
                var themeName = (string)obj;
                if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
                {
                    return cacheTheme;
                }
            }
        }
        return ElementTheme.Default;
    }

    private async Task SaveThemeInSettingsAsync(ElementTheme theme)
    {
        //await _localSettingsService.SaveSettingAsync(SettingsKey, theme.ToString());

        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = theme.ToString();
        }

        await Task.CompletedTask;
    }
}
