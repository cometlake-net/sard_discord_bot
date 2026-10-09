# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 概要

Discord.Net(3.20.1)を使った Discord Bot です。.NET 10 の Worker Service(`Microsoft.NET.Sdk.Worker`)として動き、スラッシュコマンドは `Discord.Interactions` の `InteractionService` で処理します。
ドキュメントとコミットメッセージは日本語で書いています。

## コマンド

```sh
dotnet build
dotnet run                                           # launchSettings により DOTNET_ENVIRONMENT=Development で起動
dotnet user-secrets set "Discord:Token" "<token>"    # 必須
dotnet user-secrets set "Discord:GuildId" "<id>"     # 任意。指定するとそのサーバーにコマンドを即時登録
```

テストプロジェクトとリンターはまだありません。

## 設定

| キー | 用途 |
|---|---|
| `Discord:Token` | Bot トークン。未設定だと Worker が例外を投げて停止する |
| `Discord:GuildId` | 指定するとそのサーバーにだけコマンドを登録(即時反映)。未指定ならグローバル登録 |

開発中は user-secrets(csproj の `UserSecretsId`)から読み込みます。本番では環境変数 `Discord__Token` / `Discord__GuildId` で渡します。

## アーキテクチャ

- **DI 登録([Program.cs](Program.cs))**: `DiscordSocketClient` と `InteractionService` をシングルトンで登録し、`Worker` をホステッドサービスとして登録しています。Gateway Intents もここで指定しています(現在は `AllUnprivileged`)。
- **ライフサイクル([Worker.cs](Worker.cs))**: `ExecuteAsync` で、ログ・Ready・InteractionCreated のイベントハンドラを登録し、モジュールを読み込んでからログインします。停止処理は `StopAsync` で行います。Discord.Net のログは `ILogger` に転送しています。
- **コマンドの自動検出**: `AddModulesAsync(Assembly.GetEntryAssembly(), ...)` がリフレクションで `InteractionModuleBase<SocketInteractionContext>` を継承した `public` クラスを探します。そのため、[Modules/](Modules/) にクラスを追加するだけでコマンドが増え、Worker の変更は不要です。`public` でないクラスはエラーにならずに無視されます。
- **コマンドの登録**: `Ready` イベントで Discord に登録します。`Ready` は再接続のたびに発火するので、`_commandsRegistered` フラグで初回だけ登録するようにしています。`GuildId` を指定して登録した後にグローバル登録へ切り替えると、そのサーバーではコマンドが重複して表示されます。
- **モジュールの寿命**: モジュールはコマンドが呼ばれるたびに DI 経由で新しく作られます。呼び出しをまたいで状態を保持したい場合は、シングルトンのサービスとして登録し、コンストラクタで受け取ってください。

## 守ること

- コマンドを追加・変更したときは、必要な OAuth2 Scope、Bot Permissions、Intents を [RequiredPermission.md](RequiredPermission.md) に追記してください。
- 特権 Intent(Presence / Server Members / Message Content)を [Program.cs](Program.cs) に追加するときは、Developer Portal 側でも有効にする必要があります。片方だけだと、接続時に `4014 Disallowed intent(s)` で切断されます。
