using System.Net.WebSockets;

using NAudio.Wave;

namespace OmniBot.Common.Audio;

public class WebSocketAudioSink : IAudioSink
{
    public AudioFormat Format => throw new NotImplementedException();

    private readonly string _endpointUrl;

    private ClientWebSocket clientWebSocket;

    public WebSocketAudioSink(string endpointUrl)
    {
        _endpointUrl = endpointUrl;
    }

    public async Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default)
    {
        clientWebSocket?.Dispose();

        if (audioSource == null)
            return;

        clientWebSocket = new ClientWebSocket();

        await clientWebSocket.ConnectAsync(new Uri(_endpointUrl), cancellationToken);

        // Send WAV header
        using (MemoryStream wavStream = new MemoryStream())
        {
            WaveFileWriter wavWriter = new WaveFileWriter(wavStream, audioSource.Format.ToWaveFormat());
            wavWriter.Flush();

            await clientWebSocket.SendAsync(wavStream.ToArray(), WebSocketMessageType.Binary, WebSocketMessageFlags.None, cancellationToken);
        }

        audioSource.OnAudioBufferReceived += async b => await clientWebSocket.SendAsync(b.Data, WebSocketMessageType.Binary, WebSocketMessageFlags.None, cancellationToken);
    }
}
