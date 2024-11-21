using System.ClientModel;
using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.Logging;

using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;

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
        public AudioFormat Format => new AudioFormat(24000, 1, 16);
        public bool Listening { get; set; }

        private readonly ILogger _logger;
        private string _apiKey;
        private string _apiEndpoint;

        private OpenAIClient openAIClient;
        private RealtimeConversationClient realtimeConversationClient;
        private RealtimeConversationSession realtimeConversationSession;

        private IAudioSource currentAudioSource = null;
        private Stream currentAudioStream = null;

        private readonly int sendAudioPeriodMilliseconds;
        private readonly int sendAudioSamplesCount;
        private readonly int sendAudioBufferSize;

        private Timer sendAudioTimer;
        private ConcurrentQueue<byte[]> sendAudioQueue = new();
        private TimeSpan sendAudioTimecode = TimeSpan.Zero;

        public RealTimeClient(ILogger<RealTimeClient> logger, string apiKey, string apiEndpoint)
        {
            _logger = logger;

            _apiKey = apiKey;
            _apiEndpoint = apiEndpoint;

            sendAudioPeriodMilliseconds = 150;
            sendAudioSamplesCount = Format.SampleRate * sendAudioPeriodMilliseconds / 1000;
            sendAudioBufferSize = Format.BitsPerSample / 8 * sendAudioSamplesCount;
            sendAudioTimer = new Timer(SendAudioTimer_OnTick, null, sendAudioPeriodMilliseconds, sendAudioPeriodMilliseconds);

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
                /*InputTranscriptionOptions = new ConversationInputTranscriptionOptions()
                {
                    Model = "whisper-1"
                },*/
                Voice = new ConversationVoice("sage"), // sage, verge
                //TurnDetectionOptions = ConversationTurnDetectionOptions.CreateDisabledTurnDetectionOptions()
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
                            case ConversationInputSpeechStartedUpdate speechStarted:
                                _logger.LogTrace("Speech started");
                                sendAudioQueue.Clear(); // Interrupt response when user speaks
                                break;
                            case ConversationInputSpeechFinishedUpdate speechFinished: _logger.LogTrace("Speech finished"); break;
                            case ConversationInputTranscriptionFinishedUpdate speechTranscribed: _logger.LogTrace($"Speech transcription: {speechTranscribed.Transcript}"); break;

                            case ConversationResponseStartedUpdate responseStarted:
                                _logger.LogTrace("Response started");
                                sendAudioQueue.Clear(); // Interrupt previous response
                                break;
                            case ConversationResponseFinishedUpdate responseFinished: _logger.LogTrace("Response finished"); break;
                            case ConversationItemStreamingAudioTranscriptionFinishedUpdate responseTranscribed: _logger.LogTrace($"Response transcription: {responseTranscribed.Transcript}"); break;

                            case ConversationItemStreamingPartDeltaUpdate audioDelta when audioDelta.Kind == ConversationUpdateKind.ItemStreamingPartAudioDelta:

                                var audioData = audioDelta.AudioBytes.ToArray();

                                /*for (int i = 0; i < audioData.Length; i += sendAudioBufferSize)
                                {
                                    int length = Math.Min(i + sendAudioBufferSize, audioData.Length) - i;

                                    byte[] sendBuffer = new byte[sendAudioBufferSize];
                                    Array.Clear(sendBuffer);
                                    Array.Copy(audioData, i, sendBuffer, 0, length);

                                    sendAudioQueue.Enqueue(sendBuffer);
                                }*/

                                var audioBuffer = new AudioBuffer() { Format = Format, Data = audioData };
                                OnAudioBufferReceived?.Invoke(audioBuffer);

                                break;

                            default:
                                //_logger.LogTrace($"[{update.GetType().Name}]");
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
            if (audioSource == currentAudioSource)
                return;

            if (currentAudioStream != null)
                currentAudioStream.Dispose();

            var convertedAudioSource = new LinearConversionAudioSource(audioSource, Format);

            currentAudioSource = audioSource;
            currentAudioStream = new ContinuousAudioStream(convertedAudioSource);

            await realtimeConversationSession.SendInputAudioAsync(currentAudioStream, cancellationToken);
        }

        private void SendAudioTimer_OnTick(object? state)
        {
            if (!sendAudioQueue.TryDequeue(out byte[] buffer))
                return;

            if (Listening || true)
            {
                AudioBuffer audioBuffer = new AudioBuffer()
                {
                    Format = Format,
                    Data = buffer,
                    Timecode = sendAudioTimecode
                };

                sendAudioTimecode += audioBuffer.GetDuration();

                OnAudioBufferReceived?.Invoke(audioBuffer);
            }
        }
    }
}
