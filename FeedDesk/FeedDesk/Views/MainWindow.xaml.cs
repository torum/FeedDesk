using CommunityToolkit.WinUI;
using FeedDesk.Helpers;
using FeedDesk.ViewModels;
using FeedDesk.Views;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Windows.Storage;
using WinRT;

namespace FeedDesk;

public partial class MainWindow : Window
{
    // Window position and size
    // TODO: Change this lator.1920x1080
    private int _winRestoreWidth = 1024;//1024;
    private int _winRestoreHeight = 768;//768;
    private int _winRestoreTop = 100;
    private int _winRestoreleft = 100;

    //private readonly UISettings settings;
    private ElementTheme theme = ElementTheme.Default;

    public MainWindow() 
    {
        InitializeComponent();

        this.Title = "AppDisplayName".GetLocalized();
        this.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "FeedDesk3.ico"));

        this.ExtendsContentIntoTitleBar = true;

        LoadSettings();

        // It's important to set content as early as here in order to set theme.
        // But make sure to call LoadSettings() before in order to apply settings value for contents.
        this.Content = App.GetService<ShellPage>();

        // It is necessary to set theme here after the content is set.
        if (this.Content is ShellPage root)
        {
            root.RequestedTheme = theme;

            // Don't do this. This right here interfear the color change in active state change.
            //SetCapitionButtonColorForWin11();

            // Call shelPage now that shellpage is careated.
            root.InitWhenMainWindowIsReady(this);
        }


        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 600;
            presenter.PreferredMinimumHeight = 780;
        }
    }

    public void SetCapitionButtonColorForWin11()
    {
        var currentTheme = ((FrameworkElement)Content).ActualTheme;
        if (currentTheme == ElementTheme.Dark)
        {
            this.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
            this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.White;
        }
        else if (currentTheme == ElementTheme.Light)
        {
            this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
            this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Black;
        }
        else
        {
            if (App.Current.RequestedTheme == ApplicationTheme.Dark)
            {
                this.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
                this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.White;
            }
            else
            {
                this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Black;
            }
        }
    }

    private void LoadSettings()
    {
        var filePath = App.AppConfigFilePath;
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, App.AppName + ".config");
        }

        var vm = App.GetService<MainViewModel>();

        if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            vm.IsAcrylicSupported = true;

            vm.IsBackdropEnabled = true;
        }
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            vm.IsMicaSupported = true;

            vm.IsBackdropEnabled = true;
        }

        if (!System.IO.File.Exists(filePath)) 
        {
            // Sets default.

            if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
                vm.Material = SystemBackdropOption.Acrylic;

                vm.IsBackdropEnabled = true;
            }
            else if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop()
                {
                    Kind = MicaKind.Base
                };
                vm.Material = SystemBackdropOption.Mica;

                vm.IsBackdropEnabled = true;
            }

            theme = ElementTheme.Default;

            return;
        }

        OverlappedPresenterState? winState = null; // For AOT workaround, don't set default, = OverlappedPresenterState.Restored;

        ElementTheme eleThme = ElementTheme.Default;
        SystemBackdropOption bd = SystemBackdropOption.None;
        bool isFoundNewThemeSetting = false;

        double top = 100;
        double left = 100;
        double height = 768;
        double width = 1024;

        var xdoc = XDocument.Load(filePath);

        // Main window
        if (xdoc.Root != null)
        {
            // Main Window element
            var mainWindow = xdoc.Root.Element("MainWindow");
            if (mainWindow != null)
            {
                var hoge = mainWindow.Attribute("top");
                if (hoge != null)
                {
                    top = double.Parse(hoge.Value);
                }

                hoge = mainWindow.Attribute("left");
                if (hoge != null)
                {
                    left = double.Parse(hoge.Value);
                }

                hoge = mainWindow.Attribute("height");
                if (hoge != null)
                {
                    height = double.Parse(hoge.Value);
                }

                hoge = mainWindow.Attribute("width");
                if (hoge != null)
                {
                    width = double.Parse(hoge.Value);
                }

                hoge = mainWindow.Attribute("state");
                if (hoge != null)
                {
                    if (hoge.Value == "Maximized")
                    {
                        winState = OverlappedPresenterState.Maximized;
                    }
                    else if (hoge.Value == "Normal")
                    {
                        winState = OverlappedPresenterState.Restored;
                    }
                    else if (hoge.Value == "Minimized")
                    {
                        // Ignore minimized.
                        winState = OverlappedPresenterState.Restored;
                    }
                    else
                    {
                        winState = null;
                    }
                }

                var xLeftPane = mainWindow.Element("LeftPane");
                if (xLeftPane != null)
                {
                    if (xLeftPane.Attribute("width") != null)
                    {
                        var xvalue = xLeftPane.Attribute("width")?.Value;
                        if (!string.IsNullOrEmpty(xvalue))
                        {
                            var w = double.Parse(xvalue);
                            if (w > 256)
                            {
                                vm.WidthLeftPane = w;
                            }
                        }
                    }
                }

                /*
                var xDetailPane = mainWindow.Element("DetailPane");
                if (xDetailPane != null)
                {
                    if (xDetailPane.Attribute("width") != null)
                    {
                        var xvalue = xDetailPane.Attribute("width")?.Value;
                        if (!string.IsNullOrEmpty(xvalue))
                        {
                            var w = double.Parse(xvalue);
                            if (w > 256)
                            {
                                vm.WidthDetailPane = w;
                            }
                        }
                    }
                }
                */
                var xListViewPane = mainWindow.Element("ListViewPane");
                if (xListViewPane != null)
                {
                    if (xListViewPane.Attribute("width") != null)
                    {
                        var xvalue = xListViewPane.Attribute("width")?.Value;
                        if (!string.IsNullOrEmpty(xvalue))
                        {
                            var w = double.Parse(xvalue);
                            if (w > 256)
                            {
                                vm.WidthListViewPane = w;
                            }
                        }
                    }
                }
            }

            // Themes
            var opts = xdoc.Root.Element("Theme");
            if (opts != null)
            {
                var xvalue = opts.Attribute("current");
                if (xvalue != null)
                {
                    if (!string.IsNullOrEmpty(xvalue.Value))
                    {
                        if (Enum.TryParse(xvalue.Value, out ElementTheme cacheTheme))
                        {
                            eleThme = cacheTheme;
                            isFoundNewThemeSetting = true;
                        }
                    }
                }
                xvalue = opts.Attribute("backdrop");
                if (xvalue != null)
                {
                    if (!string.IsNullOrEmpty(xvalue.Value))
                    {
                        if (Enum.TryParse(xvalue.Value, out SystemBackdropOption cacheBackdrop))
                        {
                            bd = cacheBackdrop;
                            isFoundNewThemeSetting = true;
                        }
                    }
                }
            }

            // Options
            opts = xdoc.Root.Element("Opts");
            if (opts != null)
            {
                /*
                xvalue = opts.Attribute("IsDebugSaveLog");
                if (xvalue != null)
                {
                    if (!string.IsNullOrEmpty(xvalue.Value))
                    {
                        //MainViewModel.IsDebugSaveLog = xvalue.Value == "True";
                    }
                }
                */
            }
        }

        _winRestoreWidth = (int)width;
        _winRestoreHeight = (int)height;
        _winRestoreTop = (int)top;
        _winRestoreleft = (int)left;

        // Restore window size and position
        Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (appWindow != null)
        {
            // Window state
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                // AOT bad
                // https://github.com/microsoft/CsWinRT/issues/1930
            }

            if (winState is null)
            {
                // do nothing.
            }
            else
            {
                // Window state
                if (winState == OverlappedPresenterState.Maximized)
                {
                    // Sets restore size and position.
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(_winRestoreleft, _winRestoreTop, _winRestoreWidth, _winRestoreHeight));
                    // Maximize the window.
                    (appWindow.Presenter as OverlappedPresenter)!.Maximize();
                }
                else if (winState == OverlappedPresenterState.Minimized)
                {
                    // This should not happen, but just in case.
                    (appWindow.Presenter as OverlappedPresenter)!.Restore();
                    // Sets restore size and position.
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(_winRestoreleft, _winRestoreTop, _winRestoreWidth, _winRestoreHeight));
                }
                else
                {
                    // Sets restore size and position.
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(_winRestoreleft, _winRestoreTop, _winRestoreWidth, _winRestoreHeight));
                }
            }

        }

        // For the strictly backward compatibility reason, load preference from localsetting.
        if (RuntimeHelper.IsMSIX && (!isFoundNewThemeSetting))
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("AppSystemBackdropOption", out var obj))
            {
                if (obj is not null)
                {
                    if (obj is string s)
                    {
                        if (s == SystemBackdropOption.Acrylic.ToString())
                        {
                            bd = SystemBackdropOption.Acrylic;
                        }
                        else if (s == SystemBackdropOption.Mica.ToString())
                        {
                            bd = SystemBackdropOption.Mica;
                        }
                    }
                }
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("AppBackgroundRequestedTheme", out var obj2))
            {
                if (obj2 is not null)
                {
                    if (obj2 is string themeName)
                    {
                        if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
                        {
                            eleThme = cacheTheme;
                        }
                    }
                }
            }
        }

        // Apply theme and backdrop
        if (bd != SystemBackdropOption.None)
        {
            theme = eleThme;
            vm.Theme = eleThme;
        }
        vm.Material = bd;
        SwitchBackdrop(bd);
    }

    public void SwitchBackdrop(SystemBackdropOption backdrop)
    {
        var vm = App.GetService<MainViewModel>();

        if (backdrop == SystemBackdropOption.Mica)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop()
                {
                    Kind = MicaKind.Base
                };
                vm.Material = SystemBackdropOption.Mica;
                //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);

                vm.IsBackdropEnabled = true;
            }
        }
        else if (backdrop == SystemBackdropOption.MicaAlt)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop()
                {
                    Kind = MicaKind.BaseAlt
                };
                vm.Material = SystemBackdropOption.MicaAlt;
                //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);

                vm.IsBackdropEnabled = true;
            }
        }
        else if (backdrop == SystemBackdropOption.Acrylic)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();

                vm.Material = SystemBackdropOption.Acrylic;
                //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);

                vm.IsBackdropEnabled = true;
            }
        }
        else if (backdrop == SystemBackdropOption.None)
        {
            this.SystemBackdrop = null;

            vm.Material = SystemBackdropOption.None;
            vm.IsBackdropEnabled = false;
            vm.Theme = ElementTheme.Default;
            theme = ElementTheme.Default;
            if (this.Content is ShellPage root)
            {
                root.RequestedTheme = theme;

                //TitleBarHelper.UpdateTitleBar(theme, this);
                //SetCapitionButtonColorForWin11();
            }

            this.SystemBackdrop = null;
        }
    }

    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (appWindow != null)
        {
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                }
                else if (presenter.State == OverlappedPresenterState.Minimized)
                {
                }
                else
                {
                    _winRestoreHeight = (int)appWindow.Size.Height;
                    _winRestoreWidth = (int)appWindow.Size.Width;
                    _winRestoreTop = (int)appWindow.Position.Y;
                    _winRestoreleft = (int)appWindow.Position.X;
                }
            }
        }
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        var vm = App.GetService<MainViewModel>();

        // Save service tree.
        vm.SaveServiceXml();

        // Dispose httpclient.
        vm.CleanUp();

        SaveSettings();

        // Save err log.
        (App.Current as App)?.SaveErrorLog();
    }

    private void SaveSettings()
    {
        var vm = App.GetService<MainViewModel>();

        XmlDocument doc = new();
        var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
        doc.InsertBefore(xmlDeclaration, doc.DocumentElement);

        // Root Document Element
        var root = doc.CreateElement(string.Empty, "App", string.Empty);
        doc.AppendChild(root);

        //XmlAttribute attrs = doc.CreateAttribute("Version");
        //attrs.Value = _appVer;
        //root.SetAttributeNode(attrs);
        XmlAttribute attrs;

        // Main window
        if (App.MainWnd != null)
        {
            // Main Window element
            var mainWindow = doc.CreateElement(string.Empty, "MainWindow", string.Empty);

            OverlappedPresenterState? winState = null; // For AOT workaround, don't set default.  = OverlappedPresenterState.Restored;

            Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
            if (appWindow != null)
            {
                // AOT bad
                // https://github.com/microsoft/CsWinRT/issues/1930
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    if (presenter.State == OverlappedPresenterState.Maximized)
                    {
                        winState = OverlappedPresenterState.Maximized;
                    }
                    else if (presenter.State == OverlappedPresenterState.Minimized)
                    {
                        winState = OverlappedPresenterState.Restored;
                    }
                    else
                    {
                        winState = OverlappedPresenterState.Restored;
                    }
                }
                else
                {
                    // For AOT tmp workaround
                    OverlappedPresenter presenterAOT = this.AppWindow.Presenter.As<OverlappedPresenter>();

                    if (presenterAOT.State == OverlappedPresenterState.Maximized)
                    {
                        winState = OverlappedPresenterState.Maximized;
                    }
                    else if (presenterAOT.State == OverlappedPresenterState.Minimized)
                    {
                        winState = OverlappedPresenterState.Restored;
                    }
                    else
                    {
                        winState = OverlappedPresenterState.Restored;
                    }
                }
            }

            // Main Window attributes
            attrs = doc.CreateAttribute("width");
            if (winState == OverlappedPresenterState.Maximized)
            {
                attrs.Value = _winRestoreWidth.ToString();
            }
            else
            {
                attrs.Value = this.AppWindow.Size.Width.ToString();
            }
            //attrs.Value = this.AppWindow.Size.Width.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("height");
            if (winState == OverlappedPresenterState.Maximized)
            {
                attrs.Value = _winRestoreHeight.ToString();
            }
            else
            {
                attrs.Value = this.AppWindow.Size.Height.ToString();
            }
            //attrs.Value = App.MainWindow.AppWindow.Size.Height.ToString();//App.MainWindow.GetAppWindow().Size.Height.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("top");
            if (winState == OverlappedPresenterState.Maximized)
            {
                attrs.Value = _winRestoreTop.ToString();
            }
            else
            {
                attrs.Value = this.AppWindow.Position.Y.ToString();
            }
            //attrs.Value = App.MainWindow.AppWindow.Position.Y.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("left");
            if (winState == OverlappedPresenterState.Maximized)
            {
                attrs.Value = _winRestoreleft.ToString();
            }
            else
            {
                attrs.Value = this.AppWindow.Position.X.ToString();
            }
            //attrs.Value = App.MainWindow.AppWindow.Position.X.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("state");
            if (winState == OverlappedPresenterState.Maximized)
            {
                attrs.Value = "Maximized";
            }
            else if (winState == OverlappedPresenterState.Restored)
            {
                attrs.Value = "Normal";

            }
            else if (winState == OverlappedPresenterState.Minimized)
            {
                attrs.Value = "Minimized";
            }
            mainWindow.SetAttributeNode(attrs);

            // Layout
            //ListViewPaneColumnGridSplitter


            var xLeftPane = doc.CreateElement(string.Empty, "LeftPane", string.Empty);
            var xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = vm.WidthLeftPane.ToString();
            xLeftPane.SetAttributeNode(xAttrs);

            mainWindow.AppendChild(xLeftPane);

            /*
            var xDetailPane = doc.CreateElement(string.Empty, "DetailPane", string.Empty);
            xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = vm.WidthDetailPane.ToString();
            xDetailPane.SetAttributeNode(xAttrs);

            mainWindow.AppendChild(xDetailPane);
            */
            var xListViewPane = doc.CreateElement(string.Empty, "ListViewPane", string.Empty);
            xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = vm.WidthListViewPane.ToString();
            xListViewPane.SetAttributeNode(xAttrs);

            mainWindow.AppendChild(xListViewPane);

            // set Main Window element to root.
            root.AppendChild(mainWindow);

        }

        // Themes
        var xTheme = doc.CreateElement(string.Empty, "Theme", string.Empty);

        attrs = doc.CreateAttribute("current");
        attrs.Value = vm.Theme.ToString();
        xTheme.SetAttributeNode(attrs);

        attrs = doc.CreateAttribute("backdrop");
        attrs.Value = vm.Material.ToString();
        xTheme.SetAttributeNode(attrs);

        root.AppendChild(xTheme);

        // Options
        var xOpts = doc.CreateElement(string.Empty, "Opts", string.Empty);

        //attrs = doc.CreateAttribute("isChartTooltipVisible");
        //attrs.Value = MainViewModel.IsChartTooltipVisible.ToString();
        //xOpts.SetAttributeNode(attrs);

        //attrs = doc.CreateAttribute("isDebugSaveLog");
        //attrs.Value = MainViewModel.IsDebugSaveLog.ToString();
        //xOpts.SetAttributeNode(attrs);

        root.AppendChild(xOpts);


        var filePath = App.AppConfigFilePath;
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, App.AppName + ".config");
        }
        else
        {
            System.IO.Directory.CreateDirectory(App.AppDataFolder);
        }

        try
        {
            doc.Save(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("MainWindow_Closed: " + ex + " while saving : " + filePath);
        }

    }
}
