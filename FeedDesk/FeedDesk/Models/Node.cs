using FeedDesk.Common;

namespace FeedDesk.Models;

public abstract class Node : ViewModelBase
{
    private string _name ="";

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (_name == value)
                return;

            _name = value;

            NotifyPropertyChanged(nameof(Name));
        }
    }

    protected Node(){}

    protected Node(string name)
    {
        Name = name;
    }
}
