﻿using XmlClients.Core.Helpers;
using XmlClients.Core.Models;
using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.Storage;

namespace FeedDesk.Views;

public sealed partial class MainPage : Page
{
    private WaitDialog? dialog;

    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();

        try
        {
            InitializeComponent();
        }
        catch (XamlParseException parseException)
        {
            Debug.WriteLine($"Unhandled XamlParseException in FeedsPage: {parseException.Message}");
            foreach (var key in parseException.Data.Keys)
            {
                Debug.WriteLine("{Key}:{Value}", key.ToString(), parseException.Data[key]?.ToString());
            }
            throw;
        }

        //
        ViewModel.ShowWaitDialog += (sender, arg) => { OnShowWaitDialog(arg); };

        //ViewModel.DebugOutput += (sender, arg) => { OnDebugOutput(arg); };
        //ViewModel.DebugClear += () => OnDebugClear();

        // Sets gridsplitter left.
        LeftPaneGridColumn.Width = new GridLength(ViewModel.WidthLeftPane, GridUnitType.Pixel);
        // DetailPane gridsplitter left
        col2.Width = new GridLength(ViewModel.WidthDetailPane, GridUnitType.Pixel);
        col1.Width = new GridLength(1.0, GridUnitType.Star);
    }

    public async void OnShowWaitDialog(bool isShow)
    {
        if (isShow)
        {
            if (dialog == null)
            {
                dialog = new WaitDialog();
                var ring = new ProgressRing
                {
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                    VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
                    IsActive = true,
                };
                dialog.Content = ring;
                dialog.XamlRoot = XamlRoot;
                dialog.RequestedTheme = ActualTheme;
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                dialog.Title = "WaitDialog_Title".GetLocalized();
            }
            
            await dialog.ShowAsync();
        }
        else
        {
            if (dialog == null)
            {
                return;
            }

            dialog.Hide();
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

    private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        /*
        if (args.InvokedItem is NodeTree nt)
        {
            if (nt.Children.Count > 0)
                nt.IsExpanded = !nt.IsExpanded;
        }
        */
        /*
        if (args.InvokedItem is NodeTree nt)
        {
            if (nt.Children.Count > 0)
            {
                if (!nt.IsExpanded)
                    nt.IsExpanded = true;
            }
        }
        */
    }

    private void TreeViewItem_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is TreeViewItem tvi)
        {
            if (tvi.IsSelected)
                tvi.IsExpanded = !tvi.IsExpanded;
        }
    }

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

    private void ListViewEntryItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is FeedEntryItem item)
            {
                if (ListViewEntryItem.SelectedItem != item)
                {
                    ListViewEntryItem.SelectedItem = item;
                }
            }
        }
    }

    public static List<NodeTree> DraggedItems
    {
        get; set;
    }=
    new List<NodeTree>();

    private void TreeView_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
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
        foreach (NodeTree item in args.Items)
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
        ViewModel.WidthLeftPane = LeftPaneGridColumn.ActualWidth;
    }

    private void ListViewPane_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.WidthDetailPane = col2.ActualWidth;
    }

    private void ListViewEntryItem_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //
        DetailsPaneScrollViewer.ChangeView(0,0,1);
    }
}
