using System;
using System.IO;

namespace ShiftSchedulerMVC.Services
{
    public static class Logger
    {
        private static string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        public static string CreateLogFile()
        {
            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            string fileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = Path.Combine(logDirectory, fileName);
            File.WriteAllText(path, $"Log start: {DateTime.Now}\n");
            return path;
        }

        public static void Append(string filePath, string message)
        {
            File.AppendAllText(filePath, message + Environment.NewLine);
        }
    }
}
