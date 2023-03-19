using NAudio.Wave;

namespace OmniBot.Common.Audio;

public class WaveAudioSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public bool Listening { get; set; }
    public AudioFormat Format { get; private set; }

    private byte[] waveData;

    public WaveAudioSource(string path)
    {
        waveData = File.ReadAllBytes(path);
        ReadWaveFormat();
    }
    public WaveAudioSource(byte[] data)
    {
        waveData = new byte[data.Length];
        Array.Copy(data, waveData, data.Length);
        ReadWaveFormat();
    }
    public WaveAudioSource(Stream stream)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            stream.CopyTo(memoryStream);
            waveData = memoryStream.ToArray();
        }

        ReadWaveFormat();
    }

    public async Task PlayAsync()
    {
        await Task.Yield();

        TimeSpan timecode = TimeSpan.Zero;

        using (MemoryStream waveStream = new MemoryStream(waveData))
        using (WaveFileReader waveReader = new WaveFileReader(waveStream))
        {
            byte[] buffer = new byte[Format.GetSampleSize() * Format.SampleRate / 20]; // 50ms

            while (true)
            {
                int count = waveReader.Read(buffer, 0, buffer.Length);
                if (count <= 0)
                    break;

                if (Listening)
                {
                    AudioBuffer audioBuffer = new AudioBuffer()
                    {
                        Format = Format,
                        Data = new byte[count],
                        Timecode = timecode
                    };

                    timecode += audioBuffer.GetDuration();

                    OnAudioBufferReceived?.Invoke(audioBuffer);
                }
            }
        }
    }

    private void ReadWaveFormat()
    {
        using (MemoryStream waveStream = new MemoryStream(waveData))
        using (WaveFileReader waveReader = new WaveFileReader(waveStream))
            Format = waveReader.WaveFormat.ToAudioFormat();
    }
}
