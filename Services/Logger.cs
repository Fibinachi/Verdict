// Services/Logger.cs
using System;
using System.IO;

namespace Verdict.Services;

public static class Logger
{
    private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VerdictSimulation.log");
    private static readonly object Lock = new object();

    public static void Log(string message)
    {
        lock (Lock)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n");
            }
            catch (Exception ex)
            {
                // Fallback: If logging to file fails, we can't do much else.
                // In a real app, you might log to Event Log or another persistent store.
                Console.WriteLine($"[Logger Error] Failed to write to log file: {ex.Message}");
            }
        }
    }

    public static void LogException(Exception ex, string context = "")
    {
        string message = string.IsNullOrEmpty(context) ? "" : $"Context: {context}";
        Log($"EXCEPTION: {ex.Message}\nStackTrace: {ex.StackTrace}\n{message}");
    }
}