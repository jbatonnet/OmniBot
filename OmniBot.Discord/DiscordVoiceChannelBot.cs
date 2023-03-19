using System.Collections.Concurrent;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

using Microsoft.Extensions.Logging;

using NAudio.Wave;

using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;

using AudioFormat = OmniBot.Common.Audio.AudioFormat;

namespace OmniBot.Discord
{
    public delegate Task DiscordUserEventHandler(DiscordUser discordUser);

    public class DiscordVoiceChannelBot : IAudioSource, IAudioSink
    {
        public event DiscordUserEventHandler UserSpeaking;
        public event DiscordUserEventHandler UserJoinedChannel;
        public event DiscordUserEventHandler UserLeftChannel;

        public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

        public bool Recording { get; set; } = true;

        public DiscordChannel Channel => _discordChannel;
        public IEnumerable<DiscordUser> Users => _discordChannel?.Users?.Where(u => u.Id != _discordClient.CurrentUser.Id) ?? Enumerable.Empty<DiscordUser>();
        public IEnumerable<DiscordUser> ActiveUsers => Users?.Where(u => lastSpeakingActivity.ContainsKey(u.Id) && (DateTime.Now - lastSpeakingActivity[u.Id]) < TimeSpan.FromMinutes(1));

        public AudioFormat Format => throw new NotImplementedException();

        public bool Listening { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private readonly ILogger<DiscordVoiceChannelBot> _logger;
        private readonly DiscordClient _discordClient;
        private readonly DiscordChannel _discordChannel;

        private VoiceNextConnection audioClient;
        private ConcurrentDictionary<uint, DiscordUser> voiceUsers = new ConcurrentDictionary<uint, DiscordUser>();

        private ConcurrentDictionary<ulong, DateTime> lastSpeakingActivity = new ConcurrentDictionary<ulong, DateTime>();
        private DiscordUser lastSpeakingUser;

        private ConcurrentDictionary<ulong, ConcurrentQueue<byte[]>> voiceBuffers = new ConcurrentDictionary<ulong, ConcurrentQueue<byte[]>>();
        private ConcurrentDictionary<ulong, CancellationTokenSource> voiceTimeouts = new ConcurrentDictionary<ulong, CancellationTokenSource>();

        internal DiscordVoiceChannelBot(ILogger<DiscordVoiceChannelBot> logger, DiscordClient discordClient, DiscordChannel discordChannel)
        {
            _logger = logger;
            _discordClient = discordClient;
            _discordChannel = discordChannel;
        }

        internal async Task Connect()
        {
            _logger.LogInformation($"Connecting to voice channel {_discordChannel.Name}");

            audioClient = await _discordChannel.ConnectAsync();

            _logger.LogDebug($"Connected to voice channel {_discordChannel.Name}");

            audioClient.UserJoined += async (s, e) =>
            {
                voiceUsers[e.SSRC] = e.User;
                UserJoinedChannel?.Invoke(e.User);
            };
            audioClient.UserLeft += async (s, e) =>
            {
                voiceUsers.Remove(e.SSRC, out _);
                UserLeftChannel?.Invoke(e.User);
            };
            audioClient.UserSpeaking += async (s, e) =>
            {
                voiceUsers[e.SSRC] = e.User;
            };
            audioClient.VoiceReceived += async (s, e) =>
            {
                if (!voiceUsers.TryGetValue(e.SSRC, out DiscordUser speakingUser))
                    return;

                if (speakingUser != lastSpeakingUser)
                    await UserSpeaking?.Invoke(speakingUser);

                lastSpeakingUser = speakingUser;
                lastSpeakingActivity[speakingUser.Id] = DateTime.Now;

                // Handle timeouts to split audio recordings
                if (voiceTimeouts.TryGetValue(speakingUser.Id, out CancellationTokenSource voiceTimeoutTokenSource))
                    voiceTimeoutTokenSource.Cancel();

                voiceTimeoutTokenSource = new CancellationTokenSource();
                voiceTimeouts[speakingUser.Id] = voiceTimeoutTokenSource;

                _ = Task.Delay(250, voiceTimeoutTokenSource.Token)
                    .ContinueWith(async t =>
                    {
                        lastSpeakingUser = null;
                        await ProcessVoicePause(speakingUser);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);

                // Process voice data
                await ProcessVoiceData(speakingUser, e.AudioFormat.ToAudioFormat(), e.PcmData.ToArray());
            };

            foreach (var user in _discordChannel.Users)
                lastSpeakingActivity[user.Id] = DateTime.Now;

            /*if (UserJoinedChannel != null)
            {
                foreach (var user in _discordChannel.Users)
                    await UserJoinedChannel(user);
            }*/
        }
        internal async Task Disconnect()
        {
            _logger.LogInformation($"Disconnecting from voice channel {_discordChannel.Name}");

            audioClient.Disconnect();
        }

        public async Task PlayAsync(AudioRecording audioRecording, CancellationToken cancellationToken = default)
        {
            await audioClient.SendSpeakingAsync(true);

            // Convert audio to PCM 48 kHz stereo
            using MemoryStream inputStream = new MemoryStream(audioRecording.Data);
            using WaveFileReader inputWaveReader = new WaveFileReader(inputStream);

            var audioConverter = new LinearAudioConverter(inputWaveReader.WaveFormat.ToAudioFormat(), new AudioFormat(48000, 2, 16));

            VoiceTransmitSink voiceTransmitSink = audioClient.GetTransmitSink();

            byte[] outputBuffer = new byte[voiceTransmitSink.SampleLength];
            byte[] inputBuffer = new byte[voiceTransmitSink.SampleLength * audioConverter.SourceFormat.GetSampleSize() / audioConverter.DestinationFormat.GetSampleSize() * audioConverter.SourceFormat.SampleRate / audioConverter.DestinationFormat.SampleRate];

            int read = 1;

            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    read = inputWaveReader.Read(inputBuffer, 0, inputBuffer.Length);
                    if (read <= 0)
                        break;

                    int converted = audioConverter.ConvertAudio(inputBuffer, 0, read, outputBuffer, 0);
                    await voiceTransmitSink.WriteAsync(outputBuffer, 0, converted, cancellationToken);
                }
            });

