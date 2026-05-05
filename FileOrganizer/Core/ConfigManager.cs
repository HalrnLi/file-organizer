using System.Text.Json;

namespace FileOrganizer.Core;

public class ConfigManager
{
    private readonly string _rulesPath;
    private readonly string _settingsPath;
    private List<Rule> _rules = new();
    private Settings _settings = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ConfigManager(string? configDir = null)
    {
        configDir ??= Path.GetDirectoryName(Environment.ProcessPath)!;
        _rulesPath = Path.Combine(configDir, "rules.json");
        _settingsPath = Path.Combine(configDir, "settings.json");
    }

    public IReadOnlyList<Rule> Rules => _rules.AsReadOnly();
    public Settings Settings => _settings;

    public void Load()
    {
        _rules = LoadFile(_rulesPath, () => new List<Rule>());
        _settings = LoadFile(_settingsPath, () => new Settings());
    }

    public void SaveRules()
    {
        var json = JsonSerializer.Serialize(_rules, JsonOptions);
        File.WriteAllText(_rulesPath, json);
    }

    public void SaveSettings()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public void AddRule(Rule rule)
    {
        _rules.Add(rule);
        SaveRules();
    }

    public bool RemoveRule(string id)
    {
        var removed = _rules.RemoveAll(r => r.Id == id);
        if (removed > 0) SaveRules();
        return removed > 0;
    }

    public Rule? GetRuleById(string id)
    {
        return _rules.FirstOrDefault(r => r.Id == id);
    }

    public void UpdateRule(Rule updated)
    {
        var idx = _rules.FindIndex(r => r.Id == updated.Id);
        if (idx >= 0)
        {
            _rules[idx] = updated;
            SaveRules();
        }
    }

    private T LoadFile<T>(string path, Func<T> defaultFactory)
    {
        try
        {
            if (!File.Exists(path))
            {
                var instance = defaultFactory();
                var json = JsonSerializer.Serialize(instance, JsonOptions);
                File.WriteAllText(path, json);
                return instance;
            }
            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(content) ?? defaultFactory();
        }
        catch
        {
            return defaultFactory();
        }
    }
}
