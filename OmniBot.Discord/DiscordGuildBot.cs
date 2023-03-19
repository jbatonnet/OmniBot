using System.Collections.Concurrent;

using DSharpPlus;
using DSharpPlus.Entities;

using Microsoft.Extensions.Logging;

namespace OmniBot.Discord
{
    public delegate Task DiscordVoiceChannelBotEventHandler(DiscordGuildBot discordGuildBot, DiscordVoiceChannelBot discordVoiceChannelBot);
    public delegate Task DiscordTextChannelBotEventHandler(DiscordGuildBot discordGuildBot, DiscordTextChannelBot discordTextChannelBot);

    public class DiscordGuildBot
    {
        public event DiscordVoiceChannelBotEventHandler OnVoiceChannelBotRecovered;

        public event DiscordTextChannelBotEventHandler OnTextChannelCreated;
        public event DiscordTextChannelBotEventHandler OnTextChannelDeleted;

        public DiscordGuild Guild => _discordGuild;
        public DiscordChannel VoiceChannel => voiceChannelBot?.Channel;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DiscordGuildBot> _logger;
        private readonly DiscordClient _discordClient;
        private readonly DiscordGuild _discordGuild;

        private DiscordVoiceChannelBot voiceChannelBot;
        private ConcurrentDictionary<ulong, DiscordTextChannelBot> textChannelBots = new ConcurrentDictionary<ulong, DiscordTextChannelBot>();

        private ManualResetEvent voiceChannelConnectedEvent = new ManualResetEvent(false);

        internal DiscordGuildBot(ILoggerFactory loggerFactory, DiscordClient discordClient, DiscordGuild discordGuild)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<DiscordGuildBot>();
            _discordClient = discordClient;
            _discordGuild = discordGuild;
        }

        internal async Task Connect()
        {
            // Listen to channel creation / deletion
            _discordClient.ChannelCreated += (_, e) => DiscordClient_ChannelCreated(e.Channel);
            _discordClient.ChannelDeleted += (_, e) => DiscordClient_ChannelDeleted(e.Channel);

            _ = Task.Run(async () =>
            {
                var currentUser = _discordClient.CurrentUser;
                var channels = await _discordGuild.GetChannelsAsync();

                // Create existing text channel bots
                var textChannels = channels
                    .Where(c => c.Type == ChannelType.Text)
                    .ToArray();

                foreach (var textChannel in textChannels)
                    await DiscordClient_ChannelCreated(textChannel);

                // Connect back to voice
                var voiceChannels = channels
                    .Where(c => c.Type == ChannelType.Voice)
                    .ToArray();

                var voiceChannel = voiceChannels.FirstOrDefault(c => c.Users.Any(u => u.Id == currentUser.Id));
                if (voiceChannel != null)
                {
                    await ConnectToVoiceChannel(voiceChannel);

                    if (OnVoiceChannelBotRecovered != null)
                        await OnVoiceChannelBotRecovered(this, voiceChannelBot);
                }

                _logger.LogInformation($"Connected to Guild {_discordGuild.Name}");
            });
        }
        internal async Task Disconnect() => throw new NotImplementedException();
        public async Task WaitUntilVoiceChannelConnected()
        {
            await Task.Run(() => voiceChannelConnectedEvent.WaitOne());
        }

        private async Task DiscordClient_ChannelCreated(DiscordChannel discordChannel)
        {
            if (discordChannel.Guild.Id != _discordGuild.Id)
                return;
            if (discordChannel.Type != ChannelType.Text)
                return;

            var textChannelBot = new DiscordTextChannelBot(_loggerFactory.CreateLogger<DiscordTextChannelBot>(), _discordClient, discordChannel);

            if (textChannelBots.TryAdd(discordChannel.Id, textChannelBot))
            {
                await textChannelBot.Connect();

                if (OnTextChannelCreated != null)
                    await OnTextChannelCreated.Invoke(this, textChannelBot);
            }
        }
        private async Task DiscordClient_ChannelDeleted(DiscordChannel discordChannel)
        {
            if (discordChannel.Guild.Id != _discordGuild.Id)
                return;
            if (discordChannel.Type != ChannelType.Text)
                return;

            if (textChannelBots.TryRemove(discordChannel.Id, out var textChannelBot))
                await textChannelBot.Disconnect();
        }

        public async Task<DiscordVoiceChannelBot> ConnectToVoiceChannel(DiscordChannel voiceChannel)
        {
            if (voiceChannelBot?.Channel?.Id == voiceChannel?.Id)
                return voiceChannelBot;

            if (voiceChannelBot != null)
            {
                _ = voiceChannelBot.Disconnect();
                voiceChannelConnectedEvent.Reset();

                voiceChannelBot = null;
            }

            if (voiceChannel != null)
            {
                voiceChannelBot = new DiscordVoiceChannelBot(_loggerFactory.CreateLogger<DiscordVoiceChannelBot>(), _discordClient, voiceChannel);

                _ = voiceChannelBot.Connect();
                voiceChannelConnectedEvent.Set();
            }

            return voiceChannelBot;
        }
        public async Task DisconnectFromVoiceChannel()
        {
            await ConnectToVoiceChannel(null);
        }
    }
}
