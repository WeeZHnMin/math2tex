using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace Math2Tex.Storage;

public static class HistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Math2Tex",
        "history.json");

    public static ObservableCollection<HistoryItem> Load()
    {
        if (!File.Exists(FilePath))
        {
            return new ObservableCollection<HistoryItem>();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var items = JsonSerializer.Deserialize<List<HistoryItem>>(json, JsonOptions);
            return new ObservableCollection<HistoryItem>(items ?? new List<HistoryItem>());
        }
        catch
        {
            return new ObservableCollection<HistoryItem>();
        }
    }

    public static void Save(IEnumerable<HistoryItem> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
