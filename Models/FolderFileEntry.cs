namespace WrightLogs.Models;

public enum FolderFileKind
{
    Log,
    Usage,
    UsageAll,
}

/// <summary>A file (or virtual aggregate) discovered in an opened folder, ready to pick from the file dropdown.</summary>
public sealed record FolderFileEntry(string FullPath, string FileName, FolderFileKind Kind)
{
    public string Icon => Kind switch
    {
        FolderFileKind.Log => "\U0001F4C4",
        FolderFileKind.UsageAll => "\U0001F4C8",
        _ => "\U0001F4CA",
    };
}
