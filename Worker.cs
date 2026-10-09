using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace sard_discord_bot;

public class Worker(
    ILogger<Worker> logger,
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IConfiguration configuration) : BackgroundService
{
    private bool _commandsRegistered;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = configuration["Discord:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Discord:Token が設定されていません。user-secrets か環境変数 Discord__Token で設定してください。");
        }

        client.Log += OnLogAsync;
        client.Ready += OnReadyAsync;
        client.InteractionCreated += OnInteractionCreatedAsync;
        interactions.Log += OnLogAsync;

        await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services);

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 停止要求
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await client.LogoutAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task OnReadyAsync()
    {
        logger.LogInformation("Logged in as {User} ({Id}), guilds: {GuildCount}",
            client.CurrentUser, client.CurrentUser.Id, client.Guilds.Count);

        // Ready は再接続のたびに発火するので、コマンド登録は初回だけ行う
        if (_commandsRegistered)
        {
            return;
        }

        // GuildId を指定すればそのサーバーに即時反映、未指定ならグローバル登録
        var guildId = configuration.GetValue<ulong?>("Discord:GuildId");
        if (guildId is { } id)
        {
            await interactions.RegisterCommandsToGuildAsync(id);
            logger.LogInformation("Registered slash commands to guild {GuildId}", id);
        }
        else
        {
            await interactions.RegisterCommandsGloballyAsync();
            logger.LogInformation("Registered slash commands globally");
        }
        _commandsRegistered = true;
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(client, interaction);
        var result = await interactions.ExecuteCommandAsync(context, services);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Interaction failed: {Error} {Reason}", result.Error, result.ErrorReason);
        }
    }

    private Task OnLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };
        logger.Log(level, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
