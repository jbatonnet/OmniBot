using System.Net;

using Microsoft.Extensions.Logging;

using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;

using SIPSorcery.Net;

using SIPSorceryMedia.Abstractions;

using TinyJson;

using AudioFormat = OmniBot.Common.Audio.AudioFormat;
using IAudioSink = OmniBot.Common.Audio.IAudioSink;
using IAudioSource = OmniBot.Common.Audio.IAudioSource;

using SIPSorceryAudioFormat = SIPSorceryMedia.Abstractions.AudioFormat;

namespace OmniBot.SIPSorcery
{
    public enum WebRTCAudioFormat
    {
        None,
        PCMA,
        PCMU
    }

    public class WebRTCClient : IAudioSource, IAudioSink
    {
        private const int DEFAULT_SAMPLE_RATE = 8000;

        private const int SEND_AUDIO_PERIOD_MILLISECONDS = 20;
        private const int SEND_AUDIO_SAMPLES_COUNT = DEFAULT_SAMPLE_RATE * SEND_AUDIO_PERIOD_MILLISECONDS / 1000;

        public event AudioBufferReceivedEventHandler OnAudioBufferReceived;
        
        public AudioFormat Format => new AudioFormat(DEFAULT_SAMPLE_RATE, 1, 16);
        public bool Listening { get; set; } = true;
        public bool Sending { get; set; } = true;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<WebRTCClient> _logger;
        private readonly string _webRtcEndpoint;
        private readonly WebRTCAudioFormat _receiveAudioFormat;
        private readonly WebRTCAudioFormat _sendAudioFormat;

        private MuLawAudioConverter muLawAudioConverter = new();
        private ALawAudioConverter aLawAudioConverter = new();

        private RTCPeerConnection peerConnection;
        private Go2RTCWebSocketClient webSocketClient;

        private IAudioSource sinkSource;
        private LinearAudioConverter linearAudioConverter;
        private TimeSpan timecode = TimeSpan.Zero;

        public WebRTCClient(ILoggerFactory loggerFactory, string webRtcEndpoint, WebRTCAudioFormat receiveAudioFormat = WebRTCAudioFormat.None, WebRTCAudioFormat sendAudioFormat = WebRTCAudioFormat.None)
        {
            SIPSorceryInitializer.CheckInitiatlization();

            if (receiveAudioFormat == WebRTCAudioFormat.None && sendAudioFormat == WebRTCAudioFormat.None)
                throw new NotSupportedException("You need to specify at least one of receive or send audio format");
            if (receiveAudioFormat != WebRTCAudioFormat.None && receiveAudioFormat != WebRTCAudioFormat.PCMA && receiveAudioFormat != WebRTCAudioFormat.PCMU)
                throw new NotSupportedException("Only PCMA and PCMU audio codec are supported");
            if (sendAudioFormat != WebRTCAudioFormat.None && sendAudioFormat != WebRTCAudioFormat.PCMA && sendAudioFormat != WebRTCAudioFormat.PCMU)
                throw new NotSupportedException("Only PCMA and PCMU audio codec are supported");

            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<WebRTCClient>();
            _webRtcEndpoint = webRtcEndpoint;
            _receiveAudioFormat = receiveAudioFormat;
            _sendAudioFormat = sendAudioFormat;

            // Prepare peer connection
            peerConnection = new RTCPeerConnection(null);

            if (_receiveAudioFormat != WebRTCAudioFormat.None)
                peerConnection.addTrack(new MediaStreamTrack(new SIPSorceryAudioFormat(_receiveAudioFormat == WebRTCAudioFormat.PCMA ? SDPWellKnownMediaFormatsEnum.PCMA : SDPWellKnownMediaFormatsEnum.PCMU), _receiveAudioFormat == _sendAudioFormat ? MediaStreamStatusEnum.SendRecv : MediaStreamStatusEnum.RecvOnly));
            if (_sendAudioFormat != WebRTCAudioFormat.None && _sendAudioFormat != _receiveAudioFormat)
                peerConnection.addTrack(new MediaStreamTrack(new SIPSorceryAudioFormat(_receiveAudioFormat == WebRTCAudioFormat.PCMA ? SDPWellKnownMediaFormatsEnum.PCMA : SDPWellKnownMediaFormatsEnum.PCMU), MediaStreamStatusEnum.SendOnly));

            peerConnection.OnAudioFormatsNegotiated += f => _logger?.LogDebug("Negotiated audio formats: " + f.ToJson());
            peerConnection.OnTimeout += t => _logger?.LogWarning($"Timeout on media {t}.");
            peerConnection.oniceconnectionstatechange += s => _logger?.LogDebug($"ICE connection state changed to {s}.");
            peerConnection.onconnectionstatechange += s => _logger?.LogDebug($"Peer connection connected changed to {s}.");

            peerConnection.OnRtpPacketReceived += (IPEndPoint ipEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet) =>
            {
                if (!Listening)
                    return;

                if (mediaType == SDPMediaTypesEnum.audio)
                {
                    byte[] receiveBuffer = new byte[packet.Payload.Length * 2];

                    if (_receiveAudioFormat == WebRTCAudioFormat.PCMA)
                        aLawAudioConverter.DecodeAudio(packet.Payload, 0, packet.Payload.Length, receiveBuffer, 0);
                    else if (_receiveAudioFormat == WebRTCAudioFormat.PCMU)
                        muLawAudioConverter.DecodeAudio(packet.Payload, 0, packet.Payload.Length, receiveBuffer, 0);

                    AudioBuffer audioBuffer = new AudioBuffer()
                    {
                        Format = Format,
                        Data = receiveBuffer,
                        Timecode = timecode
                    };

                    timecode += audioBuffer.GetDuration();

                    OnAudioBufferReceived?.Invoke(audioBuffer);

                    //peerConnection.SendAudio((uint)packet.Payload.Length, packet.Payload);
                }
            };
        }

