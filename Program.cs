// Program.cs
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace GoogleIPv6BlockForVRChat
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 管理者権限チェック - 起動時に昇格
            if (!IsRunningAsAdministrator())
            {
                var result = MessageBox.Show(
                    "このアプリケーションはWindowsファイアウォール操作のため管理者権限が必要です。\n" +
                    "管理者として再起動しますか？",
                    "管理者権限が必要",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        RestartAsAdministrator();
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"管理者権限での起動に失敗しました:\n{ex.Message}",
                            "起動エラー",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "管理者権限なしでは一部機能が制限されます。",
                        "制限モード",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            // アプリケーションの多重起動防止
            bool createdNew;
            using var mutex = new Mutex(true, "GoogleIPv6BlockForVRChat", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("アプリケーションは既に実行中です。", "Google IPv6 Block Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.Run(new MainForm());
        }

        private static bool IsRunningAsAdministrator()
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

        private static void RestartAsAdministrator()
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
                Verb = "runas" // UAC昇格
            };

            Process.Start(processInfo);
            Environment.Exit(0);
        }
    }
}