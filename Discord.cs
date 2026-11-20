using System.Net.Http;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace ObeliskPanel;

public static class Webhooks
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task Post(string url, string text, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var body = JsonSerializer.Serialize(new { content = text.Length > 1900 ? text[..1900] + "…" : text, username = "ObeliskPanel" });
            var r = await Http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!r.IsSuccessStatusCode) log?.Invoke($"Discord webhook failed: {(int)r.StatusCode} {r.ReasonPhrase}");
        }
        catch (Exception ex) { log?.Invoke("Discord webhook failed: " + ex.Message); }
    }

    public static Task Send(AppSettings s, ServerProfile p, string kind, string? overrideText, Action<string> log)
    {
        if (!p.WebhooksEnabled || !p.WebhookEvents.TryGetValue(kind, out var ev) || !ev.Enabled) return Task.CompletedTask;
        var hook = s.Webhooks.FirstOrDefault(w => w.Name == ev.Webhook) ?? (s.Webhooks.Count == 1 ? s.Webhooks[0] : null);
        if (hook == null) return Task.CompletedTask;
        var text = (overrideText ?? ev.Text).Replace("{ip}", PublicIp.Address(s, p)).Replace("{map}", p.Map).Replace("{name}", p.Name);
        var prefix = p.WebhookPrependSessionName ? p.SessionName : p.Name;
        return Post(hook.Url, $"**{prefix}** {text}", log);
    }
}

public interface IBotHost
{
    IReadOnlyList<ServerProfile> Servers { get; }
    bool IsRunning(ServerProfile p);
    ServerState State(ServerProfile p);
    string? Players(ServerProfile p);
    string Address(ServerProfile p);
    Task Start(ServerProfile p);
    Task Stop(ServerProfile p);
    Task Restart(ServerProfile p);
    Task<string> Rcon(ServerProfile p, string cmd);
    void Log(string msg);
}

public sealed class DiscordBot : IAsyncDisposable
{
    readonly AppSettings _s;
    readonly IBotHost _host;
    DiscordSocketClient? _client;
    CancellationTokenSource? _statusLoop;

    public bool Connected => _client?.ConnectionState == ConnectionState.Connected;

    public DiscordBot(AppSettings s, IBotHost host) { _s = s; _host = host; }

