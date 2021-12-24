using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace RegistrySearcher;

public class RegistrySearcher
{
    public enum SearchType
    {
        SingleThread,
        MultiThread
    }

    public static List<string> History = new();

    public RegistrySearcher(string programName, IReadOnlyCollection<string> blackList)
    {
        ProgramName = programName;
        BlackList = blackList;
        FoundMatches = new List<WorkResult>();
    }

    private IReadOnlyCollection<string> BlackList { get; }
    private string ProgramName { get; }
    private List<WorkResult> FoundMatches { get; set; }
    private TimeSpan SearchTime { get; set; }
    private int FailedKeyAccess { get; set; }
    public bool Finished { get; set; }
    public string Result { get; set; }

    public string DoWork(SearchType searchType)
    {
        return searchType == SearchType.SingleThread ? StartSinglethreadedSearch() : StartMultithreadedSearch();
    }

    private string StartSinglethreadedSearch()
    {
        var startTime = DateTime.Now;
        var registrySearcher = new Thread(() =>
        {
            using var currentUser64 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            FindMatches(currentUser64);
            FindMatches(localMachine64);
        });
        registrySearcher.Start();
        registrySearcher.Join();
        return GetResult(startTime);
    }

    private string StartMultithreadedSearch()
    {
        var startTime = DateTime.Now;
        CreateSearchingThread(RegistryHive.CurrentUser);
        CreateSearchingThread(RegistryHive.LocalMachine).Join();
        return GetResult(startTime);
    }

    private Thread CreateSearchingThread(RegistryHive registryHive)
    {
        var searchThread = new Thread(() =>
        {
            using var registryKey64 = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry64);
            FindMatches(registryKey64);
        });
        searchThread.Start();
        return searchThread;
    }

    private void FindMatches(RegistryKey registryKey)
    {
        foreach (var keyName in registryKey.GetSubKeyNames())
        {
            try
            {
                using var regKey = registryKey.OpenSubKey(keyName);
                if (regKey is null) return;
                var valueNames = regKey.GetValueNames();
                if (regKey.SubKeyCount is 0)
                {
                    FindValueMatches(regKey, valueNames);
                }
                else FindMatches(regKey);
            }
            catch
            {
                FailedKeyAccess++;
            }
        }
    }

    private void FindValueMatches(RegistryKey regKey, IEnumerable<string> valueNames)
    {
        foreach (var valueName in valueNames)
        {
            var value = regKey.GetValue(valueName) as string;
            if (valueName.ToLower().Contains(ProgramName) || value != null && value.ToLower().Contains(ProgramName))
            {
                FoundMatches.Add(new WorkResult(regKey.Name, valueName, value));
            }
        }
    }

    private string GetResult(DateTime startTime)
    {
        SearchTime = DateTime.Now - startTime;
        Filter();
        Finished = true;
        Result = JsonConvert.SerializeObject(FoundMatches, Formatting.Indented) + Environment.NewLine + $"Найдено {FoundMatches.Count} вхождений за {SearchTime.TotalMilliseconds / 1000:F} сек. Количество необработанных ключей реестра: '{FailedKeyAccess}'.";
        History.Add(Result);
        return Result;
    }

    private void Filter()
    {
        var foundMatches = (
            from t in FoundMatches
            let registryKey = t.RegistryKey.ToLower()
            where !BlackList.Any(registryKey.Contains)
            select t).ToList();
        FoundMatches.Clear();
        FoundMatches = foundMatches;
    }

    public string SaveResultIntoFile()
    {
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var checkTime = DateTime.Now.ToLocalTime().ToString(CultureInfo.CurrentCulture).Replace(':', '-');
        var path = Path.Combine(desktopDirectory, $"Результат сканирования {checkTime}.json");
        using var fileStream = File.CreateText(path);
        fileStream.Write(Result);
        fileStream.Flush();
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