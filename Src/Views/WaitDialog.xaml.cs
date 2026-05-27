using Microsoft.UI.Xaml.Controls;

namespace FeedDesk.Views;

public sealed partial class WaitDialog : ContentDialog
{
    public bool IsShowing { get; set; }

    public WaitDialog()
    {
        InitializeComponent();
    }
}
