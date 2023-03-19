using NAudio.Wave;

using OmniBot.Common.Audio.Converters;

namespace OmniBot.Common.Audio;

public class AudioBuffer
{
    public AudioFormat Format { get; init; }
    public byte[] Data { get; init; }
    public TimeSpan Timecode { get; init; }

    public TimeSpan GetDuration() => Format.GetDuration(Data.Length);

    public byte[] ToWaveData()
    {
        using (MemoryStream waveStream = new MemoryStream())
        using (WaveFileWriter waveWriter = new WaveFileWriter(waveStream, Format.ToWaveFormat()))
        {
            waveWriter.Write(Data);
            return waveStream.ToArray();
        }
    }
    public AudioBuffer ConvertTo(AudioFormat audioFormat)
    {
        var linearAudioConverter = new LinearAudioConverter(Format, audioFormat);

        return new AudioBuffer()
        {
            Format = audioFormat,
            Data = linearAudioConverter.ConvertAudio(Data),
            Timecode = Timecode
        };
    }
    public float[] GetNormalizedSamples()
    {
        float[] samples = new float[Data.Length / Format.GetSampleSize()];

        using (var dataStream = new MemoryStream(Data))
        using (var dataReader = new BinaryReader(dataStream))
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (Format.BitsPerSample == 8)
                {
                    long avgSample = 0;

                    for (int c = 0; c < Format.ChannelCount; c++)
                        avgSample += dataReader.ReadByte();

                    samples[i] = (float)avgSample / Format.ChannelCount / byte.MaxValue;
                }
                else if (Format.BitsPerSample == 16)
                {
                    long avgSample = 0;

                    for (int c = 0; c < Format.ChannelCount; c++)
                        avgSample += dataReader.ReadInt16();

                    samples[i] = (float)avgSample / Format.ChannelCount / short.MaxValue;
                }
                else
                    throw new NotSupportedException();
            }
        }

        return samples;
    }

    public static AudioBuffer GenerateSilence(AudioFormat format, TimeSpan duration)
    {
        AudioBuffer audioBuffer = new AudioBuffer()
        {
            Format = format,
            Data = new byte[(int)(duration.TotalSeconds * format.SampleRate) * format.GetSampleSize()],
            Timecode = TimeSpan.Zero
        };

        return audioBuffer;
    }

    public static AudioBuffer FromWaveFile(string path)
    {
        using (FileStream waveStream = File.OpenRead(path))
            return FromWaveStream(waveStream);
    }
    public static AudioBuffer FromWaveData(byte[] waveData)
    {
        using (MemoryStream waveStream = new MemoryStream(waveData))
            return FromWaveStream(waveStream);
    }
    public static AudioBuffer FromWaveStream(Stream waveStream)
    {
        using (WaveFileReader waveReader = new WaveFileReader(waveStream))
        {
            using (MemoryStream rawStream = new MemoryStream())
            {
                waveReader.CopyTo(rawStream);

                AudioBuffer audioBuffer = new AudioBuffer()
                {
                    Format = waveReader.WaveFormat.ToAudioFormat(),
                    Data = rawStream.ToArray(),
                    Timecode = TimeSpan.Zero
                };

                return audioBuffer;
            }
        }
    }
}
