using System.Reflection;
using System.Windows.Input;
using XmlClients.Core.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FeedDesk.Contracts.Services;
using FeedDesk.Contracts.ViewModels;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.Storage;

namespace FeedDesk.ViewModels;

public class SettingsViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;

    private readonly IThemeSelectorService _themeSelectorService;
    
    //private string _versionDescription;

    private SystemBackdropOption _material = SystemBackdropOption.Mica;
    public SystemBackdropOption Material
    {
        get => _material;
        set => SetProperty(ref _material, value);
    }

    private bool _isAcrylicSupported;
    public bool IsAcrylicSupported
    {
        get => _isAcrylicSupported;
        set => SetProperty(ref _isAcrylicSupported, value);
    }

    private bool _isSystemBackdropSupported = true;
    public bool IsSystemBackdropSupported
    {
        get => _isSystemBackdropSupported;
        set => SetProperty(ref _isSystemBackdropSupported, value);
    }
    //

    private ElementTheme _elementTheme = ElementTheme.Default;
    public ElementTheme ElementTheme
    {
        get => _elementTheme;
        set => SetProperty(ref _elementTheme, value);
    }
    /*
    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }
    */

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public ICommand SwitchSystemBackdropCommand
    {
        get;
    }

    public ICommand GoBackCommand
    {
        get;
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, INavigationService navigationService)
    {
        _navigationService = navigationService;

        _themeSelectorService = themeSelectorService;
        _elementTheme = _themeSelectorService.Theme;
        //_versionDescription = GetVersionDescription();


        if (App.MainWindow.SystemBackdrop is DesktopAcrylicBackdrop)
        {
            Material = SystemBackdropOption.Acrylic;
        }else if (App.MainWindow.SystemBackdrop is MicaBackdrop)
        {
            Material = SystemBackdropOption.Mica;
        }

        if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            IsAcrylicSupported = true;
        }
        else
        {
            IsAcrylicSupported = false;

            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                //
            }
            else
            {
                IsSystemBackdropSupported = false;
            }
        }

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });

        SwitchSystemBackdropCommand = new RelayCommand<string>(OnSwitchSystemBackdrop);

        GoBackCommand = new RelayCommand(OnGoBack);
    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }

    public string VersionText
    {
        get
        {
            Version version;

            if (RuntimeHelper.IsMSIX)
            {
                var packageVersion = Package.Current.Id.Version;

                version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
            }
            else
            {
                version = Assembly.GetExecutingAssembly().GetName().Version!;
                //Debug.WriteLine("asdf" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            }
            
            return $"{"Version".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
    /*
    private string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return $"{"Version".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
    */

    private void OnSwitchSystemBackdrop(string? backdrop)
    {
        if (backdrop != null)
        {
            if (App.MainWindow is not null)
            {
                if (backdrop == "Mica")
                {
                    if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported() || Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
                    {
                        App.MainWindow.SystemBackdrop = new MicaBackdrop()
                        {
                            Kind = MicaKind.Base
                        };
                        if (RuntimeHelper.IsMSIX)
                        {
                            ApplicationData.Current.LocalSettings.Values[App.BackdropSettingsKey] = SystemBackdropOption.Mica.ToString();
                        }
                        Material = SystemBackdropOption.Mica;
                    }
                }
                else if (backdrop == "Acrylic")
                {
                    if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
                    {
                        App.MainWindow.SystemBackdrop = new DesktopAcrylicBackdrop();
                        if (RuntimeHelper.IsMSIX)
                        {
                            ApplicationData.Current.LocalSettings.Values[App.BackdropSettingsKey] = SystemBackdropOption.Acrylic.ToString();
                        }
                        Material = SystemBackdropOption.Acrylic;
                    }
                }
            }
        }
    }

    private void OnGoBack()
    {
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }
}