        public Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default)
        {
            if (sinkSource != null)
                sinkSource.OnAudioBufferReceived -= SinkSource_OnAudioBufferReceived;

            sinkSource = audioSource;

            linearAudioConverter = new LinearAudioConverter(sinkSource.Format, Format);
            sinkSource.OnAudioBufferReceived += SinkSource_OnAudioBufferReceived;

            if (sinkSource != null)
            {
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(t =>
                {
                    PlayAsync(null);
                });
            }

            return Task.CompletedTask;
        }

        public async Task Connect()
        {
            await ConnectInternal();
            peerConnection.onconnectionstatechange += PeerConnection_OnConnectionStateChanged;
        }
        public Task Disconnect()
        {
            peerConnection.onconnectionstatechange -= PeerConnection_OnConnectionStateChanged;

            peerConnection.close();
            return Task.CompletedTask;
        }
        protected async Task ConnectInternal()
        {
            webSocketClient = new Go2RTCWebSocketClient(_loggerFactory.CreateLogger<Go2RTCWebSocketClient>(), _webRtcEndpoint, () => Task.FromResult(peerConnection));
            await webSocketClient.Start(CancellationToken.None).ConfigureAwait(false);

            while (peerConnection.connectionState != RTCPeerConnectionState.connected)
            {
                await Task.Delay(100);

                if (peerConnection.connectionState == RTCPeerConnectionState.failed)
                    throw new Exception("Connection failed");
            }
        }

