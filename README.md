# Google IPv6 Block Tool for VRChat

VRChatなどのアプリケーションでの通信挙動を安定化させるため、GoogleサービスへのIPv6通信のみをブロックし、IPv4通信を維持するWindows用GUIツールです。

## 機能

- **GoogleサービスのIPv6通信を選択的にブロック**
- **IPv4通信は維持**
- **シンプルなワンクリック操作**
- **IPv6ブロック状況の確認機能**
- **起動時管理者権限要求**（操作時UAC不要）
- **自動ログ記録**（最大5ファイル保持）
- **システムトレイ対応**

## システム要件

- **OS**: Windows 7/8/8.1/10/11 (x64)
- **Runtime**: .NET 8.0 Runtime

## ダウンロード

[Releases](https://github.com/leCielEtoile/GoogleIPv6BlockForVRChat/releases)から最新版をダウンロードしてください。

### リリースについて

- **リリース版**: `GoogleIPv6BlockForVRChat.exe`（.NET Runtime不要）
- **軽量版**: `GoogleIPv6BlockForVRChatLite.exe`（.NET 8.0 Runtime が別途必要）

## 使用方法

### 基本操作

1. **アプリケーション起動**
   ```
   GoogleIPv6BlockForVRChat.exe を実行
   ```

2. **管理者権限の承認**
   - UAC昇格ダイアログで「はい」をクリック
   - または右クリック「管理者として実行」

3. **初期化**
   - Google IP範囲の自動取得まで待機（数秒）

4. **IPv6ブロック操作**
   - 「有効化」: GoogleサービスへのIPv6通信をブロック
   - 「無効化」: ブロックを解除
   - 「IPv6ブロック確認」: 現在のブロック状況をテスト

### トラブルシューティング

- **初期化エラー**: インターネット接続を確認
- **操作失敗**: ログファイル（`Logs`フォルダ内）を確認
- **ブロック確認失敗**: ファイアウォールサービスの状態を確認

## 技術仕様

### アーキテクチャ

- **.NET 8.0 Windows Forms**
- **System.Text.Json**（JSON解析）
- **Windows PowerShell**（ファイアウォール操作）
- **フラットデザインUI**

### セキュリティ

- **IPv6 CIDR記法バリデーション**
- **起動時管理者権限要求**
- **エラー時の安全な処理中断**

### ログ機能

- **自動ログ記録**: UTC時刻形式（例: `20250701_143022_UTC.log`）
- **ローテーション**: 最大5ファイル保持
- **詳細レベル**: 操作内容、エラー情報、診断データ

## 開発

### 開発環境

- **IDE**: Visual Studio 2022
- **SDK**: .NET 8.0 SDK
- **言語**: C#
- **UI**: Windows Forms

### ビルド方法

```bash
# 開発ビルド
dotnet build -c Debug

# リリースビルド（.NET同梱）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# リリースビルド
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### プロジェクト構成

```
GoogleIPv6BlockForVRChat/
├── Program.cs              # エントリーポイント、管理者権限チェック
├── MainForm.cs            # メインGUI、ユーザーインターフェース
├── FirewallManager.cs     # ファイアウォールルール操作
├── GoogleIPService.cs     # Google IP範囲取得・CIDR検証
├── LogManager.cs          # ログ管理（UTC時刻、ローテーション）
├── ConnectionTester.cs    # IPv6ブロック確認（実接続テスト）
├── AdminHelper.cs         # 管理者権限処理
├── app.manifest          # UAC設定（requireAdministrator）
└── GoogleIPv6BlockForVRChat.csproj
```

## 貢献

### バグレポート

Issues にて以下の情報と共にお知らせください：

- **OS バージョン**
- **エラーメッセージ**
- **ログファイル内容**（`Logs`フォルダ内）
- **再現手順**

### コミットメッセージ

[Conventional Commits](https://www.conventionalcommits.org/) 形式を使用：

- `feat:` 新機能
- `fix:` バグ修正
- `docs:` ドキュメント
- `style:` コードスタイル
- `refactor:` リファクタリング
- `test:` テスト
- `chore:` その他

## ライセンス

BSD 3-Clause License

## 免責事項

このソフトウェアは「現状のまま」提供され、明示的または暗示的な保証はありません。
使用に伴うリスクはすべて使用者が負うものとします。

---

**Google IPv6 Block Tool for VRChat** - VRChatユーザーのためのネットワーク最適化ツール