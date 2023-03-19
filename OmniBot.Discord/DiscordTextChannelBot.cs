using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using Microsoft.Extensions.Logging;

namespace OmniBot.Discord
{
    public delegate void DiscordMessageEventHandler(DiscordTextChannelBot discordTextChannelBot, DiscordMessage discordMessage);

    public class DiscordTextChannelBot
    {
        public event DiscordMessageEventHandler OnMessageCreated;

        public DiscordChannel Channel => _discordChannel;

        private readonly ILogger<DiscordTextChannelBot> _logger;
        private readonly DiscordClient _discordClient;
        private readonly DiscordChannel _discordChannel;

        internal DiscordTextChannelBot(ILogger<DiscordTextChannelBot> logger, DiscordClient discordClient, DiscordChannel discordChannel)
        {
            _logger = logger;
            _discordClient = discordClient;
            _discordChannel = discordChannel;
        }

        internal async Task Connect()
        {
            _discordClient.MessageCreated += DiscordClient_MessageCreated;
        }
        internal async Task Disconnect()
        {
            _discordClient.MessageCreated -= DiscordClient_MessageCreated;
        }

        public Task<DiscordMessage> SendMessageAsync(string message)
        {
            return _discordChannel.SendMessageAsync(message);
        }
        public Task<DiscordMessage> SendMessageAsync(DiscordMessageBuilder discordMessageBuilder)
        {
            return _discordChannel.SendMessageAsync(discordMessageBuilder);
        }

        internal async Task DiscordClient_MessageCreated(DiscordClient discordClient, MessageCreateEventArgs e)
        {
            if (e.Channel.Id != _discordChannel.Id)
                return;
            if (e.Author.Id == _discordClient.CurrentUser.Id)
                return;

            OnMessageCreated?.Invoke(this, e.Message);
        }
    }
}