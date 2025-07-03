using XmlClients.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DataPackageOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation;

namespace FeedDesk.Views;

class MyTreeViewItem : TreeViewItem
{
    protected override void OnDragEnter(DragEventArgs e)
    {
        if (MainPage.DraggedItems.Count <= 0)
        {
            e.Handled = true;
            base.OnDragEnter(e);
            return;
        }

        var draggedItem = MainPage.DraggedItems[0];
        var draggedOverItem = DataContext as NodeTree;

        if ((draggedItem is NodeFolder) && (draggedOverItem is NodeFolder) || (draggedItem is NodeFeed) && (draggedOverItem is NodeFolder))
        {
            //e.Handled = true;
        }

        base.OnDragEnter(e);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        if (MainPage.DraggedItems.Count <= 0)
        {
            e.Handled = true;
            base.OnDragOver(e);
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var draggedItem = MainPage.DraggedItems[0];
        var draggedOverItem = DataContext as NodeTree;
        /*
        if (draggedOverItem != null)
            if (draggedOverItem.Parent != null)
                Debug.WriteLine(draggedOverItem.Parent.ToString());
        */
        /*
        if (draggedOverItem?.Parent is ServiceTreeBuilder)
        {
            // Accept

            base.OnDragOver(e);
            e.AcceptedOperation = DataPackageOperation.Move;

            return;
        }
        */
        //

        // only allow a feed move into a folder
        if ((draggedItem is NodeFeed) && (draggedOverItem is NodeFolder))
        {
            // same place is meaningless
            if (draggedItem.Parent == draggedOverItem)
            {
                // Deny

                e.Handled = true;
                base.OnDragOver(e);
                e.AcceptedOperation = DataPackageOperation.None;
            }
            else
            {
                // Accept

                base.OnDragOver(e);
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }
        else if ((draggedItem is NodeFolder) && (draggedOverItem is NodeFolder))
        {
            // same place is meaningless
            if (draggedItem.Parent == draggedOverItem)
            {
                // Deny

                e.Handled = true;
                base.OnDragOver(e);
                e.AcceptedOperation = DataPackageOperation.None;
            }
            else
            {
                // Accept

                base.OnDragOver(e);
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }
        else
        {
            // Deny

            e.Handled = true;
            base.OnDragOver(e);
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }
    protected override void OnDrop(DragEventArgs e)
    {
        if (MainPage.DraggedItems.Count <= 0)
        {
            e.Handled = true; 
            e.AcceptedOperation = DataPackageOperation.None;
            base.OnDrop(e);
            return;
        }

        var draggedItem = MainPage.DraggedItems[0];
        var draggedOverItem = DataContext as NodeTree;

        if ((draggedItem is NodeFeed) && (draggedOverItem is NodeFolder))
        {
            // ok
        }
        else if ((draggedItem is NodeFolder) && (draggedOverItem is NodeFolder)) 
        { 
            // OK
        }
        else
        {
            // no
            e.Handled = true;
        }

        base.OnDrop(e);
    }
}