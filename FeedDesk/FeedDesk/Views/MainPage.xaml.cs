using CommunityToolkit.WinUI;
using FeedDesk.Models;
using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace FeedDesk.Views;

public sealed partial class MainPage : Page
{
    private WaitDialog? _dialog;

    public MainViewModel ViewModel
    {
        get;
    }

    public static List<NodeTree> DraggedItems
    {
        get; set;
    } = [];

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();

        InitializeComponent();

        //
        ViewModel.ShowWaitDialog += (sender, arg) => { OnShowWaitDialog(arg); };

        //ViewModel.DebugOutput += (sender, arg) => { OnDebugOutput(arg); };
        //ViewModel.DebugClear += () => OnDebugClear();

        // TreeView pane gridsplitter left.
        this.LeftPaneGridColumn.Width = new GridLength(ViewModel.WidthLeftPane, GridUnitType.Pixel);

        // DetailPane gridsplitter left
        // TODO:
        //col1.Width = new GridLength(1.0, GridUnitType.Star);
        //col2.Width = new GridLength(ViewModel.WidthDetailPane, GridUnitType.Pixel);
        col1.Width = new GridLength(ViewModel.WidthListViewPane, GridUnitType.Pixel);
        col2.Width = new GridLength(1.0, GridUnitType.Star);
    }

    public async void OnShowWaitDialog(bool isShow)
    {
        if (isShow)
        {
            if (_dialog == null)
            {
                _dialog = new WaitDialog();
                var ring = new ProgressRing
                {
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                    VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
                    IsActive = true,
                };
                _dialog.Content = ring;
                _dialog.XamlRoot = XamlRoot;
                _dialog.RequestedTheme = ActualTheme;
                _dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                _dialog.Title = "WaitDialog_Title".GetLocalized();
            }
            
            await _dialog.ShowAsync();
        }
        else
        {
            if (_dialog == null)
            {
                return;
            }

            _dialog.Hide();
        }
    }

    public void OnDebugOutput(string arg)
    {
        // AppendText() is much faster than data binding.
        /*
        DebugTextBox.AppendText(arg);
        DebugTextBox.CaretIndex = DebugTextBox.Text.Length;
        DebugTextBox.ScrollToEnd();
        */

        DebugTextBox.Text = arg;
    }

    public void OnDebugClear()
    {
        DebugTextBox.Text = string.Empty;    
    }

    private void TreeViewItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is TreeViewItem tvi)
        {
            if (tvi.IsSelected)
                tvi.IsExpanded = !tvi.IsExpanded;
        }
    }

    /*
        private async void ListViewEntryItem_DoubleTappedAsync(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {

        if (ListViewEntryItem.SelectedItem is EntryItem item)
        {
            if (item != null)
            {
                if (item.AltHtmlUri != null)
                {
                    await Windows.System.Launcher.LaunchUriAsync(item.AltHtmlUri);
                }
            }
        }
    }
    */

    private void ListViewEntryItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        // right click select.
        if (e.OriginalSource is not FrameworkElement element)
        {
            return;
        }
        if (element.DataContext is FeedEntryItem item)
        {
            if (ListViewEntryItem.SelectedItem != item)
            {
                ListViewEntryItem.SelectedItem = item;
            }
        }
;
    }

    private void TreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        if (ViewModel.IsTreeWorking)
        {
            args.Cancel = true;
            return;
        }

        if (args.Items.Count > 0)
        {

            if (args.Items[0] is not NodeTree nt)
            {
                args.Cancel = true;
                return;
            }

            // only allow feed and folders
            if ((nt is NodeFeed) || (nt is NodeFolder))
            {
                //Debug.WriteLine("TreeView_DragItemsStarting: " + nt.Name);

                if ((nt.IsBusy) || (nt.IsBusyChildrenCount > 0) || (ViewModel.Root.IsBusyChildrenCount > 0))
                {
                    args.Cancel = true;
                    return;
                }

                if (nt.Parent != null)
                {
                    if (nt.Parent is NodeFolder originalFolder)
                    {
                        if (originalFolder.EntryNewCount >= nt.EntryNewCount)
                        {
                            originalFolder.EntryNewCount -= nt.EntryNewCount;
                        }
                    }
                }

                DraggedItems.Add(nt);
            }
            else
            {
                args.Cancel = true;
                return;
            }
        }

        /*
        foreach (NodeTree item in args.Items)
        {
            DraggedItems.Add(item);
            
            break;
        }
        */
    }

    private void TreeView_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        foreach (NodeTree item in args.Items.Cast<NodeTree>())
        {

            /*
            if (item.Parent != null)
            {
                if (item.Parent is NodeFolder originalFolder)
                {
                    if (originalFolder.EntryNewCount >= item.EntryNewCount)
                    {
                        originalFolder.EntryNewCount -= item.EntryNewCount;
                    }
                }
            }
            */

            if (args.NewParentItem is NodeFolder newFolder)
            {
                item.Parent = newFolder;

                newFolder.EntryNewCount += item.EntryNewCount;
            }
            else if (args.NewParentItem is FeedTreeBuilder newRoot)
            {
                // This won't be called.
                item.Parent = newRoot;
            }
            else if (args.NewParentItem is null)
            {
                item.Parent = ViewModel.Root;
            }

            break;
        }

        DraggedItems.Clear();

        ViewModel.SaveServiceXml();
    }

    private void TreeViewItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            item.IsSelected = true;
        }
    }

    private void LeftPane_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // save the width when closed.
        ViewModel.WidthLeftPane = LeftPaneGridColumn.ActualWidth;
    }

    private void ListViewPane_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // save the width when closed.
        //ViewModel.WidthDetailPane = col2.ActualWidth;
        ViewModel.WidthListViewPane = col1.ActualWidth;
    }

    private void ListViewEntryItem_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // reset scrollviewer pos.
        DetailsPaneScrollViewer.ChangeView(0, 0, 1);
    }

    private async void ListViewEntryItem_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        if (e.OriginalSource is not Microsoft.UI.Xaml.Controls.ListViewItem)
        {
            return;
        }

        Windows.System.VirtualKey releasedKey = e.OriginalKey;

        if (releasedKey != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        if (listView.SelectedItem is EntryItem item)
        {
            if (item != null)
            {
                if (item.AltHtmlUri != null)
                {
                    await Windows.System.Launcher.LaunchUriAsync(item.AltHtmlUri);
                }
            }
        }
    }

    private async void ListViewEntryItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        //ListView listView = (ListView)sender;
        if (sender is not ListView listView)
        {
            return;
        }

        // UI element that was right-clicked
        FrameworkElement element = (FrameworkElement)e.OriginalSource;

        var container = FindParent<ListViewItem>(element);

        if (container is null)
        {
            return;
        }

        if (listView.SelectedItem != container.Content)
        {
            return;
        }

        if (listView.SelectedItem is EntryItem item)
        {
            if (item != null)
            {
                if (item.AltHtmlUri != null)
                {
                    await Windows.System.Launcher.LaunchUriAsync(item.AltHtmlUri);
                }
            }
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        if (parent is not null)
        {
            return parent as T;
        }
        else
        {
            return null;
        }
    }

    private void DetailsPane_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (col2.ActualWidth >= 900)
        {
            DetailsContentStackPanel.Width = 900;
            DetailsContentStackPanel.MaxWidth = 900;
            DetailsContentStackPanel.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            DetailsContentStackPanel.Width = double.NaN;
            DetailsContentStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }
}
