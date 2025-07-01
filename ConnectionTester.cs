// ConnectionTester.cs
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

        public ConnectionTester(LogManager logManager)
        {
            _logManager = logManager;
        }

        public async Task<bool> TestGoogleIPv6BlockAsync()
        {
            try
            {
                _logManager.Log("IPv6ブロック確認テスト開始");

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

                bool connectionBlocked = await TestConnectionAsync(targetAddress);

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

        private async Task<bool> TestConnectionAsync(IPAddress targetAddress)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveTimeout = 5000;
                socket.SendTimeout = 5000;

                var connectTask = socket.ConnectAsync(targetAddress, 80);
                var timeoutTask = Task.Delay(5000);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // タイムアウト = 接続がブロックされている
                    return true;
                }

                if (connectTask.IsCompletedSuccessfully)
                {
                    // 接続成功 = ブロックされていない
                    return false;
                }

                // 接続エラー = ブロックされている可能性が高い
                return true;
            }
            catch (SocketException)
            {
                // ソケットエラー = ブロックされている
                return true;
            }
            catch (Exception)
            {
                // その他のエラー = ブロックされている可能性が高い
                return true;
            }
        }
    }
}