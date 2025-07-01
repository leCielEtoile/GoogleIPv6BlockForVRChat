// GoogleIPService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;

namespace GoogleIPv6BlockForVRChat
{
    public class GoogleIPService : IDisposable
    {
        private readonly LogManager _logManager;
        private readonly HttpClient _httpClient;
        private List<string> _ipv6Ranges;
        private bool _disposed = false;

        public GoogleIPService(LogManager logManager)
        {
            _logManager = logManager;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _ipv6Ranges = new List<string>();
        }

        public async Task LoadIPRangesAsync()
        {
            const string url = "https://www.gstatic.com/ipranges/goog.json";

            try
            {
                _logManager.Log("Google IP範囲の取得を開始");

                var response = await _httpClient.GetStringAsync(url);
                var ipv6Ranges = ParseIPv6Ranges(response);

                if (ipv6Ranges.Count == 0)
                {
                    throw new InvalidOperationException("有効なIPv6範囲が見つかりませんでした。");
                }

                _ipv6Ranges = ipv6Ranges;
                _logManager.Log($"IPv6範囲の取得完了: {_ipv6Ranges.Count}件");
            }
            catch (HttpRequestException ex)
            {
                _logManager.LogError("ネットワークエラー", ex);
                throw new InvalidOperationException("Google IP範囲の取得に失敗しました。インターネット接続を確認してください。", ex);
            }
            catch (JsonException ex)
            {
                _logManager.LogError("JSON解析エラー", ex);
                throw new InvalidOperationException("取得したデータの解析に失敗しました。", ex);
            }
            catch (Exception ex)
            {
                _logManager.LogError("IP範囲取得エラー", ex);
                throw;
            }
        }

        private List<string> ParseIPv6Ranges(string jsonContent)
        {
            var ipv6Ranges = new List<string>();

            try
            {
                using var jsonDocument = JsonDocument.Parse(jsonContent);

                if (jsonDocument.RootElement.TryGetProperty("prefixes", out var prefixes))
                {
                    foreach (var prefix in prefixes.EnumerateArray())
                    {
                        if (prefix.TryGetProperty("ipv6Prefix", out var ipv6Prefix))
                        {
                            var range = ipv6Prefix.GetString();
                            if (!string.IsNullOrWhiteSpace(range) && IsValidIPv6CIDR(range))
                            {
                                ipv6Ranges.Add(range);
                                _logManager.Log($"IPv6範囲追加: {range}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError("JSON解析詳細エラー", ex);
                throw new JsonException("JSONの解析中にエラーが発生しました。", ex);
            }

            return ipv6Ranges;
        }

        public List<string> GetIPv6Ranges()
        {
            return new List<string>(_ipv6Ranges);
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

                // IPv6アドレス部分の検証
                if (!IPAddress.TryParse(parts[0], out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                    return false;

                // プレフィックス長の検証
                if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 128)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}