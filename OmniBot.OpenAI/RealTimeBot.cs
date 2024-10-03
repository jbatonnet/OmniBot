using System.ClientModel;
using System.Net;

using Microsoft.Extensions.Logging;

using OmniBot.Common.Audio;

using OpenAI;
using OpenAI.RealtimeConversation;

using AudioFormat = OmniBot.Common.Audio.AudioFormat;

using OpenAIClient = global::OpenAI.OpenAIClient;

#pragma warning disable OPENAI002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace OmniBot.OpenAI
{
    public class RealTimeClient : IAudioSource, IAudioSink
    {
        public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

        public bool Recording { get; set; } = true;
        public AudioFormat Format => new AudioFormat(16000, 1, 16);
        public bool Listening { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private readonly ILogger _logger;
        private string _apiKey;
        private string _apiEndpoint;

        private OpenAIClient openAIClient;
        private RealtimeConversationClient realtimeConversationClient;
        private RealtimeConversationSession realtimeConversationSession;

        public RealTimeClient(ILogger<RealTimeClient> logger, string apiKey, string apiEndpoint)
        {
            _logger = logger;

            _apiKey = apiKey;
            _apiEndpoint = apiEndpoint;

            var options = new OpenAIClientOptions()
            {
                Endpoint = new Uri(apiEndpoint ?? OpenAIOptions.OpenAIEndpoint)
            };

            openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
            realtimeConversationClient = openAIClient.GetRealtimeConversationClient("gpt-4o-realtime-preview");
        }

        public async Task StartConversation()
        {
            realtimeConversationSession = await realtimeConversationClient.StartConversationSessionAsync();

            await realtimeConversationSession.ConfigureSessionAsync(new ConversationSessionOptions()
            {
                InputAudioFormat = ConversationAudioFormat.Pcm16,
                OutputAudioFormat = ConversationAudioFormat.Pcm16,
            });

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var updates = realtimeConversationSession.ReceiveUpdatesAsync();
                    await foreach (var update in updates)
                    {
                        switch (update)
                        {
                            case ConversationInputSpeechStartedUpdate speechStarted: _logger.LogTrace("Speech started"); break;
                            case ConversationInputSpeechFinishedUpdate speechFinished: _logger.LogTrace("Speech finished"); break;
                            case ConversationResponseStartedUpdate responseStarted: _logger.LogTrace("Response started"); break;
                            case ConversationResponseFinishedUpdate responseFinished: _logger.LogTrace("Response finished"); break;

                            case ConversationAudioDeltaUpdate audioDelta:
                                OnAudioBufferReceived?.Invoke(new AudioBuffer() { Format = Format, Data = audioDelta.GetRawContent().ToArray() });
                                break;
                        }
                    }
                }
            });
        }
        public async Task StopConversation()
        {
            realtimeConversationSession.Dispose();
        }

        public async Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default)
        {
            // FIXME: Add wav headers
            audioSource.OnAudioBufferReceived += async b => await realtimeConversationSession.SendAudioAsync(new BinaryData(b.Data));
        }
    }
}
