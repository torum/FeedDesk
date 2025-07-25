using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FeedDesk.Models.Clients;

public abstract class BaseClient : IDisposable
{
    private static readonly Lock _locker = new();
    private static volatile HttpClient? _client;

    protected static HttpClient Client
    {
        get
        {
            if (_client == null)
            {
                lock (_locker)
                {
                    _client ??= new HttpClient();
                }
            }

            return _client;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();

            _client = null;
        }
    }

    public abstract Task<HttpClientEntryItemCollectionResultWrapper> GetEntries(Uri entriesUrl, string feedId);

    #region == Events ==

    public delegate void ClientDebugOutput(BaseClient sender, string data);

    public event ClientDebugOutput? DebugOutput;

    #endregion

    // Writes to Debug (raises event)
    protected void ToDebugWindow(string data)
    {
        var nowait = Task.Run(() => { DebugOutput?.Invoke(this, data); });
    }

    private async Task<byte[]> GetImage(Uri imageUri)
    {
        var res = Array.Empty<byte>();

        try
        {
            if (_client != null)
            {
                res = await _client.GetByteArrayAsync(imageUri);
            }
        }
        catch (Exception e)
        {
            if (e.InnerException != null)
            {
                Debug.WriteLine(e.InnerException.Message + " @BaseClient::GetImage:GetByteArrayAsync");

                ToDebugWindow("<< " + e.InnerException.ToString() + " @BaseClient::GetImage:GetByteArrayAsync: " + e.InnerException.Message + Environment.NewLine + imageUri.AbsoluteUri + Environment.NewLine);
            }
            else
            {
                Debug.WriteLine(e.Message + " @BaseClient::GetImage:GetByteArrayAsync");

                ToDebugWindow("<< " + e.ToString() + " @BaseClient::GetImage:GetByteArrayAsync: " + e.Message + Environment.NewLine + imageUri.AbsoluteUri + Environment.NewLine);
            }
        }

        return res;
    }

    #region == Fill ErrorObject ==

    protected static ErrorObject InvalidUriScheme(ErrorObject err, string scheme, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.HTTP;
        err.ErrText = "URI scheme should be http or https : " + scheme;
        err.ErrDescription = "Invalid URI scheme.";
        err.ErrPlace = "";
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;
        return err;
    }

    protected static ErrorObject InvalidContentType(ErrorObject err, string errText, string errPlace, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.HTTP;
        err.ErrText = errText;
        err.ErrDescription = "Invalid Content-Type returned.";
        err.ErrPlace = errPlace;
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    protected static ErrorObject InvalidXml(ErrorObject err, string eMessage, string errPlace, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.XML;
        err.ErrText = eMessage;
        err.ErrDescription = "Invalid XML document returned.";
        err.ErrPlace = errPlace;
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    protected static ErrorObject FormatUndetermined(ErrorObject err, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.API;
        err.ErrText = "Unknown format";
        err.ErrDescription = "Document parse failed.";
        err.ErrPlace = "xdoc.DocumentElement.LocalName/NamespaceURI";
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    protected static ErrorObject NonSuccessStatusCode(ErrorObject err, string statusCode, string errPlace, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.HTTP;
        err.ErrText = statusCode;
        err.ErrDescription = "HTTP request failed.";
        err.ErrPlace = errPlace;
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    protected static ErrorObject HttpReqException(ErrorObject err, string eMessage, string errPlace, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.HTTP;
        err.ErrText = eMessage;
        err.ErrDescription = "HTTP request error (HttpRequestException).";
        err.ErrPlace = errPlace;
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    protected static ErrorObject HttpTimeoutException(ErrorObject err, string errText, string errPlace, string errPlaceParent)
    {
        err.ErrCode = "";
        err.ErrType = ErrorObject.ErrTypes.HTTP; ;
        err.ErrText = errText;
        err.ErrDescription = "HTTP request error (Timeout).";
        err.ErrPlace = errPlace;
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    protected static ErrorObject GenericException(ErrorObject err, string errCode, ErrorObject.ErrTypes errType, string errText, string errDescription, string errPlace, string errPlaceParent)
    {
        err.ErrCode = errCode;
        err.ErrType = errType;
        err.ErrText = errText;
        err.ErrDescription = errDescription;
        err.ErrPlace = errPlace;
        err.ErrPlaceParent = errPlaceParent;
        err.ErrDatetime = DateTime.Now;

        return err;
    }

    #endregion

}


