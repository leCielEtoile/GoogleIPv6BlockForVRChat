// FirewallManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public async Task<bool> IsRuleExistsAsync()
        {
            try
            {
                // より簡潔で確実なコマンドに変更
                var command = $"if (Get-NetFirewallRule -DisplayName \\\"{RULE_NAME}\\\" -ErrorAction SilentlyContinue) {{ Write-Output 'EXISTS' }} else {{ Write-Output 'NOT_EXISTS' }}";
                var result = await ExecutePowerShellCommand(command);

                _logManager.Log($"ルール確認結果: {result.Trim()}");
                return result.Trim().Equals("EXISTS", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logManager.LogError("ファイアウォールルール確認エラー", ex);
                // エラー時は存在しないと仮定して処理を継続
                return false;
            }
        }

        public async Task EnableBlockingAsync(List<string> ipv6Ranges)
        {
            if (!ipv6Ranges.Any())
            {
                throw new InvalidOperationException("IPv6範囲が取得されていません。");
            }

            // IPv6範囲の数を制限（PowerShellコマンドライン長制限対策）
            var limitedRanges = ipv6Ranges.Take(50).ToList();
            _logManager.Log($"使用するIPv6範囲数: {limitedRanges.Count} / {ipv6Ranges.Count}");

            // 既存ルールをチェック
            if (await IsRuleExistsAsync())
            {
                _logManager.Log("ファイアウォールルールは既に存在します。削除してから再作成します。");
                await DisableBlockingAsync();
            }

            try
            {
                // 方法1: スクリプトファイル方式（元の方法）
                var scriptContent = CreateFirewallScript(limitedRanges, true);
                await ExecutePowerShellScriptAsAdmin(scriptContent);
                _logManager.Log($"ファイアウォールルールを作成しました。対象IP範囲数: {limitedRanges.Count}");
            }
            catch (Exception ex)
            {
                _logManager.LogError("スクリプトファイル方式失敗、直接コマンド方式を試行", ex);

                try
                {
                    // 方法2: 直接コマンド方式（代替手段）
                    await CreateFirewallRuleDirectly(limitedRanges);
                    _logManager.Log($"直接コマンド方式でファイアウォールルールを作成しました。対象IP範囲数: {limitedRanges.Count}");
                }
                catch (Exception directEx)
                {
                    _logManager.LogError("直接コマンド方式も失敗", directEx);
                    throw new InvalidOperationException($"ファイアウォールルールの作成に失敗しました: {ex.Message}", ex);
                }
            }
        }

        private async Task CreateFirewallRuleDirectly(List<string> ipv6Ranges)
        {
            // 直接コマンドでファイアウォールルールを作成（UAC最小化版）

            // 方法1: 単一コマンドで全範囲処理（推奨）
            try
            {
                await CreateSingleFirewallRuleDirectly(ipv6Ranges);
            }
            catch (Exception ex) when (ex.Message.Contains("コマンドライン") || ex.Message.Contains("長さ"))
            {
                _logManager.Log("コマンドライン長制限のため分割実行に切り替え");

                // 方法2: 複数コマンドに分割（UAC複数回）
                await CreateMultipleFirewallRulesDirectly(ipv6Ranges);
            }
        }

        private async Task CreateSingleFirewallRuleDirectly(List<string> ipv6Ranges)
        {
            var remoteAddresses = string.Join(",", ipv6Ranges);
            var command = $"New-NetFirewallRule -DisplayName \\\"{RULE_NAME}\\\" -Direction Outbound -Protocol Any -RemoteAddress \\\"{remoteAddresses}\\\" -Action Block -Enabled True";

            // コマンド長チェック
            if (command.Length > 7000)
            {
                throw new InvalidOperationException("コマンドライン長制限を超過しました");
            }

            _logManager.Log($"単一コマンド方式でファイアウォールルール作成（コマンド長: {command.Length}文字）");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas", // UAC昇格（1回のみ）
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            process.Start();
            await process.WaitForExitAsync();

            _logManager.Log($"単一コマンド終了コード: {process.ExitCode}");

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"単一コマンドによるファイアウォールルール作成に失敗しました。終了コード: {process.ExitCode}");
            }
        }

        private async Task CreateMultipleFirewallRulesDirectly(List<string> ipv6Ranges)
        {
            const int maxRangesPerRule = 20; // コマンドライン長制限を考慮
            var batches = new List<List<string>>();

            // IPv6範囲を分割
            for (int i = 0; i < ipv6Ranges.Count; i += maxRangesPerRule)
            {
                var batch = ipv6Ranges.Skip(i).Take(maxRangesPerRule).ToList();
                batches.Add(batch);
            }

            _logManager.Log($"複数コマンド方式: {batches.Count}個のルールを作成（UAC {batches.Count}回）");

            // 各バッチに対してUAC昇格が必要
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                var ruleName = batches.Count == 1 ? RULE_NAME : $"{RULE_NAME}_{i + 1}";
                var remoteAddresses = string.Join(",", batch);
                var command = $"New-NetFirewallRule -DisplayName \\\"{ruleName}\\\" -Direction Outbound -Protocol Any -RemoteAddress \\\"{remoteAddresses}\\\" -Action Block -Enabled True";

                _logManager.Log($"ルール作成 {i + 1}/{batches.Count}: {batch.Count}個のIP範囲");

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"{command}\"",
                    UseShellExecute = true,
                    Verb = "runas", // 各バッチごとにUAC昇格
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                process.Start();
                await process.WaitForExitAsync();

                _logManager.Log($"バッチ {i + 1} 終了コード: {process.ExitCode}");

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"バッチ {i + 1} のファイアウォールルール作成に失敗しました。終了コード: {process.ExitCode}");
                }

                // 連続UAC操作の間隔を空ける
                if (i < batches.Count - 1)
                {
                    await Task.Delay(1000); // 1秒待機
                }
            }
        }

        public async Task DisableBlockingAsync()
        {
            try
            {
                // 削除時も直接コマンド方式を使用
                await DisableBlockingDirectly();
            }
            catch (Exception ex)
            {
                _logManager.LogError("直接コマンド削除失敗、スクリプト方式で再試行", ex);

                // フォールバック: スクリプト方式
                var scriptContent = CreateFirewallScript(new List<string>(), false);
                await ExecutePowerShellScriptAsAdmin(scriptContent);
                _logManager.Log("スクリプト方式でファイアウォールルールを削除しました。");
            }
        }

        private async Task DisableBlockingDirectly()
        {
            // 複数のルール名パターンに対応した削除
            var rulePatterns = new[]
            {
                RULE_NAME,           // 単一ルール
                $"{RULE_NAME}_*"     // 複数ルール（ワイルドカード）
            };

            foreach (var pattern in rulePatterns)
            {
                var command = $"Get-NetFirewallRule -DisplayName \\\"{pattern}\\\" -ErrorAction SilentlyContinue | Remove-NetFirewallRule";

                _logManager.Log($"ルール削除コマンド実行: {pattern}");

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"{command}\"",
                    UseShellExecute = true,
                    Verb = "runas", // UAC昇格
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                process.Start();
                await process.WaitForExitAsync();

                _logManager.Log($"削除コマンド終了コード: {process.ExitCode}");

                // 削除は成功しなくても続行（ルールが存在しない場合もある）
            }
        }

        public async Task DisableBlockingAsync()
        {
            try
            {
                var scriptContent = CreateFirewallScript(new List<string>(), false);
                await ExecutePowerShellScriptAsAdmin(scriptContent);
                _logManager.Log("ファイアウォールルールを削除しました。");
            }
            catch (Exception ex)
            {
                _logManager.LogError("ファイアウォールルール削除エラー", ex);
                throw new InvalidOperationException($"ファイアウォールルールの削除に失敗しました: {ex.Message}", ex);
            }
        }

        private string CreateFirewallScript(List<string> ipv6Ranges, bool isCreate)
        {
            var script = new StringBuilder();

            script.AppendLine("# Google IPv6 Block Tool PowerShell Script");
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine("");

            if (isCreate)
            {
                script.AppendLine("try {");
                script.AppendLine($"    $ruleName = \"{RULE_NAME}\"");
                script.AppendLine("    $ipRanges = @(");

                foreach (var range in ipv6Ranges)
                {
                    script.AppendLine($"        \"{range}\",");
                }

                if (ipv6Ranges.Any())
                {
                    script.Length -= 3; // 最後のカンマと改行を削除
                    script.AppendLine();
                }

                script.AppendLine("    )");
                script.AppendLine("");
                script.AppendLine("    # 既存ルールを削除");
                script.AppendLine("    Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule");
                script.AppendLine("");
                script.AppendLine("    # 新しいルールを作成");
                script.AppendLine("    New-NetFirewallRule -DisplayName $ruleName -Direction Outbound -Protocol Any -RemoteAddress $ipRanges -Action Block -Enabled True");
                script.AppendLine("    Write-Output 'ファイアウォールルールが正常に作成されました'");
                script.AppendLine("}");
                script.AppendLine("catch {");
                script.AppendLine("    Write-Error \"エラー: $($_.Exception.Message)\"");
                script.AppendLine("    exit 1");
                script.AppendLine("}");
            }
            else
            {
                script.AppendLine("try {");
                script.AppendLine($"    $ruleName = \"{RULE_NAME}\"");
                script.AppendLine("    Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule");
                script.AppendLine("    Write-Output 'ファイアウォールルールが正常に削除されました'");
                script.AppendLine("}");
                script.AppendLine("catch {");
                script.AppendLine("    Write-Error \"エラー: $($_.Exception.Message)\"");
                script.AppendLine("    exit 1");
                script.AppendLine("}");
            }

            return script.ToString();
        }

        private async Task<string> ExecutePowerShellCommand(string command)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _logManager.Log($"PowerShellコマンド実行: {command}");

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            _logManager.Log($"PowerShell終了コード: {process.ExitCode}");
            _logManager.Log($"PowerShell出力: {output}");

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logManager.Log($"PowerShellエラー: {error}");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"PowerShell実行エラー (終了コード: {process.ExitCode}): {error}");
            }

            return output;
        }

        private async Task ExecutePowerShellScriptAsAdmin(string scriptContent)
        {
            // 一時スクリプトファイルを作成
            var tempPath = System.IO.Path.GetTempPath();
            var scriptFile = System.IO.Path.Combine(tempPath, $"GoogleIPv6Block_{Guid.NewGuid()}.ps1");

            try
            {
                await System.IO.File.WriteAllTextAsync(scriptFile, scriptContent, Encoding.UTF8);
                _logManager.Log($"一時スクリプトファイル作成: {scriptFile}");

                // スクリプト内容をログに記録（デバッグ用）
                _logManager.Log($"スクリプト内容の行数: {scriptContent.Split('\n').Length}");

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptFile}\"",
                    UseShellExecute = true,
                    Verb = "runas", // UAC昇格
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _logManager.Log("管理者権限でPowerShellスクリプトを実行");

                process.Start();
                await process.WaitForExitAsync();

                _logManager.Log($"PowerShellスクリプト終了コード: {process.ExitCode}");

                if (process.ExitCode != 0)
                {
                    // エラー時の診断情報を追加
                    await LogDiagnosticInfo(scriptFile);
                    throw new InvalidOperationException($"PowerShellスクリプトの実行に失敗しました。終了コード: {process.ExitCode}");
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ユーザーがUACをキャンセルした場合
                throw new UnauthorizedAccessException("管理者権限の取得がキャンセルされました。", ex);
            }
            catch (Exception ex)
            {
                _logManager.LogError("PowerShellスクリプト実行エラー", ex);
                throw;
            }
            finally
            {
                // 一時ファイルを削除
                try
                {
                    if (System.IO.File.Exists(scriptFile))
                    {
                        System.IO.File.Delete(scriptFile);
                        _logManager.Log("一時スクリプトファイルを削除");
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogError("一時ファイル削除エラー", ex);
                }
            }
        }

        private async Task LogDiagnosticInfo(string scriptFile)
        {
            try
            {
                _logManager.Log("=== 診断情報開始 ===");

                // 1. PowerShell実行ポリシーの確認
                var policyCheck = await ExecutePowerShellCommand("Get-ExecutionPolicy -List | Format-Table -AutoSize");
                _logManager.Log($"実行ポリシー: {policyCheck}");

                // 2. ファイアウォールサービスの状態確認
                var serviceCheck = await ExecutePowerShellCommand("Get-Service -Name 'MpsSvc' | Select-Object Status, StartType");
                _logManager.Log($"ファイアウォールサービス: {serviceCheck}");

                // 3. スクリプトファイルの存在確認
                if (System.IO.File.Exists(scriptFile))
                {
                    var fileInfo = new System.IO.FileInfo(scriptFile);
                    _logManager.Log($"スクリプトファイル存在: Yes, サイズ: {fileInfo.Length} bytes");
                }
                else
                {
                    _logManager.Log("スクリプトファイル存在: No");
                }

                // 4. 簡単なテストスクリプトの実行
                var testScript = "Write-Output 'PowerShell Test OK'";
                var testFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Test_{Guid.NewGuid()}.ps1");
                await System.IO.File.WriteAllTextAsync(testFile, testScript);

                using var testProcess = new Process();
                testProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{testFile}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                testProcess.Start();
                await testProcess.WaitForExitAsync();
                _logManager.Log($"テストスクリプト終了コード: {testProcess.ExitCode}");

                System.IO.File.Delete(testFile);

                _logManager.Log("=== 診断情報終了 ===");
            }
            catch (Exception ex)
            {
                _logManager.LogError("診断情報取得エラー", ex);
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