using System.Xml;
using System.Xml.Linq;
using FeedDesk.ViewModels;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.UI.ViewManagement;
using FeedDesk.Helpers;
using System.Diagnostics;
using System.IO;
using System;

namespace FeedDesk;

public partial class MainWindow : Window
{
    // Window position and size
    // TODO: Change this lator.1920x1080
    private int winRestoreWidth = 1024;//1024;
    private int winRestoreHeight = 768;//768;
    private int winRestoreTop = 100;
    private int winRestoreleft = 100;

    private readonly UISettings settings;

    public MainWindow()
    {
        InitializeComponent();

        this.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "FeedDesk3.ico"));
        //Content = null;
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

        LoadSettings();
    }

    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        var frame = App.AppTitlebar as FrameworkElement;

        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        App.CurrentDispatcherQueue.TryEnqueue(() =>
        {
            TitleBarHelper.ApplySystemThemeToCaptionButtons(frame, this);
        });
    }

    private void LoadSettings()
    {
        var vm = App.GetService<MainViewModel>();

        var winState = OverlappedPresenterState.Restored;

        #region == Load settings ==

        // Ignore window size and position. Let WinEx do the Window resize. It handles save and restore perfectly including RestoreBound.

        double top = 100;
        double left = 100;
        double height = 768;
        double width = 1024;

        var filePath = App.AppConfigFilePath;
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, App.AppName + ".config");
        }

        if (System.IO.File.Exists(filePath))
        {
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
                            //(sender as Window).WindowState = WindowState.Maximized;
                            winState = OverlappedPresenterState.Maximized;
                        }
                        else if (hoge.Value == "Normal")
                        {
                            //(sender as Window).WindowState = WindowState.Normal;
                            winState = OverlappedPresenterState.Restored;
                        }
                        else if (hoge.Value == "Minimized")
                        {
                            //(sender as Window).WindowState = WindowState.Normal;
                            winState = OverlappedPresenterState.Restored;
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
                }

                // Options
                var opts = xdoc.Root.Element("Opts");
                if (opts != null)
                {
                    /*
                    var xvalue = opts.Attribute("IsChartTooltipVisible");
                    if (xvalue != null)
                    {
                        if (!string.IsNullOrEmpty(xvalue.Value))
                        {
                            //MainViewModel.IsChartTooltipVisible = xvalue.Value == "True";
                        }
                    }

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
        }


        winRestoreWidth = (int)width;
        winRestoreHeight = (int)height;
        winRestoreTop = (int)top;
        winRestoreleft = (int)left;

        // Restore window size and position
        Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (appWindow != null)
        {
            // Window state
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (winState == OverlappedPresenterState.Maximized)
                {
                    // Sets restore size and position.
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(winRestoreleft, winRestoreTop, winRestoreWidth, winRestoreHeight));
                    // Maximize the window.
                    presenter.Maximize();
                    /*
                    // TODO: TEMP
                    appWindow.Move(new Windows.Graphics.PointInt32(winRestoreleft, winRestoreTop));
                    appWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
                    */
                }
                else if (winState == OverlappedPresenterState.Minimized)
                {
                    // This should not happen, but just in case.
                    presenter.Restore();
                    // Sets restore size and position.
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(winRestoreleft, winRestoreTop, winRestoreWidth, winRestoreHeight));
                }
                else
                {
                    // Sets restore size and position.
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(winRestoreleft, winRestoreTop, winRestoreWidth, winRestoreHeight));
                }
            }

            //
            appWindow.Closing += (s, a) =>
            {
                // TODO: Currently, WinUI3 does not have "App.Current?.Windows". So, we cannot loop through all windows.
            };
        }

        #endregion

    }

    private void WindowEx_SizeChanged(object sender, WindowSizeChangedEventArgs args)
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
                    winRestoreHeight = (int)appWindow.Size.Height;
                    winRestoreWidth = (int)appWindow.Size.Width;
                    winRestoreTop = (int)appWindow.Position.Y;
                    winRestoreleft = (int)appWindow.Position.X;
                }
            }
        }
    }

    private void WindowEx_Closed(object sender, WindowEventArgs args)
    {
        var vm = App.GetService<MainViewModel>();

        #region == Save setting ==

        // Ignore window size and position. Let WinEx do the Window resize. It handles save and restore perfectly including RestoreBound.

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

            var winState = OverlappedPresenterState.Restored;
            Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
            if (appWindow != null)
            {
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
            }

            // Main Window attributes
            attrs = doc.CreateAttribute("width");
            if (winState == OverlappedPresenterState.Maximized)
            {
                attrs.Value = winRestoreWidth.ToString();
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
                attrs.Value = winRestoreHeight.ToString();
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
                attrs.Value = winRestoreTop.ToString();
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
                attrs.Value = winRestoreleft.ToString();
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


            var xLeftPane = doc.CreateElement(string.Empty, "LeftPane", string.Empty);
            var xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = vm.WidthLeftPane.ToString();
            xLeftPane.SetAttributeNode(xAttrs);

            mainWindow.AppendChild(xLeftPane);

            var xDetailPane = doc.CreateElement(string.Empty, "DetailPane", string.Empty);
            xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = vm.WidthDetailPane.ToString();
            xDetailPane.SetAttributeNode(xAttrs);

            mainWindow.AppendChild(xDetailPane);

            // set Main Window element to root.
            root.AppendChild(mainWindow);

        }

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

        try
        {
            doc.Save(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("MainWindow_Closed: " + ex + " while saving : " + filePath);
        }

        #endregion
    }
}
