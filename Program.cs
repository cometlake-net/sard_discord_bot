using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using sard_discord_bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged,
}));
builder.Services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
