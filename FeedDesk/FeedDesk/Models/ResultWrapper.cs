using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;

namespace FeedDesk.Models;

// ErrorInfo Class
public class ErrorObject
{
    public enum ErrTypes
    {
        DB, API, HTTP, XML, Other
    };

    // ErrTypes
    public ErrTypes ErrType { get; set; }

    // HTTP error code?
    public string ErrCode { get; set; } 

    // eg Error title, or type of Exception, .
    public string ErrDescription { get; set; }

    // Raw exception error messages, API error text translated via dictionary.
    public string ErrText { get; set;}

    // eg method name, or PATH info for REST
    public string ErrPlace { get; set; }

    // class name or site address 
    public string ErrPlaceParent { get; set; }  

    //
    public DateTime ErrDatetime { get; set; }

    public ErrorObject()
    {
        ErrType = ErrTypes.Other;
        ErrCode = "";
        ErrDescription = "";
        ErrText = "";
        ErrPlace = "";
        ErrPlaceParent = "";
        ErrDatetime = default;
    }
}

// Result Wrapper Class
public abstract class ResultWrapper
{
    public ErrorObject Error = new();
    public bool IsError = false;
}

public class SqliteDataAccessResultWrapper: ResultWrapper
{
    public int AffectedCount = 0;
}

public class SqliteDataAccessInsertResultWrapper: SqliteDataAccessResultWrapper
{
    public List<EntryItem> InsertedEntries = [];
}

public class SqliteDataAccessSelectResultWrapper: SqliteDataAccessResultWrapper
{
    public int UnreadCount = 0;

    public List<EntryItem> SelectedEntries = [];
}

/*
public class SqliteDataAccessSelectImageResultWrapper : SqliteDataAccessResultWrapper
{
    public BitmapImage Image;
}
*/

public class HttpClientEntryItemCollectionResultWrapper : ResultWrapper
{
    // Atom //feed/title
    // RSS2.0 //rss/channel/title
    public string Title
    {
        get; set;
    }

    // Atom //feed/subtitle
    // RSS2.0  //rss/channel/description
    public string Description
    {
        get; set;
    }

    // RSS2.0 pubDate
    // in UTC
    public DateTime Published
    {
        get; set;
    }

    // RSS2.0 lastBuildDate
    public DateTime Updated
    {
        get; set;
    }

    // Website uri
    public Uri? HtmlUri
    {
        get; set;
    }
    
    // category (s)

    public List<EntryItem> Entries = [];

    public HttpClientEntryItemCollectionResultWrapper()
    {
        Title = string.Empty;
        Description = string.Empty;
        Published = default;
        Updated = default;
    }
}
