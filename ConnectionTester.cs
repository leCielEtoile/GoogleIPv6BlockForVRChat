// ConnectionTester.cs - リファクタリング版
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace GoogleIPv6BlockForVRChat
{
    public class ConnectionTester
    {
        private readonly LogManager _logManager;
        private readonly FirewallManager _firewallManager;

        public ConnectionTester(LogManager logManager, FirewallManager firewallManager)
        {
            _logManager = logManager;
            _firewallManager = firewallManager;
        }

        public async Task<bool> TestGoogleIPv6BlockAsync()
        {
            try
            {
                _logManager.Log("IPv6ブロック確認テスト開始");

                // ファイアウォールルールの確認
                var ruleExists = await _firewallManager.IsRuleExistsAsync();
                _logManager.Log($"ファイアウォールルール存在: {ruleExists}");

                if (!ruleExists)
                {
                    _logManager.Log("ファイアウォールルールが存在しません");
                    return false;
                }

                // GoogleのIPv6アドレスを取得
                var addresses = await Dns.GetHostAddressesAsync("google.com");
                var ipv6Addresses = addresses.Where(addr => addr.AddressFamily == AddressFamily.InterNetworkV6).ToList();

                if (!ipv6Addresses.Any())
                {
                    _logManager.Log("GoogleのIPv6アドレスが見つかりませんでした");
                    throw new InvalidOperationException("GoogleのIPv6アドレスが見つかりませんでした。");
                }

                _logManager.Log($"取得したIPv6アドレス数: {ipv6Addresses.Count}");

                // 最初のIPv6アドレスに対して接続テスト
                var targetAddress = ipv6Addresses.First();
                _logManager.Log($"テスト対象アドレス: {targetAddress}");

                // 接続テストを実行
                var connectionBlocked = await TestConnectionAsync(targetAddress);

                _logManager.Log($"接続テスト結果: {(connectionBlocked ? "ブロック済み" : "ブロックなし")}");

                // IPv6範囲とのマッチング確認
                await CheckIPv6RangeMatching(targetAddress);

                if (connectionBlocked)
                {
                    _logManager.Log("IPv6接続がブロックされています（正常）");
                }
                else
                {
                    _logManager.Log("IPv6接続がブロックされていません（異常）");
                }

                return connectionBlocked;
            }
            catch (Exception ex)
            {
                _logManager.LogError("IPv6ブロック確認テストエラー", ex);
                throw;
            }
        }

        private async Task CheckIPv6RangeMatching(IPAddress targetAddress)
        {
            try
            {
                _logManager.Log("=== IPv6範囲マッチング確認開始 ===");

                var googleIPService = new GoogleIPService(_logManager);
                await googleIPService.LoadIPRangesAsync();
                var ipv6Ranges = googleIPService.GetIPv6Ranges();

                _logManager.Log($"設定されているIPv6範囲数: {ipv6Ranges.Count}");

                bool foundMatch = false;
                foreach (var range in ipv6Ranges.Take(5)) // 最初の5つだけチェック（ログ量削減）
                {
                    if (IsIPv6InRange(targetAddress, range))
                    {
                        _logManager.Log($"マッチ発見: {targetAddress} は {range} に含まれます");
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    _logManager.Log($"警告: {targetAddress} は確認した範囲にマッチしません");
                }

                _logManager.Log("=== IPv6範囲マッチング確認終了 ===");
            }
            catch (Exception ex)
            {
                _logManager.LogError("IPv6範囲マッチング確認エラー", ex);
            }
        }

        private bool IsIPv6InRange(IPAddress targetAddress, string cidr)
        {
            try
            {
                var parts = cidr.Split('/');
                if (parts.Length != 2) return false;

                if (!IPAddress.TryParse(parts[0], out var networkAddress)) return false;
                if (!int.TryParse(parts[1], out var prefixLength)) return false;

                var targetBytes = targetAddress.GetAddressBytes();
                var networkBytes = networkAddress.GetAddressBytes();

                int bytesToCheck = prefixLength / 8;
                int bitsToCheck = prefixLength % 8;

                for (int i = 0; i < bytesToCheck; i++)
                {
                    if (targetBytes[i] != networkBytes[i])
                        return false;
                }

                if (bitsToCheck > 0 && bytesToCheck < targetBytes.Length)
                {
                    int mask = 0xFF << (8 - bitsToCheck);
                    if ((targetBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestConnectionAsync(IPAddress targetAddress)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveTimeout = 3000; // タイムアウト短縮
                socket.SendTimeout = 3000;

                var connectTask = socket.ConnectAsync(targetAddress, 80);
                var timeoutTask = Task.Delay(3000); // タイムアウト短縮

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logManager.Log("接続タイムアウト - ブロックされている可能性が高い");
                    return true;
                }

                if (connectTask.IsCompletedSuccessfully)
                {
                    _logManager.Log("接続成功 - ブロックされていない");
                    return false;
                }

                _logManager.Log("接続エラー - ブロックされている可能性が高い");
                return true;
            }
            catch (SocketException ex)
            {
                _logManager.Log($"ソケットエラー: {ex.Message} - ブロックされている");
                return true;
            }
            catch (Exception ex)
            {
                _logManager.Log($"その他のエラー: {ex.Message} - ブロックされている可能性が高い");
                return true;
            }
        }
    }
}