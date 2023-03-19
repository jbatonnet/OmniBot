namespace OmniBot.Common.Audio;

public class HttpAudioSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public AudioFormat Format { get; private set; }
    public bool Listening { get; set; } = true;

    private readonly string _endpointUrl;

    private TimeSpan timecode = TimeSpan.Zero;

    public HttpAudioSource(string endpointUrl)
    {
        _endpointUrl = endpointUrl;
    }

    public async Task ConnectAsync()
    {
        HttpClient httpClient = new HttpClient();

        httpClient.Timeout = TimeSpan.FromSeconds(5);

        var response = await httpClient.GetAsync(_endpointUrl, HttpCompletionOption.ResponseHeadersRead);
        var responseStream = response.Content.ReadAsStream();

        // We need to cache the header because NAudio tries to read stream.Length, not available from HttpClient
        byte[] headerBuffer = new byte[1024];
        responseStream.Read(headerBuffer, 0, 100);

        using MemoryStream headerStream = new MemoryStream(headerBuffer);

        var waveFileChunkReader = new WaveFileChunkReaderEx();
        waveFileChunkReader.ReadWaveHeader(headerStream);

        Format = waveFileChunkReader.WaveFormat.ToAudioFormat();

        //RawSourceWaveStream rawSourceWaveStream = new RawSourceWaveStream(responseStream, wavFormat);

        _ = Task.Run(() =>
        {
            byte[] buffer = new byte[4 * 1024];

            while (true)
            {
                int read = responseStream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                if (Listening)
                {
                    AudioBuffer audioBuffer = new AudioBuffer()
                    {
                        Format = Format,
                        Data = new byte[read],
                        Timecode = timecode
                    };

                    timecode += audioBuffer.GetDuration();

                    Array.Copy(buffer, audioBuffer.Data, read);

                    OnAudioBufferReceived?.Invoke(audioBuffer);
                }
            }
        });
    }
}
