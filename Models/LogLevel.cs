namespace WrightLogs.Models;

public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}

public static class LogLevelParser
{
    public static LogLevel Parse(string? level) => level switch
    {
        "Verbose" => LogLevel.Verbose,
        "Debug" => LogLevel.Debug,
        "Information" => LogLevel.Information,
        "Warning" => LogLevel.Warning,
        "Error" => LogLevel.Error,
        "Fatal" => LogLevel.Fatal,
        _ => LogLevel.Information,
    };
}
