using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;

namespace sard_discord_bot.Modules;

/// <summary>
/// 入学年度(期)・所属班・ニックネームを対話形式で設定する。
/// 年度選択 → 班選択 → ニックネーム入力(モーダル)の順に進み、選んだ値は custom ID に埋め込んで引き継ぐ。
/// </summary>
[CommandContextType(InteractionContextType.Guild)]
public partial class InitModule : InteractionModuleBase<SocketInteractionContext>
{
    // 2024年度 = 14期
    private const int PeriodOffset = 2010;

    // 年度選択に出す年数(今年度を含む)
    private const int SelectableYears = 8;

    private const int NicknameMaxLength = 32;

    private static readonly Team[] Teams =
    [
        new("dev", "開発"),
        new("hybrid", "ハイブリッド"),
        new("hygiene", "衛生"),
        new("external", "外部"),
    ];

    // 同時に /init されても同じロールを二重に作らないようにする
    private static readonly SemaphoreSlim RoleCreationLock = new(1, 1);

    private static readonly TimeZoneInfo Jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    [GeneratedRegex(@"^\d+期$")]
    private static partial Regex PeriodRoleName();

    [SlashCommand("init", "入学年度・所属班・ニックネームを設定します")]
    public async Task InitAsync()
    {
        var currentYear = CurrentAcademicYear();
        var menu = new SelectMenuBuilder()
            .WithCustomId("init:year")
            .WithPlaceholder("入学年度を選んでください");
        for (var year = currentYear; year > currentYear - SelectableYears; year--)
        {
            menu.AddOption($"{year}年度入学({PeriodRoleNameOf(year)})", year.ToString());
        }

        await RespondAsync("**1/3** 入学年度を選んでください。",
            components: new ComponentBuilder().WithSelectMenu(menu).Build(),
            ephemeral: true);
    }

    [ComponentInteraction("init:year")]
    public async Task SelectYearAsync(string[] selected)
    {
        var year = selected[0];
        var menu = new SelectMenuBuilder()
            .WithCustomId($"init:team:{year}")
            .WithPlaceholder("所属班を選んでください");
        foreach (var team in Teams)
        {
            menu.AddOption(team.RoleName, team.Key);
        }

        await ((SocketMessageComponent)Context.Interaction).UpdateAsync(m =>
        {
            m.Content = $"入学年度: **{year}年度({PeriodRoleNameOf(int.Parse(year))})**\n**2/3** 所属班を選んでください。";
            m.Components = new ComponentBuilder().WithSelectMenu(menu).Build();
        });
    }

    [ComponentInteraction("init:team:*")]
    public async Task SelectTeamAsync(string year, string[] selected)
    {
        var user = (SocketGuildUser)Context.User;
        await RespondWithModalAsync($"init:submit:{year},{selected[0]}",
            new NicknameModal { Nickname = user.Nickname ?? user.GlobalName ?? user.Username });
    }

    [ModalInteraction("init:submit:*,*")]
    public async Task SubmitAsync(string yearText, string teamKey, NicknameModal modal)
    {
        // 年度選択のメッセージを結果で上書きする
        await DeferAsync();

        var team = Teams.FirstOrDefault(t => t.Key == teamKey);
        if (!int.TryParse(yearText, out var year) || year <= PeriodOffset || year > CurrentAcademicYear() || team is null)
        {
            await ShowResultAsync("選択内容が不正です。もう一度 `/init` を実行してください。");
            return;
        }

        var user = (SocketGuildUser)Context.User;
        var nickname = modal.Nickname.Trim();
        var periodRoleName = PeriodRoleNameOf(year);

        try
        {
            var periodRole = await GetOrCreateRoleAsync(periodRoleName);
            var teamRole = await GetOrCreateRoleAsync(team.RoleName);

            // 以前の期・班のロールを外してから付け直す
            var teamRoleNames = Teams.Select(t => t.RoleName).ToHashSet();
            var staleRoleIds = user.Roles
                .Where(r => r.Id != periodRole.Id && r.Id != teamRole.Id)
                .Where(r => PeriodRoleName().IsMatch(r.Name) || teamRoleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToList();
            if (staleRoleIds.Count > 0)
            {
                await user.RemoveRolesAsync(staleRoleIds);
            }
            await user.AddRolesAsync([periodRole.Id, teamRole.Id]);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            await ShowResultAsync("ロールを設定できませんでした。Bot に「ロールの管理」権限があり、Bot のロールが期・班のロールより上にあるか、サーバー管理者に確認してください。");
            return;
        }

        var lines = new List<string>
        {
            "設定が完了しました。",
            $"- 入学年度: {year}年度({periodRoleName})",
            $"- 所属班: {team.RoleName}",
        };

        // オーナーのニックネームは Bot から変更できない
        if (user.Id == Context.Guild.OwnerId)
        {
            lines.Add($"- ニックネーム: サーバーオーナーのため変更できません。ご自身で「{nickname}」に変更してください。");
        }
        else
        {
            try
            {
                await user.ModifyAsync(p => p.Nickname = nickname);
                lines.Add($"- ニックネーム: {nickname}");
            }
            catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
            {
                lines.Add("- ニックネーム: 権限不足で変更できませんでした。Bot に「ニックネームの管理」権限があり、Bot のロールがあなたの最上位ロールより上にあるか、サーバー管理者に確認してください。");
            }
        }

        await ShowResultAsync(string.Join('\n', lines));
    }

    private async Task ShowResultAsync(string content)
    {
        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = content;
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task<IRole> GetOrCreateRoleAsync(string name)
    {
        if (FindRole(Context.Guild.Roles, name) is { } cached)
        {
            return cached;
        }

        await RoleCreationLock.WaitAsync();
        try
        {
            // 直前に別の /init が作った直後だとキャッシュに反映されていないことがあるので、REST で取り直す
            var guild = await Context.Client.Rest.GetGuildAsync(Context.Guild.Id);
            return FindRole(guild.Roles, name)
                ?? await Context.Guild.CreateRoleAsync(name, GuildPermissions.None);
        }
        finally
        {
            RoleCreationLock.Release();
        }
    }

    private static IRole? FindRole(IEnumerable<IRole> roles, string name) =>
        roles.FirstOrDefault(r => r.Name == name);

    private static string PeriodRoleNameOf(int year) => $"{year - PeriodOffset}期";

    // 年度は4月始まり
    private static int CurrentAcademicYear()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Jst);
        return now.Month >= 4 ? now.Year : now.Year - 1;
    }

    private sealed record Team(string Key, string RoleName);

    public class NicknameModal : IModal
    {
        public string Title => "ニックネームの設定(3/3)";

        [InputLabel("ニックネーム")]
        [ModalTextInput("nickname", minLength: 1, maxLength: NicknameMaxLength)]
        public string Nickname { get; set; } = "";
    }
}
