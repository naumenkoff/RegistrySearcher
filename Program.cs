using System;
using System.Collections.Generic;
using System.Timers;

namespace RegistrySearcher;

public static class Program
{
    private static readonly List<string> BlackFilter = new();

    private static void Main()
    {
        Console.Title = "Simple Registry Searcher";
        CheckHistory();
        StartSearch();
        ExitOrContinueExectuion();
    }

    private static void StartSearch()
    {
        var programName = GetUserVirtualInput("Введите что-то, чтобы найти кое-что > ", ConsoleColor.Red).ToLower();
        var registrySearcher = new RegistrySearcher(programName, BlackFilter);
        var multithreaded = GetUserPhysicalInput("Нажмите Y чтобы поиск был многопоточным, иначе он будет однопоточным > ", ConsoleKey.Y, ConsoleColor.Red);
        var excludedKeys = GetUserVirtualInput($"Введите нежелательные для поиска разделы реестра, сейчас их {BlackFilter.Count} > ", ConsoleColor.Red);
        AddExcludedKeys(excludedKeys);
        PrintElapsedTime(registrySearcher);
        var resultMessage = registrySearcher.DoWork(multithreaded ? RegistrySearcher.SearchType.MultiThread : RegistrySearcher.SearchType.SingleThread);
        PrintColoredLine(resultMessage, ConsoleColor.Green);
        SaveResultInFile(registrySearcher);
    }

    private static void AddExcludedKeys(string keys)
    {
        if (string.IsNullOrEmpty(keys)) return;
        var blackKeys = keys.Split(' ');
        foreach (var key in blackKeys)
        {
            if (BlackFilter.Contains(key)) return;
            BlackFilter.Add(key);
        }
    }

    private static void CheckHistory()
    {
        if (RegistrySearcher.History.Count is 0) return;
        if (GetUserPhysicalInput("Если вы хотите просмотреть историю найденных совпадений, нажмите Y > ", ConsoleKey.Y, ConsoleColor.Red) is false) return;
        foreach (var workResult in RegistrySearcher.History) PrintColoredLine(workResult, ConsoleColor.Green);
    }

    private static void ExitOrContinueExectuion()
    {
        if (GetUserPhysicalInput("Нажмите Enter что бы выйти или любую другую клавишу чтобы продолжить выполнение программы с начала > ", ConsoleKey.Enter, ConsoleColor.Red) is true) return;
        Main();
    }

    private static void ClearLine(int cursorTop)
    {
        Console.MoveBufferArea(0, cursorTop, Console.BufferWidth, 1, Console.BufferWidth, cursorTop, ' ', Console.ForegroundColor, Console.BackgroundColor);
    }

    private static void PrintElapsedTime(RegistrySearcher rs)
    {
        var t = new Timer { Interval = 25 };
        var cursorTop = Console.CursorTop;
        var startTime = DateTime.Now;
        t.Elapsed += (a, _) =>
        {
            Console.SetCursorPosition(0, cursorTop);
            ClearLine(cursorTop);
            if (rs.Finished)
            {
                (a as Timer)?.Close();
                return;
            }

            Console.Write($"Прошло {(DateTime.Now - startTime).TotalMilliseconds / 1000:F} сек.");
        };
        t.Start();
    }

    private static void SaveResultInFile(RegistrySearcher rs)
    {
        if (GetUserPhysicalInput("Если вы хотите сохранить полученный результат на рабочий стол, нажмите Y > ", ConsoleKey.Y, ConsoleColor.Red) is false) return;
        var result = rs.SaveResultIntoFile();
        PrintColoredLine(result, ConsoleColor.Red);
    }

    private static string GetUserVirtualInput(string message, ConsoleColor color)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(message);
        Console.ForegroundColor = color;
        var input = Console.ReadLine();
        Console.ResetColor();
        return input;
    }

    private static bool GetUserPhysicalInput(string message, ConsoleKey targetKey, ConsoleColor color)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(message);
        Console.ForegroundColor = color;
        var pressed = Console.ReadKey();
        Console.ResetColor();
        Console.WriteLine();
        return pressed.Key == targetKey;
    }

    private static void PrintColoredLine(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}