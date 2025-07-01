// FirewallManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Text;

namespace GoogleIPv6BlockForVRChat
{
    public class FirewallManager
    {
        private readonly LogManager _logManager;
        private const string RULE_NAME = "Google IPv6 Block For VRChat";

        public FirewallManager(LogManager logManager)
        {
            _logManager = logManager;
        }

        public bool HasAdministratorPrivileges => IsRunningAsAdministrator();

        public async Task<bool> IsRuleExistsAsync()
        {
            _logManager.Log("🔍 ファイアウォールルール存在確認");
            try
            {
                var result = await ExecutePowerShell($"if (Get-NetFirewallRule -DisplayName '{RULE_NAME}' -ErrorAction SilentlyContinue) {{ Write-Output 'EXISTS' }} else {{ Write-Output 'NOT_EXISTS' }}");
                var exists = result.Trim().Equals("EXISTS", StringComparison.OrdinalIgnoreCase);
                _logManager.Log($"✅ ルール確認完了: {(exists ? "存在" : "なし")}");
                return exists;
            }
            catch (Exception ex)
            {
                _logManager.LogError("❌ ルール確認エラー", ex);
                return false;
            }
        }

        public async Task EnableBlockingAsync(List<string> ipv6Ranges)
        {
            _logManager.Log($"🚀 IPv6ブロック有効化開始: {ipv6Ranges.Count}個の範囲");

            if (!ipv6Ranges.Any())
                throw new InvalidOperationException("IPv6範囲が取得されていません。");

            // 既存ルール削除
            if (await IsRuleExistsAsync())
            {
                await DisableBlockingAsync();
                await Task.Delay(1000);
            }

            // 有効な範囲のフィルタリング
            var validRanges = ipv6Ranges.Where(IsValidIPv6CIDR).ToList();
            if (!validRanges.Any())
                throw new InvalidOperationException("有効なIPv6範囲がありません");

            _logManager.Log($"📊 有効範囲: {validRanges.Count}個");

            // ルール作成
            var addresses = string.Join("', '", validRanges);
            var script = $@"
try {{
    $addresses = @('{addresses}')
    New-NetFirewallRule -Name '{RULE_NAME}' -DisplayName '{RULE_NAME}' -Direction Outbound -Protocol Any -RemoteAddress $addresses -Action Block -Enabled True
    Write-Output 'SUCCESS'
}} catch {{
    Write-Error $_.Exception.Message
    exit 1
}}";

            await ExecutePowerShell(script, true);
            _logManager.Log("🎉 IPv6ブロック有効化完了");
        }

        public async Task DisableBlockingAsync()
        {
            _logManager.Log("🗑️ IPv6ブロック無効化開始");
            await ExecutePowerShell($"Get-NetFirewallRule -DisplayName '{RULE_NAME}' -ErrorAction SilentlyContinue | Remove-NetFirewallRule", true);
            _logManager.Log("🎉 IPv6ブロック無効化完了");
        }

        private async Task<string> ExecutePowerShell(string command, bool requiresAdmin = false)
        {
            var processType = requiresAdmin ? "管理者権限" : "標準権限";
            _logManager.Log($"⚡ PowerShell実行 ({processType})");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"{command}\"",
                UseShellExecute = requiresAdmin,
                RedirectStandardOutput = !requiresAdmin,
                RedirectStandardError = !requiresAdmin,
                CreateNoWindow = true,
                StandardOutputEncoding = requiresAdmin ? null : Encoding.UTF8,
                StandardErrorEncoding = requiresAdmin ? null : Encoding.UTF8
            };

            if (requiresAdmin)
            {
                process.StartInfo.Verb = "runas";
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }

            var startTime = DateTime.UtcNow;
            process.Start();

            string output = "";
            if (!requiresAdmin)
            {
                output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(error))
                    _logManager.Log($"⚠️ エラー: {error.Trim()}");
            }

            await process.WaitForExitAsync();
            var duration = DateTime.UtcNow - startTime;

            _logManager.Log($"✅ PowerShell完了: 終了コード={process.ExitCode}, 時間={duration.TotalSeconds:F1}秒");

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"PowerShell実行エラー (終了コード: {process.ExitCode})");

            return output;
        }

        private bool IsValidIPv6CIDR(string cidr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cidr) || !cidr.Contains('/'))
                    return false;

                var parts = cidr.Split('/');
                if (parts.Length != 2)
                    return false;

                return IPAddress.TryParse(parts[0], out var address) &&
                       address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                       int.TryParse(parts[1], out var prefixLength) &&
                       prefixLength >= 0 && prefixLength <= 128;
            }
            catch
            {
                return false;
            }
        }

        private bool IsRunningAsAdministrator()
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
    }
}