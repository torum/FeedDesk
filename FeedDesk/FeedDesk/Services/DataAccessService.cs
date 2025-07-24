using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using FeedDesk.Services.Contracts;
using FeedDesk.Models;
using static FeedDesk.Models.FeedEntryItem;
using static FeedDesk.Services.FeedLink;
using System.Threading;
using System.Diagnostics;

namespace FeedDesk.Services;

public class DataAccessService : IDataAccessService
{
    private readonly SQLiteConnectionStringBuilder connectionStringBuilder = new();

    private readonly ReaderWriterLockSlim _readerWriterLock = new();

    public SqliteDataAccessResultWrapper InitializeDatabase(string dataBaseFilePath)
    {
        var res = new SqliteDataAccessResultWrapper();

        connectionStringBuilder.DataSource = dataBaseFilePath;
        connectionStringBuilder.ForeignKeys = true;
        connectionStringBuilder.Version = 3; // Default is 3.

        // Sync Mode
        connectionStringBuilder.SyncMode = SynchronizationModes.Normal;

        // Journal Mode
        connectionStringBuilder.JournalMode = SQLiteJournalModeEnum.Persist; // faster than Truncate?
        //connectionStringBuilder.JournalMode = SQLiteJournalModeEnum.Wal;// Testing WAL.

        using (var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString))
        {
            try
            {
                connection.Open();

                using var tableCmd = connection.CreateCommand();
                tableCmd.Transaction = connection.BeginTransaction();
                try
                {
                    tableCmd.CommandText = "CREATE TABLE IF NOT EXISTS feeds (" +
                        "feed_id TEXT NOT NULL PRIMARY KEY," +
                        "url TEXT NOT NULL," +
                        "name TEXT NOT NULL," +
                        "title TEXT," +
                        "description TEXT," +
                        "updated TEXT," +
                        "html_url TEXT" +
                        ")";
                    tableCmd.ExecuteNonQuery();

                    tableCmd.CommandText = "CREATE TABLE IF NOT EXISTS entries (" +
                        "entry_id TEXT NOT NULL PRIMARY KEY," +
                        "feed_id TEXT NOT NULL," +
                        "url TEXT NOT NULL," +
                        "title TEXT," +
                        "published TEXT NOT NULL," +
                        "updated TEXT NOT NULL," +
                        "author TEXT," +
                        "category TEXT," +
                        "summary TEXT," +
                        "content TEXT," +
                        "content_type TEXT," +
                        //"content_baseurl TEXT," +
                        "source TEXT," +
                        "source_url TEXT," +
                        //"image_id TEXT," +
                        "image_url TEXT," +
                        "audio_url TEXT," +
                        "comment_url TEXT," +
                        "status TEXT," +
                        "archived TEXT," +
                        "CONSTRAINT fk_feeds FOREIGN KEY (feed_id) REFERENCES feeds(feed_id) ON DELETE CASCADE" +
                        ")";
                    tableCmd.ExecuteNonQuery();
                    /*
                    tableCmd.CommandText = "CREATE TABLE IF NOT EXISTS images (" +
                        "image_id TEXT NOT NULL PRIMARY KEY," +
                        "entry_id TEXT NOT NULL," +
                        "image_url TEXT," +
                        //"image_downloaded TEXT," +
                        "image BLOB," +
                        "CONSTRAINT fk_entries FOREIGN KEY (entry_id) REFERENCES entries(entry_id) ON DELETE CASCADE" +
                        ")";
                    tableCmd.ExecuteNonQuery();
                    */

                    //tableCmd.CommandText = "ALTER TABLE entries ADD COLUMN category TEXT;";
                    //tableCmd.ExecuteNonQuery();

                    //
                    //tableCmd.CommandText = "drop trigger if exists trigger_delete_old_entries";
                    //tableCmd.ExecuteNonQuery();
                    /*
                    tableCmd.CommandText = "CREATE TRIGGER IF NOT EXISTS trigger_delete_old_entries AFTER INSERT ON entries";
                    tableCmd.CommandText += " BEGIN";
                    tableCmd.CommandText += " delete from entries where";
                    tableCmd.CommandText += " entry_id = (select min(entry_id) from entries)";
                    tableCmd.CommandText += " and (select count(*) from entries) > 1000;";
                    tableCmd.CommandText += " END;";
                    tableCmd.ExecuteNonQuery();
                    */
                    /*
                    tableCmd.CommandText = "CREATE TRIGGER IF NOT EXISTS trigger_delete_old_entries AFTER INSERT ON entries";
                    tableCmd.CommandText += " WHEN (SELECT COUNT(*) FROM entries) > 1000";
                    tableCmd.CommandText += " BEGIN";
                    tableCmd.CommandText += " DELETE FROM entries WHERE entry_id NOT IN (SELECT entry_id FROM entries ORDER BY published DESC LIMIT 1000);";
                    tableCmd.CommandText += " END;";
                    tableCmd.ExecuteNonQuery();
                    */
                    tableCmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    tableCmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDescription = "Exception while executing SQL queries";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::InitializeDatabase";

                    return res;
                }
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                res.IsError = true;
                res.Error.ErrType = ErrorObject.ErrTypes.DB;
                res.Error.ErrCode = "";
                res.Error.ErrText = ex.Message;
                res.Error.ErrDescription = "TargetInvocationException while connecting to a SQL database file"; 
                res.Error.ErrDatetime = DateTime.Now;
                res.Error.ErrPlace = "connection.Open";
                res.Error.ErrPlaceParent = "DataAccess::InitializeDatabase";

                return res;
            }
            catch (System.InvalidOperationException ex)
            {
                res.IsError = true;
                res.Error.ErrType = ErrorObject.ErrTypes.DB;
                res.Error.ErrCode = "";
                res.Error.ErrText = ex.Message;
                res.Error.ErrDescription = "InvalidOperationException while connecting to a SQL database file";
                res.Error.ErrDatetime = DateTime.Now;
                res.Error.ErrPlace = "connection.Open";
                res.Error.ErrPlaceParent = "DataAccess::InitializeDatabase";

                return res;
            }
            catch (Exception e)
            {
                res.IsError = true;
                res.Error.ErrType = ErrorObject.ErrTypes.DB;
                res.Error.ErrCode = "";

                if (e.InnerException != null)
                {
                    res.Error.ErrDescription = "InnerException while connecting to a SQL database file";
                    res.Error.ErrText = e.InnerException.Message;
                }
                else
                {
                    res.Error.ErrDescription = "Exception while connecting to a SQL database file";
                    res.Error.ErrText = e.Message;
                }
                res.Error.ErrDatetime = DateTime.Now;
                res.Error.ErrPlace = "connection.Open";
                res.Error.ErrPlaceParent = "DataAccess::InitializeDatabase";

                return res;
            }
        }