        private void SinkSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
        {
            if (!Sending || peerConnection.connectionState != RTCPeerConnectionState.connected)
                return;

            byte[] linearData = linearAudioConverter.ConvertAudio(audioBuffer.Data);
            byte[] encodedData = null;

            if (_sendAudioFormat == WebRTCAudioFormat.PCMU)
                encodedData = muLawAudioConverter.EncodeAudio(linearData);
            else if (_sendAudioFormat == WebRTCAudioFormat.PCMA)
                encodedData = aLawAudioConverter.EncodeAudio(linearData);

            peerConnection.SendAudio(SEND_AUDIO_SAMPLES_COUNT, encodedData);
        }
        private async void PeerConnection_OnConnectionStateChanged(RTCPeerConnectionState state)
        {
            if (state == RTCPeerConnectionState.@new || state == RTCPeerConnectionState.connecting || state == RTCPeerConnectionState.connected)
                return;

            // Automatic reconnection
            if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.disconnected)
            {
                _logger?.LogInformation($"Lost WebRTC connection, reconnecting...");
                await ConnectInternal();
            }
        }

        /*
        // Config
        var audioReceiveFormat = new SIPSorceryAudioFormat(SDPWellKnownMediaFormatsEnum.PCMA);
        //var audioFormat = new SIPSorceryAudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2);
        var videoReceiveFormat = new SIPSorceryVideoFormat(VideoCodecsEnum.H264, 100, 90000);
        //var videoFormat = new SIPSorceryVideoFormat(VideoCodecsEnum.VP8, 100, 90000);
        //var audioSendFormat = new SIPSorceryAudioFormat(AudioCodecsEnum.PCMA, 8);
        var audioSendFormat = new SIPSorceryAudioFormat(SDPWellKnownMediaFormatsEnum.PCMA);


        // From go2rtc
        var peerConnection = new RTCPeerConnection(null);

        //peerConnection.addTrack(new MediaStreamTrack(audioReceiveFormat, MediaStreamStatusEnum.RecvOnly));
        //peerConnection.addTrack(new MediaStreamTrack(videoReceiveFormat, MediaStreamStatusEnum.RecvOnly));
        peerConnection.addTrack(new MediaStreamTrack(audioSendFormat, MediaStreamStatusEnum.SendRecv));
            //peerConnection.addTrack(new MediaStreamTrack(audioSendFormat, MediaStreamStatusEnum.SendOnly));
        */

        /*if (true)
        // Debug audio and video sample
        {
            WindowsAudioEndPoint debugAudioSink = null; // new WindowsAudioEndPoint(new AudioEncoder());
            var debugVideoSink = new FFmpegVideoEndPoint();

            var pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Visible = true
            };

            var form = new Form()
            {
                Width = 640,
                Height = 480,
                Controls = { pictureBox }
            };

            form.FormClosing += (s, e) => Environment.Exit(0);

            Application.EnableVisualStyles();

            Thread formThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.Run(form);
            });

            formThread.SetApartmentState(ApartmentState.STA);
            formThread.Start();

            debugVideoSink.OnVideoSinkDecodedSampleFaster += i =>
            {
                pictureBox.BeginInvoke(new Action(() =>
                {
                    if (i.PixelFormat == VideoPixelFormatsEnum.Rgb)
                    {
                        Bitmap bitmap = new Bitmap(i.Width, i.Height, i.Stride, PixelFormat.Format24bppRgb, i.Sample);
                        pictureBox.Image = bitmap;
                    }
                }));
            };

            peerConnection.OnVideoFormatsNegotiated += f => debugVideoSink?.SetVideoSinkFormat(f.First());
            peerConnection.OnVideoFrameReceived += (i, t, p, v) => debugVideoSink?.GotVideoFrame(i, t, p, v);
            peerConnection.OnAudioFormatsNegotiated += f => debugAudioSink?.SetAudioSinkFormat(f.First());

            peerConnection.OnRtpPacketReceived += (IPEndPoint ipEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet) =>
            {
                if (mediaType == SDPMediaTypesEnum.audio)
                    debugAudioSink?.GotAudioRtp(ipEndPoint, packet.Header.SyncSource, packet.Header.SequenceNumber, packet.Header.Timestamp, packet.Header.PayloadType, packet.Header.MarkerBit == 1, packet.Payload);
            };

            peerConnection.onconnectionstatechange += async s =>
            {
                if (s == RTCPeerConnectionState.connected)
                    _ = debugAudioSink?.StartAudio();
            };
        }*/
    }
}
