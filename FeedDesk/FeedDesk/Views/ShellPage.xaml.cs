using System.Xml;
using System.Xml.Linq;
using XmlClients.Core.Helpers;
using FeedDesk.Contracts.Services;
using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace FeedDesk.Views;

public sealed partial class ShellPage : Page
{

    public ShellViewModel ViewModel
    {
        get;
    }

    public MainViewModel MainViewModel
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        MainViewModel = App.GetService<MainViewModel>();

        InitializeComponent();

        AppTitleBarText.Text = "AppDisplayName".GetLocalized();

        ViewModel.NavigationService.Frame = NavigationFrame;
        //ViewModel.NavigationViewService.Initialize(NavigationViewControl);

        App.MainWindow.ExtendsContentIntoTitleBar = true;

        App.MainWindow.SetTitleBar(AppTitleBar);

        App.MainWindow.Activated += MainWindow_Activated;
        App.MainWindow.Closed += MainWindow_Closed;

        #region == Load settings ==

        // Ignore window size and position. Let WinEx do the Window resize. It handles save and restore perfectly including RestoreBound.

        double height = 640;
        double width = 480;

        var filePath = App.AppConfigFilePath;
        if (RuntimeHelper.IsMSIX)
        {
            filePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, App.AppName + ".config");
        }

        if (System.IO.File.Exists(filePath))
        {
            var xdoc = XDocument.Load(filePath);
            //Debug.WriteLine(xdoc.ToString());

            // Main window
            if (App.MainWindow != null && xdoc.Root != null)
            {
                // Main Window element
                var mainWindow = xdoc.Root.Element("MainWindow");
                if (mainWindow != null)
                {
                    /*
                    var hoge = mainWindow.Attribute("top");
                    if (hoge != null)
                    {
                        (sender as Window).Top = double.Parse(hoge.Value);
                    }
                    */
                    /*
                    hoge = mainWindow.Attribute("left");
                    if (hoge != null)
                    {
                        (sender as Window).Left = double.Parse(hoge.Value);
                    }
                    */
                    var hoge = mainWindow.Attribute("height");
                    if (hoge != null)
                    {
                        height = double.Parse(hoge.Value);
                    }

                    hoge = mainWindow.Attribute("width");
                    if (hoge != null)
                    {
                        width = double.Parse(hoge.Value);
                    }
                    /*
                    hoge = mainWindow.Attribute("state");
                    if (hoge != null)
                    {
                        if (hoge.Value == "Maximized")
                        {
                            (sender as Window).WindowState = WindowState.Maximized;
                        }
                        else if (hoge.Value == "Normal")
                        {
                            (sender as Window).WindowState = WindowState.Normal;
                        }
                        else if (hoge.Value == "Minimized")
                        {
                            (sender as Window).WindowState = WindowState.Normal;
                        }
                    }
                    */

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
                                    MainViewModel.WidthLeftPane = w;
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
                                    MainViewModel.WidthDetailPane = w;
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

        #endregion
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Needed to be here. (don't put this in constructor.. messes up when theme changed.)
        //TitleBarHelper.UpdateTitleBar(RequestedTheme, App.MainWindow);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        //ShellMenuBarSettingsButton.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ShellMenuBarSettingsButton_PointerPressed), true);
        //ShellMenuBarSettingsButton.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ShellMenuBarSettingsButton_PointerReleased), true);

        // give some time to let window draw itself.
        //await Task.Delay(100);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;

        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";

        AppTitleBarText.Foreground = (SolidColorBrush)App.Current.Resources[resource];
        AppTitleBarIcon.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
        AppMenuBar.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        //ShellMenuBarSettingsButton.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)ShellMenuBarSettingsButton_PointerPressed);
        //ShellMenuBarSettingsButton.RemoveHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)ShellMenuBarSettingsButton_PointerReleased);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var vm = App.GetService<MainViewModel>();

        // Save service tree.
        vm.SaveServiceXml();

        // Dispose httpclient.
        vm.CleanUp();

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
        if (App.MainWindow != null)
        {
            // Main Window element
            var mainWindow = doc.CreateElement(string.Empty, "MainWindow", string.Empty);

            // Main Window attributes
            attrs = doc.CreateAttribute("width");
            /*
            if (App.MainWindow.WindowState == WindowState.Maximized)
            {
                attrs.Value = //RestoreBounds.Width.ToString();
            }
            else
            {
                attrs.Value = App.MainWindow.AppWindow.Size.Width.ToString();
            }
            */
            attrs.Value = App.MainWindow.AppWindow.Size.Width.ToString();//App.MainWindow.GetAppWindow().Size.Width.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("height");
            /*
            if ((sender as Window).WindowState == WindowState.Maximized)
            {
                attrs.Value = (sender as Window).RestoreBounds.Height.ToString();
            }
            else
            {
                attrs.Value = (sender as Window).Height.ToString();
            }
            */
            attrs.Value = App.MainWindow.AppWindow.Size.Height.ToString();//App.MainWindow.GetAppWindow().Size.Height.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("top");
            /*
            if ((sender as Window).WindowState == WindowState.Maximized)
            {
                attrs.Value = (sender as Window).RestoreBounds.Top.ToString();
            }
            else
            {
                attrs.Value = (sender as Window).Top.ToString();
            }
            */
            attrs.Value = App.MainWindow.AppWindow.Position.Y.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("left");
            /*
            if ((sender as Window).WindowState == WindowState.Maximized)
            {
                attrs.Value = (sender as Window).RestoreBounds.Left.ToString();
            }
            else
            {
                attrs.Value = (sender as Window).Left.ToString();
            }
            */
            attrs.Value = App.MainWindow.AppWindow.Position.X.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("state");
            if (App.MainWindow.WindowState == WindowState.Maximized)
            {
                attrs.Value = "Maximized";
            }
            else if (App.MainWindow.WindowState == WindowState.Normal)
            {
                attrs.Value = "Normal";

            }
            else if (App.MainWindow.WindowState == WindowState.Minimized)
            {
                attrs.Value = "Minimized";
            }
            mainWindow.SetAttributeNode(attrs);


            var xLeftPane = doc.CreateElement(string.Empty, "LeftPane", string.Empty);
            var xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = MainViewModel.WidthLeftPane.ToString();
            xLeftPane.SetAttributeNode(xAttrs);

            mainWindow.AppendChild(xLeftPane);

            var xDetailPane = doc.CreateElement(string.Empty, "DetailPane", string.Empty);
            xAttrs = doc.CreateAttribute("width");
            xAttrs.Value = MainViewModel.WidthDetailPane.ToString();
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

        // Save err log.
        (App.Current as App)?.SaveErrorLog();
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }
    /*

    private void ShellMenuBarSettingsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "PointerOver");
    }

    private void ShellMenuBarSettingsButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "Pressed");
    }

    private void ShellMenuBarSettingsButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "Normal");
    }

    private void ShellMenuBarSettingsButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "Normal");
    }
    */


    private void NavigationViewControl_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked == true)
        {
            // pass mainviewmodel for setting page.
            //NavigationFrame.Navigate(typeof(SettingsPage), MainVM, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
            NavigationFrame.Navigate(typeof(SettingsPage), null, args.RecommendedNavigationTransitionInfo);
        }
        else if (args.InvokedItemContainer != null && (args.InvokedItemContainer?.Tag != null))
        {
            /*
            if (_pages is null)
                return;

            var item = _pages.FirstOrDefault(p => p.Tag.Equals(args.InvokedItemContainer.Tag.ToString()));

            var _page = item.Page;

            if (_page is null)
                return;


            NavigationFrame.Navigate(_page, vm, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
            //NavigationFrame.Navigate(_page, vm, args.RecommendedNavigationTransitionInfo);
            //NavigationFrame.Navigate(_page, vm, new SuppressNavigationTransitionInfo());
            */
        }
    }

    /*
    private void NavigationFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (NavigationFrame.SourcePageType == typeof(SettingsPage))
        {
            // SettingsItem is not part of NavView.MenuItems, and doesn't have a Tag.
            //NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.SettingsItem;
            //NavigationViewControl.Header = "設定";
            return;
        }
        else if (NavigationFrame.SourcePageType != null)
        {
            //NavigationViewControl.Header = null;

            //var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

            //NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First(n => n.Tag.Equals(item.Tag));

            //NavigationViewControl.Header = ((NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();

            // Do nothing.
        }
    }
    */

    private void NavigationFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
    
    }
}
