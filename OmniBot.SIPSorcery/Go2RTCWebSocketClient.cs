using System.Net.WebSockets;
using System.Text;

using Microsoft.Extensions.Logging;

using SIPSorcery;
using SIPSorcery.Net;

using TinyJson;

namespace OmniBot.SIPSorcery
{
    public class Go2RTCMessage
    {
        public string type { get; set; }
        public string value { get; set; }

        public Go2RTCMessage() { }
        public Go2RTCMessage(RTCSessionDescriptionInit init)
        {
            type = "webrtc/" + init.type.ToString();
            value = init.sdp;
        }

        public string toJSON()
        {
            return this.ToJson();
        }

        public static bool TryParseSdp(string json, out RTCSessionDescriptionInit init)
        {
            init = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var msg = json.FromJson<Go2RTCMessage>();
            if (msg != null)
            {
                if (msg.type == "webrtc/offer") init = new RTCSessionDescriptionInit() { type = RTCSdpType.offer, sdp = msg.value };
                if (msg.type == "webrtc/answer") init = new RTCSessionDescriptionInit() { type = RTCSdpType.answer, sdp = msg.value };

                return init != null;
            }

            return false;
        }
        public static bool TryParseCandidate(string json, out RTCIceCandidate candidate)
        {
            candidate = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var msg = json.FromJson<Go2RTCMessage>();
            if (msg != null)
            {
                try
                {
                    if (msg.type == "webrtc/candidate") candidate = RTCIceCandidate.Parse(msg.value);
                }
                catch { }

                return candidate != null;
            }

            return false;
        }
    }

    public class Go2RTCWebSocketClient
    {
        private const int MAX_RECEIVE_BUFFER = 8192;
        private const int MAX_SEND_BUFFER = 8192;
        private const int WEB_SOCKET_CONNECTION_TIMEOUT_MS = 10000;

        private ILogger<Go2RTCWebSocketClient> logger;

        private Uri _webSocketServerUri;
        private Func<Task<RTCPeerConnection>> _createPeerConnection;

        private RTCPeerConnection _pc;
        private ClientWebSocket _ws;
        public RTCPeerConnection RTCPeerConnection => _pc;
        public ClientWebSocket ClientWebSocket => _ws;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="webSocketServer">The web socket server URL to connect to for the SDP and 
        /// ICE candidate exchange.</param>
        public Go2RTCWebSocketClient(
            ILogger<Go2RTCWebSocketClient> logger,
            string webSocketServer,
            Func<Task<RTCPeerConnection>> createPeerConnection)
        {
            if (string.IsNullOrWhiteSpace(webSocketServer))
            {
                throw new ArgumentNullException("The web socket server URI must be supplied.");
            }

            this.logger = logger;

            _webSocketServerUri = new Uri(webSocketServer);
            _createPeerConnection = createPeerConnection;
        }

        /// <summary>
        /// Creates a new WebRTC peer connection and then starts polling the web socket server.
        /// An SDP offer is expected from the server. Once it has been received an SDP answer 
        /// will be returned.
        /// </summary>
        public async Task Start(CancellationToken cancellation)
        {
            _pc = await _createPeerConnection().ConfigureAwait(false);

            logger?.LogDebug($"websocket-client attempting to connect to {_webSocketServerUri}.");

            _ws = new ClientWebSocket();

            // As best I can tell the point of the CreateClientBuffer call is to set the size of the internal
            // web socket buffers. The return buffer seems to be for cases where direct access to the raw
            // web socket data is desired.
            _ = WebSocket.CreateClientBuffer(MAX_RECEIVE_BUFFER, MAX_SEND_BUFFER);
            CancellationTokenSource connectCts = new CancellationTokenSource();
            connectCts.CancelAfter(WEB_SOCKET_CONNECTION_TIMEOUT_MS);
            await _ws.ConnectAsync(_webSocketServerUri, connectCts.Token).ConfigureAwait(false);

            if (_ws.State == WebSocketState.Open)
            {
                logger?.LogTrace($"websocket-client starting receive task for server {_webSocketServerUri}.");

                _ = Task.Run(() => ReceiveFromWebSocket(_pc, _ws, cancellation)).ConfigureAwait(false);
                _ = Task.Run(() => SendInitialOffer(_pc, _ws, cancellation)).ConfigureAwait(false);
            }
            else
            {
                _pc.Close("web socket connection failure");
            }
        }






        private async Task ReceiveFromWebSocket(RTCPeerConnection pc, ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[MAX_RECEIVE_BUFFER];
            int posn = 0;

            while (ws.State == WebSocketState.Open &&
                (pc.connectionState == RTCPeerConnectionState.@new || pc.connectionState == RTCPeerConnectionState.connecting))
            {
                WebSocketReceiveResult receiveResult;
                do
                {
                    receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, posn, MAX_RECEIVE_BUFFER - posn), ct).ConfigureAwait(false);
                    posn += receiveResult.Count;
                }
                while (!receiveResult.EndOfMessage);

                if (posn > 0)
                {
                    var jsonMsg = Encoding.UTF8.GetString(buffer, 0, posn);
                    string jsonResp = await OnMessage(jsonMsg, pc);

                    if (jsonResp != null)
                    {
                        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonResp)), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                    }
                }

                posn = 0;
            }

            logger?.LogTrace($"websocket-client receive loop exiting.");
        }

        private async Task SendInitialOffer(RTCPeerConnection pc, ClientWebSocket ws, CancellationToken ct)
        {
            await Task.Delay(100);

            var offerSdp = pc.createOffer(null);
            var msg = new Go2RTCMessage(offerSdp);
            var msgJson = msg.toJSON();

            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msgJson)), WebSocketMessageType.Text, true, CancellationToken.None);

            logger?.LogTrace(offerSdp.sdp);
        }


        private async Task<string> OnMessage(string jsonStr, RTCPeerConnection pc)
        {
            if (Go2RTCMessage.TryParseCandidate(jsonStr, out var iceCandidate))
            {
                logger?.LogTrace($"Got remote ICE candidate: {iceCandidate.candidate}");
                pc.addIceCandidate(new RTCIceCandidateInit()
                {
                    candidate = iceCandidate.candidate,
                    sdpMid = iceCandidate.sdpMid,
                    sdpMLineIndex = iceCandidate.sdpMLineIndex,
                    usernameFragment = iceCandidate.usernameFragment
                });
            }
            else if (Go2RTCMessage.TryParseSdp(jsonStr, out var descriptionInit))
            {
                logger?.LogTrace($"Got remote SDP, type {descriptionInit.type}.");

                var result = pc.setRemoteDescription(descriptionInit);
                if (result != SetDescriptionResultEnum.OK)
                {
                    logger?.LogWarning($"Failed to set remote description, {result}.");
                    pc.Close("failed to set remote description");
                }

                if (descriptionInit.type == RTCSdpType.offer)
                {
                    var answerSdp = pc.createAnswer(null);
                    await pc.setLocalDescription(answerSdp).ConfigureAwait(false);

                    return answerSdp.toJSON();
                }
            }
            else
            {
                logger?.LogWarning($"websocket-client could not parse JSON message. {jsonStr}");
            }

            return null;
        }
    }
}
