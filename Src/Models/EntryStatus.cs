namespace FeedDesk.Models;

public enum EntryArchivingStatusKeys
{
    Inbox,  All
}
//Archived, Read, Unread,

public class EntryArchivingStatus(EntryArchivingStatusKeys key, string label)
{
    public EntryArchivingStatusKeys Key { get; set; } = key;
    public string Label { get; set; } = label;
}
