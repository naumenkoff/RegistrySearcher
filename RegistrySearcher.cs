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
            FindMatches(RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64));
            FindMatches(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64));
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
            try
            {
                using var regKey = registryKey.OpenSubKey(keyName);
                if (regKey is null) continue;
                var valueNames = regKey.GetValueNames();
                if (regKey.SubKeyCount is 0)
                    foreach (var valueName in valueNames)
                    {
                        var value = regKey.GetValue(valueName) as string;
                        if (valueName.ToLower().Contains(ProgramName) ||
                            value != null && value.ToLower().Contains(ProgramName))
                        {
                            var workResult = new WorkResult
                            {
                                RegistryKey = regKey.Name,
                                ValueName = valueName,
                                Value = value
                            };
                            FoundMatches.Add(workResult);
                        }
                    }
                else FindMatches(regKey);
            }
            catch
            {
                FailedKeyAccess++;
            }
    }

    private string GetResult(DateTime startTime)
    {
        SearchTime = DateTime.Now - startTime;
        Filter();
        Finished = true;
        Result = JsonConvert.SerializeObject(FoundMatches, Formatting.Indented)
                 + Environment.NewLine +
                 $"Найдено {FoundMatches.Count} вхождений за {SearchTime.TotalMilliseconds / 1000:F} сек. Количество необработанных ключей реестра: '{FailedKeyAccess}'.";
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
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"Результат сканирования {DateTime.Now.ToLocalTime().ToString(CultureInfo.CurrentCulture).Replace(':', '-')}.json");
        using var fileStream = File.CreateText(path);
        fileStream.Write(Result);
        fileStream.Flush();
        return $"Результат сканирования сохранен в \"{path}\".";
    }

    public class WorkResult
    {
        [JsonProperty("Registry Key")] public string RegistryKey { get; set; }

        [JsonProperty("Value Name")] public string ValueName { get; set; }

        [JsonProperty("Value")] public string Value { get; set; }
    }
}