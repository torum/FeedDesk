using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

public sealed partial class WaitDialog : ContentDialog
{
    public WaitDialog()
    {
        InitializeComponent();
    }

    public bool IsShowing { get; set; }

}
