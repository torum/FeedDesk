using XmlClients.Core.Helpers;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;

namespace FeedDesk;

public sealed partial class MainWindow : WindowEx
{
    private readonly UISettings settings;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "FeedDesk3.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();

        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged;

        // SystemBackdrop
        if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            if (RuntimeHelper.IsMSIX)
            {
                // Load preference from localsetting.
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(App.BackdropSettingsKey, out var obj))
                {
                    var s = (string)obj;
                    if (s == SystemBackdropOption.Acrylic.ToString())
                    {
                        SystemBackdrop = new DesktopAcrylicBackdrop();
                    }
                    else if (s == SystemBackdropOption.Mica.ToString())
                    {
                        SystemBackdrop = new MicaBackdrop()
                        {
                            Kind = MicaKind.Base
                        };
                    }
                    else
                    {
                        SystemBackdrop = new DesktopAcrylicBackdrop();
                    }
                }
                else
                {
                    // default acrylic.
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                }
            }
            else
            {
                // just for me.
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }

        }
        else if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop()
            {
                Kind = MicaKind.Base
            };
        }
        else
        {
            // Memo: Without Backdrop, theme setting's theme is not gonna have any effect( "system default" will be used). So the setting is disabled.
        }
    }

    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        var frame = App.AppTitlebar as FrameworkElement;

        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        App.CurrentDispatcherQueue.TryEnqueue(() =>
        {
            TitleBarHelper.ApplySystemThemeToCaptionButtons(frame, App.MainWindow);
        });
    }
}
