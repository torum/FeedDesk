using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace XmlClients.Core.Common;

public abstract class ViewModelBase : INotifyPropertyChanged//, IDataErrorInfo
{
    public ViewModelBase() { }

    #region == INotifyPropertyChanged ==

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged(string propertyName)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /*
        var uithread = App.CurrentDispatcherQueue?.HasThreadAccess;

        if (uithread != null)
        {
            if (uithread == true)
            {
                DoNotifyPropertyChanged(propertyName);
            }
            else
            {
                App.CurrentDispatcherQueue?.TryEnqueue(() =>
                {
                    DoNotifyPropertyChanged(propertyName);
                });
            }
        }
        */
    }

    private void DoNotifyPropertyChanged(string propertyName)
    {
        try
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception at NotifyPropertyChanged ({propertyName}): " + ex.Message);

            //(App.Current as App).AppendErrorLog($"Exception at NotifyPropertyChanged ({propertyName}): ", ex.Message);
        }
    }

    #endregion
    
    /*
    #region == IDataErrorInfo ==

    private Dictionary<string, string> _ErrorMessages = new Dictionary<string, string>();

    string IDataErrorInfo.Error
    {
        get
        {
            return (_ErrorMessages.Count > 0) ? "Has Error" : null;
        }
    }

    string IDataErrorInfo.this[string columnName]
    {
        get
        {
            if (_ErrorMessages.ContainsKey(columnName))
                return _ErrorMessages[columnName];
            else
                return "";
        }
    }

    protected void SetError(string propertyName, string errorMessage)
    {
        _ErrorMessages[propertyName] = errorMessage;
    }

    protected void ClearErrror(string propertyName)
    {
        if (_ErrorMessages.ContainsKey(propertyName))
            //_ErrorMessages.Remove(propertyName);
            _ErrorMessages[propertyName] = "";
    }


    #endregion
    */
}
