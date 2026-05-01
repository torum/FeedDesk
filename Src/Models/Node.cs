using CommunityToolkit.Mvvm.ComponentModel;

namespace FeedDesk.Models;

public abstract class Node : ObservableObject
{
    public string Name
    {
        get;
        set
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (field == value)
                return;

            field = value;

            OnPropertyChanged();
        }
    } = "";

    protected Node(){}

    protected Node(string name)
    {
        Name = name;
    }
}
