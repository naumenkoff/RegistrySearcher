using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace RegistrySearcher;

public class RegistrySearcher
{
    public static readonly List<string> History = new();
    private readonly IReadOnlyCollection<string> _blackList;
    private readonly string _targetName;
    private int _failedKeyAccess;
    private List<WorkResult> _foundMatches;
    private string _json;
    private TimeSpan _searchTime;

    public RegistrySearcher(string targetName, IReadOnlyCollection<string> blackList)
    {
        _targetName = targetName;
        _blackList = blackList;
        _foundMatches = new List<WorkResult>();
    }

    public bool Finished { get; private set; }

    public async Task<string> StartSearch()
    {
        var startTime = DateTime.Now;
        await Task.WhenAll(
            //CreateSearchingStream(RegistryHive.ClassesRoot),
            CreateSearchingStream(RegistryHive.CurrentUser),
            CreateSearchingStream(RegistryHive.LocalMachine),
            CreateSearchingStream(RegistryHive.Users)
            //CreateSearchingStream(RegistryHive.PerformanceData),
            //CreateSearchingStream(RegistryHive.CurrentConfig),
        );
        return ConstructJson(startTime);
    }

    private async Task CreateSearchingStream(RegistryHive registryHive)
    {
        await Task.Run(() =>
        {
            using var registryKey64 = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry64);
            FindMatches(registryKey64);
        });
    }

    // private AutoResetEvent _resetEvent = new(false);
    private void FindMatches(RegistryKey registryKey)
    {
        foreach (var keyName in registryKey.GetSubKeyNames())
        {
            try
            {
                using var regKey = registryKey.OpenSubKey(keyName);
                if (regKey is null) return;
                var valueNames = regKey.GetValueNames();
                if (regKey.SubKeyCount is 0) GetValue(regKey, valueNames);
                else FindMatches(regKey);
            }
            catch
            {
                _failedKeyAccess++;
            }
        }
    }

    private void GetValue(RegistryKey regKey, IEnumerable<string> valueNames)
    {
        foreach (var valueName in valueNames)
        {
            var value = regKey.GetValue(valueName) as string;
            if (valueName.ToLower().Contains(_targetName) || (value != null && value.ToLower().Contains(_targetName)))
                _foundMatches.Add(new WorkResult(regKey.Name, valueName, value));
        }
    }

    private string ConstructJson(DateTime startTime)
    {
        _searchTime = DateTime.Now - startTime;
        Filter();
        Finished = true;
        _json = JsonConvert.SerializeObject(_foundMatches, Formatting.Indented) + Environment.NewLine +
                $"Найдено {_foundMatches.Count} вхождений за {_searchTime.TotalMilliseconds / 1000:F} сек. Количество необработанных ключей реестра: '{_failedKeyAccess}'.";
        History.Add(_json);
        return _json;
    }

    private void Filter()
    {
        var foundMatches = (
            from t in _foundMatches
            let registryKey = t.RegistryKey.ToLower()
            where !_blackList.Any(registryKey.Contains)
            select t).ToList();
        _foundMatches = foundMatches;
    }

    public async Task<string> SaveResult()
    {
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var checkTime = DateTime.Now.ToLocalTime().ToString(CultureInfo.CurrentCulture).Replace(':', '-');
        var path = Path.Combine(desktopDirectory, $"Scan result {checkTime}.json");
        using var fileStream = File.CreateText(path);
        await fileStream.WriteAsync(_json);
        await fileStream.FlushAsync();
        return $"Результат сканирования сохранен по пути \"{path}\".";
    }

    public class WorkResult
    {
        public WorkResult(string registryKey, string valueName, string value)
        {
            RegistryKey = registryKey;
            ValueName = valueName;
            Value = value;
        }

        [JsonProperty("Registry Key")] public string RegistryKey { get; set; }

        [JsonProperty("Value Name")] public string ValueName { get; set; }

        [JsonProperty("Value")] public string Value { get; set; }
    }
}