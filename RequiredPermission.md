# 必要な権限

Botが動作するために、Discord Developer Portalとサーバー側で必要になる設定の一覧です。
コマンドを追加・変更したときは、このファイルも更新してください。

## まとめ(現時点で必要なもの)

### OAuth2 Scopes(招待URL)

| Scope | 理由 |
|---|---|
| `bot` | Botユーザーとしてサーバーに参加し、Gatewayからインタラクションを受け取るため |
| `applications.commands` | スラッシュコマンドを登録するため |

### Bot Permissions(招待URL)

**現時点では不要**です。スラッシュコマンドへの応答は、インタラクションのレスポンスとして返すので、チャンネルの「メッセージを送信」権限がなくても届きます。

### Privileged Gateway Intents(Developer Portal → Bot)

**現時点では不要**です。コードでは `GatewayIntents.AllUnprivileged` を指定しています。

| Intent | 状態 |
|---|---|
| Presence Intent | OFF |
| Server Members Intent | OFF |
| Message Content Intent | OFF |

### サーバー側(コマンドを使うユーザー)

コマンドを使うユーザーには、そのチャンネルで「アプリコマンドを使う(Use Application Commands)」権限が必要です。この権限は `@everyone` に最初から付いています。

## コマンド別

| コマンド | 内容 | Bot Permissions | Intents | 備考 |
|---|---|---|---|---|
| `/time` | 現在時刻を表示する | なし | なし(特権不要) | Discordのタイムスタンプ記法(`<t:...:F>`)で返すので、各ユーザーのタイムゾーンで表示される |