    public async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(_s.BotToken)) { _host.Log("Discord bot: no token set."); return; }
        _client = new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.Guilds });
        _client.Log += m => { if (m.Severity <= LogSeverity.Warning) _host.Log($"Discord bot: {m.Message ?? m.Exception?.Message}"); return Task.CompletedTask; };
        _client.Ready += OnReady;
        _client.JoinedGuild += g => RegisterCommands(g);
        _client.SlashCommandExecuted += OnCommand;
        await _client.LoginAsync(TokenType.Bot, _s.BotToken.Trim());
        await _client.StartAsync();
    }

    async Task OnReady()
    {
        _host.Log($"Discord bot connected as {_client!.CurrentUser.Username} ({_client.Guilds.Count} Discord server(s)).");
        foreach (var g in _client.Guilds) await RegisterCommands(g);
        _statusLoop?.Cancel();
        _statusLoop = new CancellationTokenSource();
        _ = StatusLoop(_statusLoop.Token);
    }

    SlashCommandOptionBuilder ServerOption()
    {
        var o = new SlashCommandOptionBuilder().WithName("server").WithDescription("Server profile").WithType(ApplicationCommandOptionType.String).WithRequired(true);
        foreach (var p in _host.Servers.Take(25)) o.AddChoice($"{p.Name} ({p.Game})", p.Name);
        return o;
    }

    async Task RegisterCommands(SocketGuild g)
    {
        try
        {
            var cmds = new List<ApplicationCommandProperties>
            {
                new SlashCommandBuilder().WithName("status").WithDescription("Show all ARK servers").Build(),
                new SlashCommandBuilder().WithName("ip").WithDescription("Show the connect addresses").Build(),
                new SlashCommandBuilder().WithName("players").WithDescription("List online players").AddOption(ServerOption()).Build(),
                new SlashCommandBuilder().WithName("start").WithDescription("Start a server (admin)").AddOption(ServerOption()).Build(),
                new SlashCommandBuilder().WithName("stop").WithDescription("Save and stop a server (admin)").AddOption(ServerOption()).Build(),
                new SlashCommandBuilder().WithName("restart").WithDescription("Restart a server with countdown (admin)").AddOption(ServerOption()).Build(),
                new SlashCommandBuilder().WithName("rcon").WithDescription("Send an RCON command (admin)").AddOption(ServerOption())
                    .AddOption("command", ApplicationCommandOptionType.String, "RCON command", isRequired: true).Build(),
            };
            await g.BulkOverwriteApplicationCommandAsync(cmds.ToArray());
        }
        catch (Exception ex) { _host.Log($"Discord bot: could not register commands in '{g.Name}': {ex.Message}"); }
    }

    public Task RefreshCommands() => _client == null ? Task.CompletedTask : Task.WhenAll(_client.Guilds.Select(RegisterCommands));

    bool IsAdmin(SocketSlashCommand c)
    {
        if (c.User is not SocketGuildUser u) return false;
        if (u.GuildPermissions.Administrator) return true;
        return ulong.TryParse(_s.BotAdminRoleId, out var role) && u.Roles.Any(r => r.Id == role);
    }

    string StatusText()
    {
        var sb = new StringBuilder();
        foreach (var p in _host.Servers)
        {
            var state = _host.State(p);
            sb.Append(state switch { ServerState.Running => "🟢 ", ServerState.Stopped => "🔴 ", _ => "🟡 " })
              .Append($"**{p.Name}** – {p.Map.Replace("_WP", "")} ({p.Game})");
            if (state == ServerState.Running) sb.Append($" · {_host.Players(p) ?? "?"} players · `{_host.Address(p)}`");
            else if (state != ServerState.Stopped) sb.Append(state == ServerState.Starting ? " · starting up…" : " · stopping…");
            sb.AppendLine();
        }
        return sb.Length == 0 ? "No servers set up." : sb.ToString();
    }

    async Task OnCommand(SocketSlashCommand c)
    {
        try
        {
            string? Opt(string n) => c.Data.Options.FirstOrDefault(o => o.Name == n)?.Value?.ToString();
            ServerProfile? Server() => _host.Servers.FirstOrDefault(p => p.Name == Opt("server"));

            switch (c.Data.Name)
            {
                case "status":
                    await c.RespondAsync(embed: new EmbedBuilder().WithTitle("ARK Servers").WithDescription(StatusText()).WithColor(Color.Teal).Build());
                    return;
                case "ip":
                    await c.RespondAsync(string.Join("\n", _host.Servers.Select(p => $"**{p.Name}**: `{_host.Address(p)}`")));
                    return;
            }

            var p = Server();
            if (p == null) { await c.RespondAsync("Unknown server.", ephemeral: true); return; }

            if (c.Data.Name == "players")
            {
                if (!_host.IsRunning(p)) { await c.RespondAsync($"**{p.Name}** is not running."); return; }
                await c.DeferAsync();
                var r = await _host.Rcon(p, "ListPlayers");
                await c.FollowupAsync($"**{p.Name}**\n```{(string.IsNullOrWhiteSpace(r) ? "No answer" : r)}```");
                return;
            }

            if (!IsAdmin(c)) { await c.RespondAsync("You are not allowed to do that.", ephemeral: true); return; }
            _host.Log($"Discord: {c.User.Username} used /{c.Data.Name} on {p.Name}");
            switch (c.Data.Name)
            {
                case "start":
                    if (_host.IsRunning(p)) { await c.RespondAsync($"**{p.Name}** is already running."); return; }
                    await c.RespondAsync($"Starting **{p.Name}**…");
                    await _host.Start(p);
                    return;
                case "stop":
                    if (!_host.IsRunning(p)) { await c.RespondAsync($"**{p.Name}** is not running."); return; }
                    await c.RespondAsync($"Saving and stopping **{p.Name}**…");
                    await _host.Stop(p);
                    return;
                case "restart":
                    await c.RespondAsync($"Restarting **{p.Name}** (with countdown if enabled)…");
                    await _host.Restart(p);
                    return;
                case "rcon":
                    await c.DeferAsync(ephemeral: true);
                    var r = await _host.Rcon(p, Opt("command") ?? "");
                    await c.FollowupAsync($"```{(string.IsNullOrWhiteSpace(r) ? "(no response)" : r)}```", ephemeral: true);
                    return;
            }
        }
        catch (Exception ex)
        {
            _host.Log("Discord bot command failed: " + ex.Message);
            try { if (!c.HasResponded) await c.RespondAsync("Something went wrong: " + ex.Message, ephemeral: true); } catch { }
        }
    }

    async Task StatusLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (ulong.TryParse(_s.BotStatusChannelId, out var chId) && _client?.GetChannel(chId) is IMessageChannel ch)
                {
                    var embed = new EmbedBuilder().WithTitle("ARK Servers").WithDescription(StatusText())
                        .WithFooter("Updated").WithCurrentTimestamp().WithColor(Color.Teal).Build();
                    IUserMessage? msg = null;
                    if (ulong.TryParse(_s.BotStatusMessageId, out var mId)) msg = await ch.GetMessageAsync(mId) as IUserMessage;
                    if (msg != null) await msg.ModifyAsync(m => m.Embed = embed);
                    else _s.BotStatusMessageId = (await ch.SendMessageAsync(embed: embed)).Id.ToString();
                }
            }
            catch (Exception ex) { _host.Log("Discord status message failed: " + ex.Message); }
            try { await Task.Delay(TimeSpan.FromSeconds(60), ct); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _statusLoop?.Cancel();
        if (_client != null)
        {
            try { await _client.StopAsync(); await _client.LogoutAsync(); } catch { }
            _client.Dispose();
        }
    }
}
