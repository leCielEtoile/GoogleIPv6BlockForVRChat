// LogManager.cs
using System;
using System.IO;
using System.Linq;

namespace GoogleIPv6BlockForVRChat
{
    public class LogManager
    {
        private readonly string _logDirectory;
        private readonly string _currentLogFile;

        public LogManager()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);

            var utcNow = DateTime.UtcNow;
            _currentLogFile = Path.Combine(_logDirectory, $"{utcNow:yyyyMMdd_HHmmss}_UTC.log");

            // ログローテーション（最大5ファイル保持）
            RotateLogs();

            Log("ログ開始");
        }

        public void Log(string message)
        {
            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {message}";

            try
            {
                File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // ログ書き込み失敗は無視（アプリの動作に影響させない）
            }
        }

        public void LogError(string message, Exception exception)
        {
            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] ERROR: {message}" + Environment.NewLine +
                          $"Exception: {exception.GetType().Name}" + Environment.NewLine +
                          $"Message: {exception.Message}" + Environment.NewLine +
                          $"StackTrace: {exception.StackTrace}" + Environment.NewLine;

            try
            {
                File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // ログ書き込み失敗は無視
            }
        }

        private void RotateLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "*_UTC.log")
                                      .Select(f => new FileInfo(f))
                                      .OrderByDescending(f => f.CreationTime)
                                      .ToList();

                // 5ファイルを超える場合は古いものを削除
                var filesToDelete = logFiles.Skip(4);
                foreach (var file in filesToDelete)
                {
                    file.Delete();
                }
            }
            catch
            {
                // ローテーション失敗は無視
            }
        }
    }
}