            try
            {
                await Task.Delay((int)Math.Max(0, audioRecording.Duration.TotalMilliseconds - 500), cancellationToken);
            }
            catch
            {
                throw;
            }
            finally
            {
                await audioClient.SendSpeakingAsync(false);
            }
        }
        public Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            //audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
        }

        private async void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
        {
            await audioClient.SendSpeakingAsync(true);

            // Convert audio to PCM 48 kHz stereo
            var audioConverter = new LinearAudioConverter(audioBuffer.Format, new AudioFormat(48000, 2, 16));

            VoiceTransmitSink voiceTransmitSink = audioClient.GetTransmitSink();

            byte[] outputBuffer = new byte[voiceTransmitSink.SampleLength];
            byte[] inputBuffer = new byte[voiceTransmitSink.SampleLength * audioConverter.SourceFormat.GetSampleSize() / audioConverter.DestinationFormat.GetSampleSize() * audioConverter.SourceFormat.SampleRate / audioConverter.DestinationFormat.SampleRate];

            int read = 1;

            _ = Task.Run(async () =>
            {
                while (true)//!cancellationToken.IsCancellationRequested)
                {
                    /*read = inputWaveReader.Read(inputBuffer, 0, inputBuffer.Length);
                    if (read <= 0)
                        break;

                    int converted = audioConverter.ConvertAudio(inputBuffer, 0, read, outputBuffer, 0);
                    await voiceTransmitSink.WriteAsync(outputBuffer, 0, converted, cancellationToken);*/
                }
            });

            try
            {
                //await Task.Delay((int)Math.Max(0, audioRecording.Duration.TotalMilliseconds - 500), cancellationToken);
            }
            catch
            {
                throw;
            }
            finally
            {
                await audioClient.SendSpeakingAsync(false);
            }
        }

        private async Task ProcessVoiceData(DiscordUser user, AudioFormat format, byte[] rawData)
        {
            // TODO: Shortcut to AudioSource

            using var inputStream = new MemoryStream(rawData);
            using var inputWaveStream = new RawSourceWaveStream(inputStream, format.ToWaveFormat());

            // Convert audio to PCM 16 kHz mono
            var audioConverter = new LinearAudioConverter(format, AudioFormat.Default);

            // Queue audio buffers
            byte[] inputBuffer = new byte[4096];
            byte[] outputBuffer = new byte[4096];

            while (true)
            {
                int read = inputWaveStream.Read(inputBuffer, 0, inputBuffer.Length);
                if (read <= 0)
                    break;

                int converted = audioConverter.ConvertAudio(inputBuffer, 0, read, outputBuffer, 0);

                var bufferQueue = voiceBuffers.GetOrAdd(user.Id, _ => new ConcurrentQueue<byte[]>());

                byte[] audioBufferSpan = new byte[converted];
                Array.Copy(outputBuffer, audioBufferSpan, converted);

                bufferQueue.Enqueue(audioBufferSpan);
            }
        }
        private async Task ProcessVoicePause(DiscordUser user)
        {
            var bufferQueue = voiceBuffers.GetOrAdd(user.Id, _ => new ConcurrentQueue<byte[]>());
            byte[][] buffers = bufferQueue.ToArray();

            bufferQueue.Clear();

            int byteCount = buffers.Sum(b => b.Length);
            TimeSpan duration = TimeSpan.FromSeconds(byteCount / (double)AudioFormat.Default.SampleRate);

            if (duration < AudioRecording.MinimumSpeechDuration)
                return;

            if (!Recording)
                return;

            using MemoryStream audioStream = new MemoryStream();

            foreach (byte[] buffer in buffers)
                audioStream.Write(buffer);

            audioStream.Flush();

            AudioBuffer audioBuffer = new AudioBuffer()
            {
                Format = AudioFormat.Default,
                Data = audioStream.GetBuffer(),
                Timecode = TimeSpan.Zero
            };

            OnAudioBufferReceived?.Invoke(audioBuffer);
        }
    }
}