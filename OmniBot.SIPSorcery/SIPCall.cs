using System.Net;

using Microsoft.Extensions.Logging;

using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;

using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;

using SIPSorceryMedia.Abstractions;

using TinyJson;

using AudioFormat = OmniBot.Common.Audio.AudioFormat;
using IAudioSink = OmniBot.Common.Audio.IAudioSink;
using IAudioSource = OmniBot.Common.Audio.IAudioSource;

using SIPSorceryAudioFormat = SIPSorceryMedia.Abstractions.AudioFormat;

namespace OmniBot.SIPSorcery
{
    public class SIPCall : IAudioSource, IAudioSink
    {
        private const int DEFAULT_SAMPLE_RATE = 8000;

        private const int SEND_AUDIO_PERIOD_MILLISECONDS = 20;
        private const int SEND_AUDIO_SAMPLES_COUNT = DEFAULT_SAMPLE_RATE * SEND_AUDIO_PERIOD_MILLISECONDS / 1000;

        public event AudioBufferReceivedEventHandler OnAudioBufferReceived;
        public event Action<int> OnDtmfTone;
        public event Action OnHungup;

        public AudioFormat Format => new AudioFormat(DEFAULT_SAMPLE_RATE, 1, 16);
        public bool Listening { get; set; } = true;
        public bool Sending { get; set; } = true;
        public bool IsConnected => sipUserAgent.IsCallActive;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<WebRTCClient> _logger;

        private readonly string _sipAddress;
        private readonly string _sipUser;
        private readonly string _sipPassword;

        private ALawAudioConverter aLawAudioConverter = new();

        private RTPSession rtpSession;
        public SIPUserAgent sipUserAgent;

        private IAudioSource sinkSource;
        private LinearAudioConverter linearAudioConverter;
        private TimeSpan timecode = TimeSpan.Zero;

        internal SIPCall(ILoggerFactory loggerFactory, string sipAddress, string sipUser, string sipPassword)
        {
            SIPSorceryInitializer.CheckInitiatlization();

            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<WebRTCClient>();
            _sipAddress = sipAddress;
            _sipUser = sipUser;
            _sipPassword = sipPassword;

            // Prepare RTP session
            rtpSession = new RTPSession(new RtpSessionConfig
            {
                IsMediaMultiplexed = false,
                IsRtcpMultiplexed = false
            })
            {
                AcceptRtpFromAny = true
            };

            rtpSession.addTrack(new MediaStreamTrack(new SIPSorceryAudioFormat(SDPWellKnownMediaFormatsEnum.PCMA), MediaStreamStatusEnum.SendRecv));

            rtpSession.OnAudioFormatsNegotiated += f => _logger?.LogDebug("Negotiated audio formats: " + f.ToJson());
            rtpSession.OnTimeout += t => _logger?.LogWarning($"Timeout on media {t}.");

            rtpSession.OnRtpPacketReceived += (IPEndPoint ipEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet) =>
            {
                if (!Listening)
                    return;

                if (mediaType == SDPMediaTypesEnum.audio)
                {
                    byte[] receiveBuffer = new byte[packet.Payload.Length * 2];

                    aLawAudioConverter.DecodeAudio(packet.Payload, 0, packet.Payload.Length, receiveBuffer, 0);

                    AudioBuffer audioBuffer = new AudioBuffer()
                    {
                        Format = Format,
                        Data = receiveBuffer,
                        Timecode = timecode
                    };

                    timecode += audioBuffer.GetDuration();

                    OnAudioBufferReceived?.Invoke(audioBuffer);
                }
            };
        }

        internal async Task Connect()
        {
            var sipTransport = new SIPTransport();
            sipUserAgent = new SIPUserAgent(sipTransport, default);

            EnableTraceLogs(sipTransport);

            sipUserAgent.OnDtmfTone += (t, _) => OnDtmfTone?.Invoke(t);
            sipUserAgent.OnCallHungup += _ => OnHungup?.Invoke();

            // Place the call and wait for the result.
            bool callResult = await sipUserAgent.Call(_sipAddress, _sipUser, _sipPassword, rtpSession);
        }
        public Task Hangup()
        {
            sipUserAgent.Hangup();

            return Task.CompletedTask;
        }

        public Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default)
        {
            if (sinkSource != null)
                sinkSource.OnAudioBufferReceived -= SinkSource_OnAudioBufferReceived;

            sinkSource = audioSource;

            if (sinkSource != null)
            {
                linearAudioConverter = new LinearAudioConverter(sinkSource.Format, Format);
                sinkSource.OnAudioBufferReceived += SinkSource_OnAudioBufferReceived;

                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(t =>
                {
                    PlayAsync(null);
                });
            }

            return Task.CompletedTask;
        }

        private void SinkSource_OnAudioBufferReceived(AudioBuffer buffer)
        {
            if (!Sending || !rtpSession.IsStarted || rtpSession.IsClosed)
                return;

            byte[] linearData = linearAudioConverter.ConvertAudio(buffer.Data);
            byte[] encodedData = aLawAudioConverter.EncodeAudio(linearData);

            rtpSession.SendAudio(SEND_AUDIO_SAMPLES_COUNT, encodedData);
        }
        private void EnableTraceLogs(SIPTransport sipTransport)
        {
            sipTransport.SIPRequestInTraceEvent += (localEP, remoteEP, req) =>
            {
                _logger.LogTrace($"Request received: {localEP}<-{remoteEP}");
                _logger.LogTrace(req.ToString());
            };

            sipTransport.SIPRequestOutTraceEvent += (localEP, remoteEP, req) =>
            {
                _logger.LogTrace($"Request sent: {localEP}->{remoteEP}");
                _logger.LogTrace(req.ToString());
            };

            sipTransport.SIPResponseInTraceEvent += (localEP, remoteEP, resp) =>
            {
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogTrace($"Response received: {localEP}<-{remoteEP}");
                    _logger.LogTrace(resp.ToString());
                }
                else
                {
                    _logger.LogWarning($"Response received: {localEP}<-{remoteEP}");
                    _logger.LogWarning(resp.ToString());
                }
            };

            sipTransport.SIPResponseOutTraceEvent += (localEP, remoteEP, resp) =>
            {
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogTrace($"Response sent: {localEP}->{remoteEP}");
                    _logger.LogTrace(resp.ToString());
                }
                else
                {
                    _logger.LogWarning($"Response sent: {localEP}->{remoteEP}");
                    _logger.LogWarning(resp.ToString());
                }
            };

            sipTransport.SIPRequestRetransmitTraceEvent += (tx, req, count) =>
            {
                _logger.LogTrace($"Request retransmit {count} for request {req.StatusLine}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
            };

            sipTransport.SIPResponseRetransmitTraceEvent += (tx, resp, count) =>
            {
                _logger.LogTrace($"Response retransmit {count} for response {resp.ShortDescription}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
            };
        }
    }
}
