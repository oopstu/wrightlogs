using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WrightLogs.Services;

public static class ColumnLayoutSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WrightLogs",
        "columnwidths.json");

    public static Dictionary<string, double> Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new Dictionary<string, double>();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json)
                   ?? new Dictionary<string, double>();
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    public static void Save(Dictionary<string, double> columnWidths)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(columnWidths);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence; a failed save shouldn't block app shutdown.
        }
    }
}
