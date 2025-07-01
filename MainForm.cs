// MainForm.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoogleIPv6BlockForVRChat
{
    public partial class MainForm : Form
    {
        private readonly FirewallManager _firewallManager;
        private readonly GoogleIPService _googleIPService;
        private readonly LogManager _logManager;
        private readonly ConnectionTester _connectionTester;

        private Button _toggleButton = null!;
        private Button _testButton = null!;
        private Label _statusLabel = null!;
        private Label _messageLabel = null!;
        private Label _privilegeLabel = null!;
        private NotifyIcon _notifyIcon = null!;

        private bool _isBlocking = false;
        private bool _isInitialized = false;

        public MainForm()
        {
            InitializeComponent();

            _logManager = new LogManager();
            _firewallManager = new FirewallManager(_logManager);
            _googleIPService = new GoogleIPService(_logManager);
            _connectionTester = new ConnectionTester(_logManager, _firewallManager);

            InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Google IPv6 Block Tool for VRChat";
            this.Size = new Size(400, 320);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);

            // 権限状態表示ラベル
            _privilegeLabel = new Label
            {
                Text = "権限確認中...",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(20, 10),
                Size = new Size(350, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_privilegeLabel);

            // ステータスラベル
            _statusLabel = new Label
            {
                Text = "初期化中...",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70),
                Location = new Point(20, 40),
                Size = new Size(350, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(_statusLabel);

            // トグルボタン
            _toggleButton = new Button
            {
                Text = "有効化",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(120, 40),
                Location = new Point(140, 90),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _toggleButton.FlatAppearance.BorderSize = 0;
            _toggleButton.Click += ToggleButton_Click;
            this.Controls.Add(_toggleButton);

            // 確認ボタン
            _testButton = new Button
            {
                Text = "IPv6ブロック確認",
                Font = new Font("Segoe UI", 9),
                Size = new Size(140, 35),
                Location = new Point(130, 150),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _testButton.FlatAppearance.BorderSize = 0;
            _testButton.Click += TestButton_Click;
            this.Controls.Add(_testButton);

            // メッセージラベル
            _messageLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(20, 200),
                Size = new Size(350, 80),
                TextAlign = ContentAlignment.TopCenter
            };
            this.Controls.Add(_messageLabel);

            // システムトレイアイコン
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "Google IPv6 Block Tool",
                Visible = true
            };
            _notifyIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };

            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                    _notifyIcon.ShowBalloonTip(2000, "Google IPv6 Block Tool",
                        "アプリケーションはシステムトレイに最小化されました", ToolTipIcon.Info);
                }
            };
        }

        private async void InitializeAsync()
        {
            try
            {
                _logManager.Log("アプリケーション初期化開始");

                // 管理者権限の表示を更新
                UpdatePrivilegeStatus();

                if (!_firewallManager.HasAdministratorPrivileges)
                {
                    _messageLabel.Text = "管理者権限がありません。一部機能が制限されます。";
                    _messageLabel.ForeColor = Color.Orange;
                    _logManager.Log("管理者権限なしで動作中");
                    return;
                }

                // 現在のファイアウォールルール状態をチェック
                _isBlocking = await _firewallManager.IsRuleExistsAsync();

                // Google IP範囲を取得
                await _googleIPService.LoadIPRangesAsync();

                _isInitialized = true;
                UpdateUI();

                _messageLabel.Text = "初期化が完了しました。";
                _messageLabel.ForeColor = Color.FromArgb(100, 100, 100);
                _logManager.Log("アプリケーション初期化完了");
            }
            catch (Exception ex)
            {
                _messageLabel.Text = $"初期化エラー: {ex.Message}";
                _messageLabel.ForeColor = Color.Red;
                _logManager.LogError("初期化エラー", ex);
            }
        }

        private void UpdatePrivilegeStatus()
        {
            if (_firewallManager.HasAdministratorPrivileges)
            {
                _privilegeLabel.Text = "✓ 管理者権限で実行中";
                _privilegeLabel.ForeColor = Color.Green;
            }
            else
            {
                _privilegeLabel.Text = "⚠ 制限モード（管理者権限なし）";
                _privilegeLabel.ForeColor = Color.Orange;
            }
        }

        private async void ToggleButton_Click(object? sender, EventArgs e)
        {
            if (!_firewallManager.HasAdministratorPrivileges)
            {
                MessageBox.Show(
                    "ファイアウォール操作には管理者権限が必要です。\n" +
                    "アプリケーションを管理者として再起動してください。",
                    "権限不足",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!_isInitialized)
            {
                MessageBox.Show("初期化が完了していません。", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _toggleButton.Enabled = false;
                _messageLabel.Text = "処理中...";
                _messageLabel.ForeColor = Color.FromArgb(100, 100, 100);

                if (_isBlocking)
                {
                    // 無効化
                    await _firewallManager.DisableBlockingAsync();
                    _isBlocking = false;
                    _messageLabel.Text = "IPv6ブロックを無効化しました。";
                }
                else
                {
                    // 有効化
                    var ipRanges = _googleIPService.GetIPv6Ranges();
                    await _firewallManager.EnableBlockingAsync(ipRanges);
                    _isBlocking = true;
                    _messageLabel.Text = "IPv6ブロックを有効化しました。";
                }

                UpdateUI();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("管理者権限が不足しています。アプリケーションを管理者として再起動してください。", "権限エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _messageLabel.Text = "権限エラーが発生しました。";
                _messageLabel.ForeColor = Color.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作に失敗しました: {ex.Message}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _messageLabel.Text = $"エラー: {ex.Message}";
                _messageLabel.ForeColor = Color.Red;
                _logManager.LogError("操作エラー", ex);
            }
            finally
            {
                _toggleButton.Enabled = true;
            }
        }

        private async void TestButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _testButton.Enabled = false;
                _messageLabel.Text = "接続テスト中...";
                _messageLabel.ForeColor = Color.FromArgb(100, 100, 100);

                bool isBlocked = await _connectionTester.TestGoogleIPv6BlockAsync();

                if (isBlocked)
                {
                    _messageLabel.Text = "✓ IPv6ブロックが正常に機能しています。";
                    _messageLabel.ForeColor = Color.Green;
                }
                else
                {
                    _messageLabel.Text = "⚠ IPv6ブロックが機能していません。";
                    _messageLabel.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                _messageLabel.Text = $"テストエラー: {ex.Message}";
                _messageLabel.ForeColor = Color.Red;
                _logManager.LogError("接続テストエラー", ex);
            }
            finally
            {
                _testButton.Enabled = true;
            }
        }

        private void UpdateUI()
        {
            var hasPrivileges = _firewallManager.HasAdministratorPrivileges;

            if (_isBlocking)
            {
                _statusLabel.Text = "IPv6ブロック: 有効";
                _statusLabel.ForeColor = Color.Green;
                _toggleButton.Text = "無効化";
                _toggleButton.BackColor = Color.FromArgb(244, 67, 54);
                _testButton.Enabled = true;
            }
            else
            {
                _statusLabel.Text = "IPv6ブロック: 無効";
                _statusLabel.ForeColor = Color.Red;
                _toggleButton.Text = "有効化";
                _toggleButton.BackColor = Color.FromArgb(76, 175, 80);
                _testButton.Enabled = false;
            }

            _toggleButton.Enabled = _isInitialized && hasPrivileges;

            // 管理者権限がない場合はボタンを無効化
            if (!hasPrivileges)
            {
                _toggleButton.BackColor = Color.Gray;
                _toggleButton.Text = "権限不足";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon?.Dispose();
                _googleIPService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}