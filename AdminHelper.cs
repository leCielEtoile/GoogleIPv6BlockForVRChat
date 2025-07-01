// AdminHelper.cs
using System;
using System.Diagnostics;
using System.Security.Principal;

namespace GoogleIPv6BlockForVRChat
{
    public static class AdminHelper
    {
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static void RestartAsAdministrator()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var mainModule = currentProcess.MainModule;

                if (mainModule?.FileName == null)
                {
                    throw new InvalidOperationException("プロセスの実行ファイルパスを取得できませんでした。");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = mainModule.FileName,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("管理者権限での再起動に失敗しました。", ex);
            }
        }
    }
}