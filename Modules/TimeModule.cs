using Discord.Interactions;

namespace sard_discord_bot.Modules;

public class TimeModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("time", "現在時刻を表示します")]
    public async Task TimeAsync()
    {
        // Discord のタイムスタンプ記法なので、各ユーザーのタイムゾーンで表示される
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await RespondAsync($"現在時刻: <t:{unixTime}:F>");
    }
}
