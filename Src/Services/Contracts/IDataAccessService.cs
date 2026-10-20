using FeedDesk.Models;
using System;
using System.Collections.Generic;
using static FeedDesk.Models.FeedEntryItem;

namespace FeedDesk.Services.Contracts;

public interface IDataAccessService
{
    SqliteDataAccessResultWrapper InitializeDatabase(string dataBaseFilePath);

    SqliteDataAccessResultWrapper InsertFeed(string feedId, Uri feedUri, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri);

    SqliteDataAccessResultWrapper UpdateFeed(string feedId, Uri feedUri, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri);

    SqliteDataAccessResultWrapper DeleteFeed(string feedId);

    SqliteDataAccessSelectResultWrapper SelectEntriesByFeedId(string feedId, bool IsUnarchivedOnly = true);

    SqliteDataAccessSelectResultWrapper SelectEntriesByFeedIds(List<string> feedIds, bool IsUnarchivedOnly = true);

    SqliteDataAccessInsertResultWrapper InsertEntries(List<EntryItem> entries, string feedId, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri);

    int CountEntriesByFeedId(string feedId, bool IsUnarchivedOnly = true);

    int CountEntriesByFeedIds(List<string> feedIds, bool IsUnarchivedOnly = true);

    SqliteDataAccessResultWrapper UpdateAllEntriesAsArchived(List<string> feedIds, bool isReadOnly = false);

    SqliteDataAccessResultWrapper UpdateEntryAsArchived(string? entryId);

    SqliteDataAccessResultWrapper UpdateEntryReadStatus(string? entryId, ReadStatus readStatus);

    SqliteDataAccessResultWrapper DeleteEntriesByFeedIds(List<string> feedIds);

}



