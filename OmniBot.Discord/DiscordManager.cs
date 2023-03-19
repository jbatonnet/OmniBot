using System.Collections.Concurrent;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

using Microsoft.Extensions.Logging;

namespace OmniBot.Discord
{
    public delegate void DiscordGuildBotEventHandler(DiscordManager discordManager, DiscordGuildBot discordGuildBot);

    public class DiscordManager
    {
        public event DiscordGuildBotEventHandler OnDiscordGuildBotCreated;
        public event DiscordGuildBotEventHandler OnDiscordGuildBotDeleted;

        public DiscordUser CurrentUser => _discordClient.CurrentUser;
        public IEnumerable<DiscordGuild> Guilds => _discordClient.Guilds.Values;
        public IEnumerable<DiscordGuildBot> GuildBots => guildBots.Values;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DiscordManager> _logger;
        private readonly DiscordClient _discordClient;

        private ConcurrentDictionary<ulong, DiscordGuildBot> guildBots = new ConcurrentDictionary<ulong, DiscordGuildBot>();

        public DiscordManager(ILoggerFactory loggerFactory, string botToken)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<DiscordManager>();

            DiscordConfiguration discordConfiguration = new DiscordConfiguration()
            {
                TokenType = TokenType.Bot,
                Token = botToken,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
                MinimumLogLevel = LogLevel.Warning,
                LoggerFactory = _loggerFactory
            };

            _discordClient = new DiscordClient(discordConfiguration);
            _discordClient.UseVoiceNext(new VoiceNextConfiguration() { EnableIncoming = true });
            _discordClient.Ready += async (s, e) => _logger.LogInformation("Connected to Discord gateway");
        }

        public async Task Connect()
        {
            await _discordClient.ConnectAsync();

            TaskCompletionSource taskCompletionSource = new TaskCompletionSource();

            _discordClient.GuildDownloadCompleted += async (c, e) =>
            {
                await _discordClient.InitializeAsync();

                var user = _discordClient.CurrentUser;
                var guilds = _discordClient.Guilds.Values;

                foreach (var guild in guilds)
                {
                    DiscordGuildBot guildBot = new DiscordGuildBot(_loggerFactory, _discordClient, guild);

                    _ = guildBot.Connect();
                    OnDiscordGuildBotCreated?.Invoke(this, guildBot);

                    guildBots[guild.Id] = guildBot;
                }

                taskCompletionSource.SetResult();
            };

            _discordClient.GuildCreated += async (c, e) =>
            {
                DiscordGuildBot guildBot = new DiscordGuildBot(_loggerFactory, _discordClient, e.Guild);

                _ = guildBot.Connect();
                OnDiscordGuildBotCreated?.Invoke(this, guildBot);

                guildBots[e.Guild.Id] = guildBot;
            };

            _discordClient.GuildDeleted += async (c, e) =>
            {
                if (!guildBots.TryRemove(e.Guild.Id, out DiscordGuildBot guildBot))
                    return;

                OnDiscordGuildBotDeleted?.Invoke(this, guildBot);
                await guildBot.Disconnect();
            };

            await taskCompletionSource.Task;
        }
        public async Task Disconnect() => throw new NotImplementedException();

        private async Task Test()
        {
            //_discordClient.
        }
    }
}