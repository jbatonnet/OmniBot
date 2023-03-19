using NAudio.Wave;

namespace OmniBot.Common.Audio;

public class AudioRecording
{
    public static TimeSpan MinimumSpeechDuration { get; } = TimeSpan.FromMilliseconds(500);

    public Person User { get; init; }
    public byte[] Data { get; init; }
    public TimeSpan Duration { get; init; }

    public AudioFormat ReadAudioFormat()
    {
        using MemoryStream inputStream = new MemoryStream(Data);
        using WaveFileReader inputWaveReader = new WaveFileReader(inputStream);

        return inputWaveReader.WaveFormat.ToAudioFormat();
    }
}
