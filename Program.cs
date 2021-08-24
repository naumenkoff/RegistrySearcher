using System;
using System.Collections.Generic;
using System.Timers;

namespace RegistrySearcher
{
    public class Program
    {
        private static readonly List<string> BlackFilter = new() {"windowsselfhost", "edge", "google"};

        private static void Main()
        {
            Console.Title = "Simple Registry Searcher";
            CheckHistory();
            StartSearch();
            ExitOrContinueExectuion();
        }

        private static void StartSearch()
        {
            var programName = GetUserVirtualInput("Введите что-то, чтобы найти кое-что > ",
                ConsoleColor.Red).ToLower();
            var registrySearcher = new RegistrySearcher(programName, BlackFilter);
            PrintElapsedTime(registrySearcher);
            var resultMessage = registrySearcher.DoWork(RegistrySearcher.SearchType.MultiThread);
            PrintColoredLine(resultMessage, ConsoleColor.Green);
            SaveResultInFile(registrySearcher);
        }

        private static void CheckHistory()
        {
            if (RegistrySearcher.History.Count is 0) return;
            if (GetUserPhysicalInput("Если вы хотите просмотреть историю найденных совпадений, нажмите Y > ",
                ConsoleKey.Y) is false) return;
            foreach (var workResult in RegistrySearcher.History) PrintColoredLine(workResult, ConsoleColor.Green);
        }

        private static void ExitOrContinueExectuion()
        {
            if (GetUserPhysicalInput(
                "Нажмите Enter что бы выйти или любую другую клавишу чтобы продолжить выполнение программы с начала > ",
                ConsoleKey.Enter) is true) return;
            Main();
        }

        private static void ClearLine(int cursorTop)
        {
            Console.MoveBufferArea(0, cursorTop, Console.BufferWidth, 1, Console.BufferWidth, cursorTop, ' ',
                Console.ForegroundColor, Console.BackgroundColor);
        }

        private static void PrintElapsedTime(RegistrySearcher rs)
        {
            var t = new Timer {Interval = 25};
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
            if (GetUserPhysicalInput("Если вы хотите сохранить полученный результат на рабочий стол, нажмите Y > ",
                ConsoleKey.Y) is false) return;
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

        private static bool GetUserPhysicalInput(string message, ConsoleKey targetKey)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(message);
            var pressed = Console.ReadKey();
            Console.WriteLine();
            Console.ResetColor();
            return pressed.Key == targetKey;
        }

        private static void PrintColoredLine(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}