using NAudio.Wave;

namespace OmniBot.Common.Audio;

public struct AudioFormat : IEquatable<AudioFormat>
{
    public static AudioFormat Default { get; } = new AudioFormat() { SampleRate = 16000, ChannelCount = 1, BitsPerSample = 16 };

    public int SampleRate;
    public int ChannelCount;
    public int BitsPerSample;

    public AudioFormat(int sampleRate, int channelCount, int bitsPerSample)
    {
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        BitsPerSample = bitsPerSample;
    }

    public int GetSampleSize() => BitsPerSample / 8 * ChannelCount;
    public TimeSpan GetDuration(int bytes) => TimeSpan.FromSeconds((double)(bytes / GetSampleSize()) / SampleRate);
    public override string ToString() => $"{{ SampleRate: {SampleRate}, ChannelCount: {ChannelCount}, BitPerSample: {BitsPerSample} }}";

    public override bool Equals(object obj)
    {
        return obj is AudioFormat && Equals((AudioFormat)obj);
    }
    public bool Equals(AudioFormat other)
    {
        return this == other;
    }
    public static bool operator ==(AudioFormat left, AudioFormat right)
    {
        return left.SampleRate == right.SampleRate && left.ChannelCount == right.ChannelCount && left.BitsPerSample == right.BitsPerSample;
    }
    public static bool operator !=(AudioFormat left, AudioFormat right)
    {
        return !(left == right);
    }
}

public static class AudioFormatHelpers
{
    public static WaveFormat ToWaveFormat(this AudioFormat audioFormat)
    {
        return new WaveFormat(audioFormat.SampleRate, audioFormat.BitsPerSample, audioFormat.ChannelCount);
    }
    public static AudioFormat ToAudioFormat(this WaveFormat waveFormat)
    {
        return new AudioFormat(waveFormat.SampleRate, waveFormat.Channels, waveFormat.BitsPerSample);
    }
}
