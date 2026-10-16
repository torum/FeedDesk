
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Collections.Generic;

namespace FeedDesk.Models;

public class Configuration
{
    public class WindowSettings
    {
        public int Width { get; set; } = 780;
        public int Height { get; set; } = 600;
        public int Left { get; set; } = 100;
        public int Top { get; set; } = 100;
        public OverlappedPresenterState WindowState { get; set; } = OverlappedPresenterState.Restored;

        public int RestoreWidth { get; set; } = 780;
        public int RestoreHeight { get; set; } = 600;
        public int RestoreLeft { get; set; } = 100;
        public int RestoreTop { get; set; } = 100;
    }

    public WindowSettings MainWindow { get; set; } = new WindowSettings();

    public double WidthLeftPane { get; set; } = 256;

    public double WidthListViewPane { get; set; } = 300;

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    public SystemBackdropOption SystemBackdrop { get; set; } = SystemBackdropOption.Mica;

    //public bool IsForceSetVolumeExplicitly { get; set; }

}