        return res;
    }

    public SqliteDataAccessResultWrapper InsertFeed(string feedId, Uri feedUri, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri)
    {
        var res = new SqliteDataAccessResultWrapper();
        var isBreaked = false;

        if (string.IsNullOrEmpty(feedId) || (feedUri == null))
        {
            res.IsError = true;
            // TODO:
            return res;
        }

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.Transaction = connection.BeginTransaction();
                try
                {
                    cmd.CommandText = "INSERT OR IGNORE INTO feeds (feed_id, url, name, title, description, updated, html_url) VALUES (@FeedId, @Uri, @Name, @Title, @Description, @Updated, @HtmlUri)";
                    cmd.CommandType = CommandType.Text;

                    cmd.Parameters.AddWithValue("@FeedId", feedId);
                    cmd.Parameters.AddWithValue("@Uri", feedUri.AbsoluteUri);
                    cmd.Parameters.AddWithValue("@Name", feedName);
                    cmd.Parameters.AddWithValue("@Title", feedTitle);
                    cmd.Parameters.AddWithValue("@Description", feedDescription);
                    cmd.Parameters.AddWithValue("@Updated", updated.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (htmlUri != null)
                    {
                        cmd.Parameters.AddWithValue("@HtmlUri", htmlUri.AbsoluteUri);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@HtmlUri", "");
                    }

                    res.AffectedCount = cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "connection.Open(),Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::InsertFeed";

                    // TODO: need to check if this really isn't needed.
                    //_readerWriterLock.ExitWriteLock();

                    return res;
                }
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDescription = "TargetInvocationException";
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::InsertFeed";

            return res;
        }
        catch (System.InvalidOperationException ex)
        {
            Debug.WriteLine("Opps. InvalidOperationException@DataAccess::InsertFeed");

            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::InsertFeed";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";

            if (e.InnerException != null)
            {
                res.Error.ErrText = e.InnerException.Message;
                res.Error.ErrDescription = "InnerException";
            }
            else
            {
                res.Error.ErrText = e.Message;
                res.Error.ErrDescription = "Exception";
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::InsertFeed";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }
        //Debug.WriteLine(string.Format("{0} Entries Inserted to DB", res.AffectedCount.ToString()));
        
        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return InsertFeed(feedId, feedUri, feedName, feedTitle, feedDescription, updated, htmlUri);
        }

        return res;
    }

    public SqliteDataAccessResultWrapper DeleteFeed(string feedId)
    {
        var res = new SqliteDataAccessResultWrapper();
        var isBreaked = false;

        if (string.IsNullOrEmpty(feedId))
        {
            res.IsError = true;
            // TODO:
            return res;
        }

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = String.Format("DELETE FROM feeds WHERE feed_id = '{0}';", feedId);

                cmd.Transaction = connection.BeginTransaction();
                try
                {
                    res.AffectedCount = cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "cmd.ExecuteNonQuery(),Transaction.Commit()";
                    res.Error.ErrPlaceParent = "DataAccess::DeleteFeed";

                    return res;
                }
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDescription = "TargetInvocationException";
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),cmd.ExecuteNonQuery()";
            res.Error.ErrPlaceParent = "DataAccess::DeleteFeed";

            return res;
        }
        catch (System.InvalidOperationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),cmd.ExecuteNonQuery()";
            res.Error.ErrPlaceParent = "DataAccess::DeleteFeed";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            if (e.InnerException != null)
            {
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
                Debug.WriteLine(e.InnerException.Message + " @DataAccess::DeleteFeed");
            }
            else
            {
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
                Debug.WriteLine(e.Message + " @DataAccess::DeleteFeed");
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),cmd.ExecuteNonQuery()";
            res.Error.ErrPlaceParent = "DataAccess::DeleteFeed";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }
        //Debug.WriteLine(string.Format("{0} feed Deleted from DB", res.AffectedCount));
        
        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return DeleteFeed(feedId);
        }

        return res;
    }

    // Not really used because of updates on InsertEntries.
    public SqliteDataAccessResultWrapper UpdateFeed(string feedId, Uri feedUri, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri)
    {
        var res = new SqliteDataAccessResultWrapper();
        var isBreaked = false;

        if (string.IsNullOrEmpty(feedId))
        {
            res.IsError = true;
            // TODO:
            return res;
        }

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.Transaction = connection.BeginTransaction();
                try
                {
                    var sql = "UPDATE feeds SET ";
                    sql += String.Format("name = '{0}', ", EscapeSingleQuote(feedName));
                    sql += String.Format("title = '{0}', ", EscapeSingleQuote(feedTitle));
                    sql += String.Format("description = '{0}', ", EscapeSingleQuote(feedDescription));
                    sql += String.Format("updated = '{0}'", updated.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (htmlUri != null)
                    {
                        sql += String.Format(", html_url = '{0}'", EscapeSingleQuote(htmlUri.AbsoluteUri));
                    }
                    sql += String.Format(" WHERE feed_id = '{0}'; ", feedId);

                    cmd.CommandText = sql;

                    res.AffectedCount = cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "connection.Open(),Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::UpdateFeed";

                    return res;
                }
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "TargetInvocationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateFeed";

            return res;
        }
        catch (System.InvalidOperationException ex)
        {
            Debug.WriteLine("Opps. InvalidOperationException@DataAccess::UpdateFeed");

            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateFeed";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";

            if (e.InnerException != null)
            {
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
            }
            else
            {
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateFeed";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }
        //Debug.WriteLine(string.Format("{0} feed updated", res.AffectedCount.ToString()));

        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return UpdateFeed(feedId, feedUri, feedName, feedTitle, feedDescription, updated, htmlUri);
        }

        return res;
    }

    public SqliteDataAccessInsertResultWrapper InsertEntries(List<EntryItem> entries, string feedId, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri)
    {
        var res = new SqliteDataAccessInsertResultWrapper();
        var isBreaked = false;

        if (entries is null)
        {
            return res;
        }

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.Transaction = connection.BeginTransaction();
                try
                {
                    // update feed info.
                    var sql = "UPDATE feeds SET ";
                    sql += string.Format("name = '{0}', ", EscapeSingleQuote(feedName));
                    sql += string.Format("title = '{0}', ", EscapeSingleQuote(feedTitle));
                    sql += string.Format("description = '{0}', ", EscapeSingleQuote(feedDescription));
                    sql += string.Format("updated = '{0}'", updated.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (htmlUri != null)
                    {
                        sql += string.Format(", html_url = '{0}'", EscapeSingleQuote(htmlUri.AbsoluteUri));
                    }
                    sql += string.Format(" WHERE feed_id = '{0}'; ", feedId);

                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();

                    // Experimental 
                    // Delete old entries LIMIT 1000.
                    cmd.CommandText = string.Format("DELETE FROM entries WHERE feed_id = '{0}' AND entry_id NOT IN (SELECT entry_id FROM entries WHERE feed_id = '{0}' ORDER BY updated DESC LIMIT 1000);", feedId);//ASC
                    cmd.ExecuteNonQuery();

                    foreach (var entry in entries)
                    {
                        if (entry is not FeedEntryItem)
                        {
                            continue;
                        }

                        //if ((entry.EntryId == null) || (entry.AltHtmlUri == null))
                        if (entry.EntryId == null)
                        {
                            continue;
                        }

                        // Test Upsert...> no way to know how many are newly inserted...
                        //var sqlInsert = "INSERT INTO entries (entry_id, feed_id, url, title, published, updated, author, category, summary, content, content_type, image_url, audio_url, source, source_url, comment_url, status, archived) VALUES (@EntryId, @FeedId, @AltHtmlUri, @Title, @Published, @Updated, @Author, @Category, @Summary, @Content, @ContentType, @ImageUri, @AudioUri, @Source, @SourceUri, @CommentUri, @Status, @IsArchived) ON CONFLICT(entry_id) DO UPDATE SET title=excluded.title, updated=excluded.updated, summary=excluded.summary, content=excluded.content, content_type=excluded.content_type";
                        var sqlInsert = "INSERT OR IGNORE INTO entries (entry_id, feed_id, url, title, published, updated, author, category, summary, content, content_type, image_url, audio_url, source, source_url, comment_url, status, archived) VALUES (@EntryId, @FeedId, @AltHtmlUri, @Title, @Published, @Updated, @Author, @Category, @Summary, @Content, @ContentType, @ImageUri, @AudioUri, @Source, @SourceUri, @CommentUri, @Status, @IsArchived)";

                        cmd.CommandText = sqlInsert;

                        cmd.Parameters.Clear();

                        cmd.Parameters.AddWithValue("@FeedId", entry.ServiceId);// should be same as "feedId"

                        //cmd.Parameters.AddWithValue("@EntryId", entry.EntryId);/ not good.
                        cmd.Parameters.AddWithValue("@EntryId", entry.Id);// should be "entry.Id" here.

                        if (entry.AltHtmlUri != null)
                        {
                            cmd.Parameters.AddWithValue("@AltHtmlUri", entry.AltHtmlUri.AbsoluteUri);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@AltHtmlUri", string.Empty);
                        }

                        if (entry.Title != null)
                        {
                            cmd.Parameters.AddWithValue("@Title", entry.Title);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Title", string.Empty);
                        }

                        if (entry.Published == default)
                        {
                            if (entry.Updated != default)
                            {
                                cmd.Parameters.AddWithValue("@Published", entry.Updated.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@Published", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Published", entry.Published.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        if (entry.Updated == default)
                        {
                            cmd.Parameters.AddWithValue("@Updated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Updated", entry.Updated.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                        //cmd.Parameters.AddWithValue("@Updated", entry.Updated.ToString("yyyy-MM-dd HH:mm:ss"));

                        if (entry.Author != null)
                        {

                            cmd.Parameters.AddWithValue("@Author", entry.Author);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Author", string.Empty);
                        }

                        if (entry.Category != null)
                        {
                            cmd.Parameters.AddWithValue("@Category", entry.Category);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Category", string.Empty);
                        }

                        if (entry.Summary != null)
                        {
                            cmd.Parameters.AddWithValue("@Summary", entry.Summary);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Summary", string.Empty);
                        }

                        if (entry.Content != null)
                        {
                            cmd.Parameters.AddWithValue("@Content", entry.Content);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Content", string.Empty);
                        }

                        cmd.Parameters.AddWithValue("@ContentType", entry.ContentType.ToString());

                        /*
                        if (entry.IsImageDownloaded)
                            cmd.Parameters.AddWithValue("@IsImageDownloaded", bool.TrueString);
                        else
                            cmd.Parameters.AddWithValue("@IsImageDownloaded", bool.FalseString);
                        */

                        if (entry.ImageUri != null)
                        {
                            cmd.Parameters.AddWithValue("@ImageUri", entry.ImageUri.AbsoluteUri);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@ImageUri", string.Empty);
                        }

                        if (entry.AudioUri != null)
                        {
                            cmd.Parameters.AddWithValue("@AudioUri", entry.AudioUri.AbsoluteUri);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@AudioUri", string.Empty);
                        }

                        //

                        if (entry is FeedEntryItem fei)
                        {
                            if (fei.Source != null)
                            {
                                cmd.Parameters.AddWithValue("@Source", fei.Source);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@Source", string.Empty);
                            }

                            if (fei.SourceUri != null)
                            {
                                cmd.Parameters.AddWithValue("@SourceUri", fei.SourceUri.AbsoluteUri);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@SourceUri", string.Empty);
                            }

                            if (fei.CommentUri != null)
                            {
                                cmd.Parameters.AddWithValue("@CommentUri", fei.CommentUri.AbsoluteUri);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@CommentUri", string.Empty);
                            }

                            cmd.Parameters.AddWithValue("@Status", fei.Status.ToString());
                            cmd.Parameters.AddWithValue("@IsArchived", bool.FalseString);//(entry as FeedEntryItem).IsArchived.ToString()
                        }

                        var r = cmd.ExecuteNonQuery();

                        if (r > 0)
                        {
                            //c++;
                            res.AffectedCount++;

                            res.InsertedEntries.Add(entry);
                        }
                        else
                        {
                            // Update
                            var sqlUpdate = "UPDATE entries SET ";
                            sqlUpdate += string.Format("title = '{0}', ", EscapeSingleQuote(entry.Title ?? ""));
                            sqlUpdate += string.Format("author = '{0}', ", EscapeSingleQuote(entry.Author ?? "-"));
                            sqlUpdate += string.Format("category = '{0}', ", EscapeSingleQuote(entry.Category ?? "-"));
                            sqlUpdate += string.Format("summary = '{0}', ", EscapeSingleQuote(entry.Summary ?? ""));
                            sqlUpdate += string.Format("content = '{0}', ", EscapeSingleQuote(entry.Content ?? ""));
                            if (entry.ImageUri != null)
                            {
                                sqlUpdate += string.Format("image_url = '{0}', ", EscapeSingleQuote(entry.ImageUri.AbsoluteUri));
                            }
                            else
                            {
                                sqlUpdate += string.Format("image_url = '{0}', ", "");
                            }
                            if (entry.AudioUri != null)
                            {
                                sqlUpdate += string.Format("audio_url = '{0}', ", EscapeSingleQuote(entry.AudioUri.AbsoluteUri));
                            }
                            else
                            {
                                sqlUpdate += string.Format("audio_url = '{0}', ", "");
                            }

                            if (entry.Updated == default)
                            {
                                sqlUpdate += string.Format("updated = '{0}'", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            else
                            {
                                sqlUpdate += string.Format("updated = '{0}'", updated.ToString("yyyy-MM-dd HH:mm:ss"));
                            }

                            if (entry.AltHtmlUri != null)
                            {
                                sqlUpdate += string.Format(", url = '{0}'", EscapeSingleQuote(entry.AltHtmlUri.AbsoluteUri));
                            }
                            sqlUpdate += string.Format(" WHERE entry_id = '{0}'; ", entry.Id);
                            //Debug.WriteLine(sqlUpdate);
                            cmd.CommandText = sqlUpdate;
                            var c = cmd.ExecuteNonQuery();
                            if (c > 0)
                            {
                                //Debug.WriteLine($"{c} entries updated.");
                            }
                        }
                    }

                    cmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "connection.Open(),Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::InsertEntries";
                    
                    return res;
                }
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "TargetInvocationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::InsertEntries";

            return res;
        }
        catch (System.InvalidOperationException ex)
        {
            Debug.WriteLine("Opps. InvalidOperationException@DataAccess::InsertEntries");

            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::InsertEntries";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";

            if (e.InnerException != null)
            {
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
            }
            else
            {
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::InsertEntries";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }
        //Debug.WriteLine(string.Format("{0} Entries Inserted to DB", res.AffectedCount.ToString()));

        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return InsertEntries(entries, feedId, feedName, feedTitle, feedDescription, updated, htmlUri);
        }

        return res;
    }

    public SqliteDataAccessSelectResultWrapper SelectEntriesByFeedId(string feedId, bool IsUnarchivedOnly = true)
    {
        var res = new SqliteDataAccessSelectResultWrapper();

        if (string.IsNullOrEmpty(feedId))
        {
            return res;
        }

        try
        {
            _readerWriterLock.EnterReadLock();

            using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            if (IsUnarchivedOnly)
            {
                //cmd.CommandText = String.Format("SELECT * FROM entries INNER JOIN feeds USING (feed_id) WHERE feed_id = '{0}' AND archived = '{1}' ORDER BY published DESC LIMIT 1000", feedId, bool.FalseString);

                cmd.CommandText = String.Format("SELECT feeds.name as feedName, entries.title as entryTitle, entries.entry_id as entryId, entries.url as entryUrl, entries.published as entryPublished, entries.updated as entryUpdated, entries.summary as entrySummary, entries.content as entryContent, entries.content_type as entryContentType, entries.image_url as entryImageUri, entries.audio_url as entryAudioUri, entries.source as entrySource, entries.source_url as entrySourceUri, entries.author as entryAuthor, entries.category as entryCategory, entries.comment_url as entryCommentUri, entries.archived as entryArchived, entries.status as entryStatus FROM entries INNER JOIN feeds USING (feed_id) WHERE feed_id = '{0}' AND archived = '{1}' ORDER BY published DESC LIMIT 1000", feedId, bool.FalseString);
            }
            else
            {
                cmd.CommandText = String.Format("SELECT feeds.name as feedName, entries.title as entryTitle, entries.entry_id as entryId, entries.url as entryUrl, entries.published as entryPublished, entries.updated as entryUpdated, entries.summary as entrySummary, entries.content as entryContent, entries.content_type as entryContentType, entries.image_url as entryImageUri, entries.audio_url as entryAudioUri, entries.source as entrySource, entries.source_url as entrySourceUri, entries.author as entryAuthor, entries.category as entryCategory, entries.comment_url as entryCommentUri, entries.archived as entryArchived, entries.status as entryStatus FROM entries INNER JOIN feeds USING (feed_id) WHERE feed_id = '{0}' ORDER BY published DESC LIMIT 5000", feedId);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var s = Convert.ToString(reader["entryTitle"]);
                if (string.IsNullOrEmpty(s))
                {
                    s = "-";
                }
                var entry = new FeedEntryItem(s, feedId, null);

                entry.EntryId = Convert.ToString(reader["entryId"]);

                s = Convert.ToString(reader["entryUrl"]);
                if (!string.IsNullOrEmpty(s))
                {
                    entry.AltHtmlUri = new Uri(RestoreSingleQuote(s));
                }

                s = Convert.ToString(reader["entryPublished"]);
                if (!string.IsNullOrEmpty(s)) 
                {
                    try
                    {
                        entry.Published = DateTime.Parse(s);
                    }
                    catch { }
                }

                s = Convert.ToString(reader["entryUpdated"]);
                if (!string.IsNullOrEmpty(s))
                {
                    try
                    {
                        entry.Updated = DateTime.Parse(s);
                    }
                    catch { }
                }

                s = Convert.ToString(reader["entrySummary"]);
                if (!string.IsNullOrEmpty(s))
                {
                    entry.Summary = s;
                }

                s = Convert.ToString(reader["entryContent"]);
                if (!string.IsNullOrEmpty(s))
                {
                    entry.Content = s;
                }

                var t = Convert.ToString(reader["entryContentType"]);
                if (t == "textHtml")
                {
                    entry.ContentType = EntryItem.ContentTypes.textHtml;
                }
                else if (t == "text")
                {
                    entry.ContentType = EntryItem.ContentTypes.text;
                }
                else if (t == "markdown")
                {
                    entry.ContentType = EntryItem.ContentTypes.markdown;
                }
                else if (t == "hatena")
                {
                    entry.ContentType = EntryItem.ContentTypes.hatena;
                }
                else if (t == "unknown")
                {
                    entry.ContentType = EntryItem.ContentTypes.hatena;
                }
                else if (t == "none")
                {
                    entry.ContentType = EntryItem.ContentTypes.none;
                }
                else
                {
                    entry.ContentType = EntryItem.ContentTypes.unknown;
                }

                entry.Source = Convert.ToString(reader["entrySource"]) ?? "";

                var su = Convert.ToString(reader["entrySourceUri"]);
                if (!string.IsNullOrEmpty(su))
                {
                    entry.SourceUri = new Uri(su);
                }

                var u = Convert.ToString(reader["entryImageUri"]);
                if (!string.IsNullOrEmpty(u))
                {
                    entry.ImageUri = new Uri(RestoreSingleQuote(u));
                }

                var au = Convert.ToString(reader["entryAudioUri"]);
                if (!string.IsNullOrEmpty(au))
                {
                    entry.AudioUri = new Uri(RestoreSingleQuote(au));
                }

                var cu = Convert.ToString(reader["entryCommentUri"]);
                if (!string.IsNullOrEmpty(cu))
                {
                    entry.CommentUri = new Uri(cu);
                }

                /*
                if (!string.IsNullOrEmpty(Convert.ToString(reader["image_url"])))
                    entry.ImageUri = new Uri(Convert.ToString(reader["image_url"]));
                */
                /*
                string bln = Convert.ToString(reader["IsImageDownloaded"]);
                if (!string.IsNullOrEmpty(bln))
                {
                    if (bln == bool.TrueString)
                        entry.IsImageDownloaded = true;
                    else
                        entry.IsImageDownloaded = false;
                }
                */

                /*
                if (reader["Image"] != DBNull.Value)
                {
                    byte[] imageBytes = (byte[])reader["Image"];

                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            entry.Image = BitmapImageFromBytes(imageBytes);
                        });
                    }

                    entry.IsImageDownloaded = true;
                }
                */
                //entry.ImageId = Convert.ToString(reader["image_id"]);

                var status = Convert.ToString(reader["entryStatus"]);
                if (!string.IsNullOrEmpty(status))
                {
                    entry.Status = entry.StatusTextToType(status);
                }

                entry.Author = Convert.ToString(reader["entryAuthor"]) ?? "";

                entry.Category = Convert.ToString(reader["entryCategory"]) ?? "";

                var blnstr = Convert.ToString(reader["entryArchived"]);
                if (!string.IsNullOrEmpty(blnstr))
                {
                    if (blnstr == bool.TrueString)
                        entry.IsArchived = true;
                    else
                        entry.IsArchived = false;
                }
                
                if (entry.IsArchived)
                {
                    if (entry.Status == FeedEntryItem.ReadStatus.rsNew)
                        entry.Status = FeedEntryItem.ReadStatus.rsNormal;

                    if (entry.Status == FeedEntryItem.ReadStatus.rsNewVisited)
                        entry.Status = FeedEntryItem.ReadStatus.rsNormalVisited;
                }
                
                if (!entry.IsArchived)
                {
                    res.UnreadCount++;
                }

                //entry.Publisher = Convert.ToString(reader["name"]);
                entry.FeedTitle = Convert.ToString(reader["feedName"]) ?? "";

                res.AffectedCount++;

                res.SelectedEntries.Add(entry);
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "TargetInvocationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::SelectEntriesByFeedId";
        }
        catch (System.InvalidOperationException ex)
        {
            Debug.WriteLine("Opps. InvalidOperationException@DataAccess::SelectEntriesByFeedId");

            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::SelectEntriesByFeedId";
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            if (e.InnerException != null)
            {
                Debug.WriteLine(e.InnerException.Message + " @DataAccess::SelectEntriesByFeedId");
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.InnerException.Message;
            }
            else
            {
                Debug.WriteLine(e.Message + " @DataAccess::SelectEntriesByFeedId");
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::SelectEntriesByFeedId";
        }
        finally
        {
            _readerWriterLock.ExitReadLock();
        }

        return res;
    }

    public SqliteDataAccessSelectResultWrapper SelectEntriesByFeedIds(List<string> feedIds, bool IsUnarchivedOnly = true)
    {
        var res = new SqliteDataAccessSelectResultWrapper();

        if (feedIds is null)
            return res;

        if (feedIds.Count == 0)
            return res;

        var before = "SELECT feeds.name as feedName, feeds.feed_id as feedId, entries.title as entryTitle, entries.entry_id as entryId, entries.url as entryUrl, entries.published as entryPublished, entries.updated as entryUpdated, entries.summary as entrySummary, entries.content as entryContent, entries.content_type as entryContentType, entries.image_url as entryImageUri, entries.audio_url as entryAudioUri, entries.source as entrySource, entries.source_url as entrySourceUri, entries.author as entryAuthor, entries.category as entryCategory, entries.comment_url as entryCommentUri, entries.archived as entryArchived, entries.status as entryStatus FROM entries INNER JOIN feeds USING (feed_id) WHERE ";

        var middle = "(";

        foreach (var asdf in feedIds)
        {
            if (middle != "(")
                middle += "OR ";

            middle += String.Format("feed_id = '{0}' ", asdf);
        }

        //string after = string.Format(") AND IsArchived = '{0}' ORDER BY Published DESC LIMIT 1000", bool.FalseString);
        string after;
        if (IsUnarchivedOnly)
        {
            after = string.Format(") AND archived = '{0}' ORDER BY published DESC LIMIT 1000", bool.FalseString);
        }
        else
        {
            after = string.Format(") ORDER BY published DESC LIMIT 10000");
        }

        //Debug.WriteLine(before + middle + after);

        try
        {
            _readerWriterLock.EnterReadLock();

            using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = before + middle + after;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var fid = Convert.ToString(reader["feedId"]);
                if (string.IsNullOrEmpty(fid))
                    continue;

                FeedEntryItem entry = new FeedEntryItem(Convert.ToString(reader["entryTitle"]) ?? "no title", fid, null);

                //entry.MyNodeFeed = ndf;

                entry.EntryId = Convert.ToString(reader["entryId"]);

                //if (!string.IsNullOrEmpty(Convert.ToString(reader["entryUrl"])))
                //    entry.AltHtmlUri = new Uri(Convert.ToString(reader["entryUrl"]));

                var s = Convert.ToString(reader["entryUrl"]);
                if (!string.IsNullOrEmpty(s))
                {
                    entry.AltHtmlUri = new Uri(RestoreSingleQuote(s));
                }

                var pnsd = Convert.ToString(reader["entryPublished"]);
                if (!string.IsNullOrEmpty(pnsd))
                {
                    entry.Published = DateTime.Parse(pnsd);
                }

                try
                {
                    var hoge = Convert.ToString(reader["entryUpdated"]);
                    if (!string.IsNullOrEmpty(hoge))
                    {
                        entry.Updated = DateTime.Parse(hoge);
                    }
                }
                catch { }

                entry.Summary = Convert.ToString(reader["entrySummary"]) ?? "";

                entry.Content = Convert.ToString(reader["entryContent"]) ?? "";

                var t = Convert.ToString(reader["entryContentType"]);
                if (t == "textHtml")
                {
                    entry.ContentType = EntryItem.ContentTypes.textHtml;
                }
                else if (t == "text")
                {
                    entry.ContentType = EntryItem.ContentTypes.text;
                }
                else if (t == "markdown")
                {
                    entry.ContentType = EntryItem.ContentTypes.markdown;
                }
                else if (t == "hatena")
                {
                    entry.ContentType = EntryItem.ContentTypes.hatena;
                }
                else if (t == "unknown")
                {
                    entry.ContentType = EntryItem.ContentTypes.hatena;
                }
                else if (t == "none")
                {
                    entry.ContentType = EntryItem.ContentTypes.none;
                }
                else
                {
                    entry.ContentType = EntryItem.ContentTypes.unknown;
                }

                var u = Convert.ToString(reader["entryImageUri"]);
                if (!string.IsNullOrEmpty(u))
                {
                    entry.ImageUri = new Uri(RestoreSingleQuote(u));
                }

                var au = Convert.ToString(reader["entryAudioUri"]);
                if (!string.IsNullOrEmpty(au))
                {
                    entry.AudioUri = new Uri(RestoreSingleQuote(au));
                }

                var cu = Convert.ToString(reader["entryCommentUri"]);
                if (!string.IsNullOrEmpty(cu))
                {
                    entry.CommentUri = new Uri(cu);
                }
                /*

                if (reader["Image"] != DBNull.Value)
                {
                    byte[] imageBytes = (byte[])reader["Image"];

                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            entry.Image = BitmapImageFromBytes(imageBytes);
                        });
                    }

                    entry.IsImageDownloaded = true;
                }
                */
                /*
                string bln = Convert.ToString(reader["IsImageDownloaded"]);
                if (!string.IsNullOrEmpty(bln))
                {
                    if (bln == bool.TrueString)
                        entry.IsImageDownloaded = true;
                    else
                        entry.IsImageDownloaded = false;
                }
                */

                //entry.ImageId = Convert.ToString(reader["image_id"]);

                /*
                string status = Convert.ToString(reader["status"]);
                if (!string.IsNullOrEmpty(status))
                    entry.Status = entry.StatusTextToType(status);
                */
                var status = Convert.ToString(reader["entryStatus"]);
                if (!string.IsNullOrEmpty(status))
                {
                    entry.Status = entry.StatusTextToType(status);
                }

                entry.Source = Convert.ToString(reader["entrySource"]) ?? "";

                var su = Convert.ToString(reader["entrySourceUri"]);
                if (!string.IsNullOrEmpty(su))
                {
                    entry.SourceUri = new Uri(su);
                }

                entry.Author = Convert.ToString(reader["entryAuthor"]) ?? "";

                entry.Category = Convert.ToString(reader["entryCategory"]) ?? "";

                var blnstr = Convert.ToString(reader["entryArchived"]);
                if (!string.IsNullOrEmpty(blnstr))
                {
                    if (blnstr == bool.TrueString)
                        entry.IsArchived = true;
                    else
                        entry.IsArchived = false;
                }

                if (entry.IsArchived)
                {
                    if (entry.Status == FeedEntryItem.ReadStatus.rsNew)
                        entry.Status = FeedEntryItem.ReadStatus.rsNormal;

                    if (entry.Status == FeedEntryItem.ReadStatus.rsNewVisited)
                        entry.Status = FeedEntryItem.ReadStatus.rsNormalVisited;
                }

                if (!entry.IsArchived)
                {
                    res.UnreadCount++;
                }

                entry.FeedTitle = Convert.ToString(reader["feedName"]) ?? "";

                res.AffectedCount++;

                res.SelectedEntries.Add(entry);
            }
        }
        catch (System.InvalidOperationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::SelectEntriesByMultipleFeedIds";
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            if (e.InnerException != null)
            {
                Debug.WriteLine(e.InnerException.Message + " @DataAccess::SelectEntriesByMultipleFeedIds");
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
                res.Error.ErrDescription = "InnerException";
            }
            else
            {
                Debug.WriteLine(e.Message + " @DataAccess::SelectEntriesByMultipleFeedIds");
                res.Error.ErrText = e.Message;
                res.Error.ErrDescription = "Exception";
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),ExecuteReader()";
            res.Error.ErrPlaceParent = "DataAccess::SelectEntriesByMultipleFeedIds";
        }
        finally
        {
            _readerWriterLock.ExitReadLock();
        }

        return res;
    }

    public SqliteDataAccessResultWrapper UpdateAllEntriesAsArchived(List<string> feedIds)
    {
        var res = new SqliteDataAccessResultWrapper();
        var isBreaked = false;

        if (feedIds is null)
            return res;

        if (feedIds.Count == 0)
            return res;

        var before = string.Format("UPDATE entries SET archived = '{0}' WHERE ", bool.TrueString);

        var middle = "(";

        foreach (var asdf in feedIds)
        {
            if (middle != "(")
                middle += "OR ";

            middle += String.Format("feed_id = '{0}' ", asdf);
        }

        var after = string.Format(") AND archived = '{0}'", bool.FalseString);

        //Debug.WriteLine(before + middle + after);

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.Transaction = connection.BeginTransaction();

                try
                {
                    cmd.CommandText = before + middle + after;

                    cmd.CommandType = CommandType.Text;

                    res.AffectedCount = cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "connection.Open(),Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::UpdateAllEntriesAsRead";

                    return res;
                }
            }
        }
        catch (System.InvalidOperationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateAllEntriesAsRead";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";

            if (e.InnerException != null)
            {
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
            }
            else
            {
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateAllEntriesAsRead";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }
        //Debug.WriteLine(string.Format("{0} Entries from {1} Updated as read in the DB", c.ToString(), feedId));

        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return UpdateAllEntriesAsArchived(feedIds);
        }

        return res;
    }

    public SqliteDataAccessResultWrapper UpdateEntryReadStatus(string? entryId, ReadStatus readStatus)
    {
        var res = new SqliteDataAccessResultWrapper();
        var isBreaked = false;

        if (string.IsNullOrEmpty(entryId))
        {
            res.IsError = true;
            // TODO:
            return res;
        }

        var sql = "UPDATE entries SET ";
        sql += string.Format("status = '{0}'", readStatus.ToString());
        sql += string.Format(" WHERE entry_id = '{0}'; ", entryId);

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.Transaction = connection.BeginTransaction();
                try
                {
                    cmd.CommandText = sql;

                    res.AffectedCount = cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = e.Message;
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "connection.Open(),Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::UpdateEntryReadStatus";

                    return res;
                }
            }
        }
        catch (System.InvalidOperationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateEntryReadStatus";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";

            if (e.InnerException != null)
            {
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
            }
            else
            {
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),BeginTransaction()";
            res.Error.ErrPlaceParent = "DataAccess::UpdateEntryReadStatus";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }

        //Debug.WriteLine(string.Format("{0} Entries from {1} Updated as read in the DB", c.ToString(), feedId));

        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return UpdateEntryReadStatus(entryId, readStatus);
        }

        return res;
    }

    // Not really used because of "ON DELETE CASCADE".
    public SqliteDataAccessResultWrapper DeleteEntriesByFeedIds(List<string> feedIds)
    {
        var res = new SqliteDataAccessResultWrapper();
        var isBreaked = false;

        if (feedIds is null)
            return res;

        if (feedIds.Count == 0)
            return res;

        string sqlDelEntries;// = string.Empty;

        if (feedIds.Count > 1)
        {
            var before = "DELETE FROM entries WHERE ";
            //var before = "DELETE FROM feeds WHERE ";
            var middle = "(";

            foreach (var asdf in feedIds)
            {
                if (middle != "(")
                    middle += "OR ";

                middle += String.Format("feed_id = '{0}' ", asdf);
            }

            var after = ")";

            sqlDelEntries = before + middle + after;
        }
        else
        {
            sqlDelEntries = String.Format("DELETE FROM entries WHERE feed_id = '{0}';", feedIds[0]);
            //sqlDelEntries = String.Format("DELETE FROM feeds WHERE feed_id = '{0}';", feedIds[0]);
        }

        //Debug.WriteLine(sqlDelEntries);

        _readerWriterLock.EnterWriteLock();
        try
        {
            if (_readerWriterLock.WaitingReadCount > 0)
            {
                isBreaked = true;
            }
            else
            {
                using var connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.Transaction = connection.BeginTransaction();
                try
                {
                    cmd.CommandText = sqlDelEntries;

                    res.AffectedCount = cmd.ExecuteNonQuery();

                    cmd.Transaction.Commit();
                }
                catch (Exception ex)
                {
                    cmd.Transaction.Rollback();

                    res.IsError = true;
                    res.Error.ErrType = ErrorObject.ErrTypes.DB;
                    res.Error.ErrCode = "";
                    res.Error.ErrText = ex.Message;
                    res.Error.ErrDescription = "Exception";
                    res.Error.ErrDatetime = DateTime.Now;
                    res.Error.ErrPlace = "connection.Open(),Transaction.Commit";
                    res.Error.ErrPlaceParent = "DataAccess::DeleteEntriesByFeedIds";

                    return res;
                }
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "TargetInvocationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),cmd.ExecuteNonQuery()";
            res.Error.ErrPlaceParent = "DataAccess::DeleteEntriesByFeedIds";

            return res;
        }
        catch (System.InvalidOperationException ex)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            res.Error.ErrDescription = "InvalidOperationException";
            res.Error.ErrText = ex.Message;
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),cmd.ExecuteNonQuery()";
            res.Error.ErrPlaceParent = "DataAccess::DeleteEntriesByFeedIds";

            return res;
        }
        catch (Exception e)
        {
            res.IsError = true;
            res.Error.ErrType = ErrorObject.ErrTypes.DB;
            res.Error.ErrCode = "";
            if (e.InnerException != null)
            {
                Debug.WriteLine(e.InnerException.Message + " @DataAccess::DeleteEntriesByFeedIds");
                res.Error.ErrDescription = "InnerException";
                res.Error.ErrText = e.Message + " " + e.InnerException.Message;
            }
            else
            {
                Debug.WriteLine(e.Message + " @DataAccess::DeleteEntriesByFeedIds");
                res.Error.ErrDescription = "Exception";
                res.Error.ErrText = e.Message;
            }
            res.Error.ErrDatetime = DateTime.Now;
            res.Error.ErrPlace = "connection.Open(),cmd.ExecuteNonQuery()";
            res.Error.ErrPlaceParent = "DataAccess::DeleteEntriesByFeedIds";

            return res;
        }
        finally
        {
            _readerWriterLock.ExitWriteLock();
        }

        Debug.WriteLine(string.Format("{0} Entries Deleted from DB", res.AffectedCount));

        if (isBreaked)
        {
            Thread.Sleep(10);
            //await Task.Delay(100);

            return DeleteEntriesByFeedIds(feedIds);
        }

        return res;
    }

    // ColumnExists check
    private static bool ColumnExists(IDataRecord dr, string columnName)
    {
        for (var i = 0; i < dr.FieldCount; i++)
        {
            if (dr.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }
        return false; ;
    }

    private static string EscapeSingleQuote(string s)
    {
        return s is null ? string.Empty : s.Replace("'", "''");
    }

    private static string RestoreSingleQuote(string s)
    {
        return s is null ? string.Empty : s.Replace("''", "'");
    }
}
