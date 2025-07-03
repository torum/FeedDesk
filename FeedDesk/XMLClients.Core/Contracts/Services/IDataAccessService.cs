﻿using XmlClients.Core.Models;
using static XmlClients.Core.Models.FeedEntryItem;

namespace XmlClients.Core.Contracts.Services;

public interface IDataAccessService
{
    SqliteDataAccessResultWrapper InitializeDatabase(string dataBaseFilePath);

    SqliteDataAccessResultWrapper InsertFeed(string feedId, Uri feedUri, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri);

    SqliteDataAccessResultWrapper UpdateFeed(string feedId, Uri feedUri, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri? htmlUri);

    SqliteDataAccessResultWrapper DeleteFeed(string feedId);

    SqliteDataAccessSelectResultWrapper SelectEntriesByFeedId(string feedId, bool IsUnarchivedOnly = true);
    
    SqliteDataAccessSelectResultWrapper SelectEntriesByFeedIds(List<string> feedIds, bool IsUnarchivedOnly = true);


    //SqliteDataAccessSelectImageResultWrapper SelectImageByImageId(string imageId);

    SqliteDataAccessInsertResultWrapper InsertEntries(List<EntryItem> entries, string feedId, string feedName, string feedTitle, string feedDescription, DateTime updated, Uri htmlUri);


    //SqliteDataAccessInsertResultWrapper InsertImages(List<EntryItem> entries);

    SqliteDataAccessResultWrapper UpdateAllEntriesAsArchived(List<string> feedIds);

    SqliteDataAccessResultWrapper UpdateEntryReadStatus(string? entryId, ReadStatus readStatus);


    //SqliteDataAccessResultWrapper UpdateEntryStatus(EntryItem entry);

    SqliteDataAccessResultWrapper DeleteEntriesByFeedIds(List<string> feedIds);

}



