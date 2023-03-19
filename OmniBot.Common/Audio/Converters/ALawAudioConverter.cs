using NAudio.Codecs;

namespace OmniBot.Common.Audio.Converters;

public class ALawAudioConverter
{
    public ALawAudioConverter()
    {
        // Source
        // - sampleRate: 8000
        // - bitsPerSample: 8
        // - channels: 1

        // Destination
        // - sampleRate: 8000
        // - bitsPerSample: 16
        // - channels: 1
    }

    public int EncodeAudio(byte[] sourceBuffer, int sourceOffset, int sourceSize, byte[] destinationBuffer, int destinationOffset)
    {
        using MemoryStream sourceStream = new MemoryStream(sourceBuffer, sourceOffset, sourceSize);
        using BinaryReader sourceReader = new BinaryReader(sourceStream);

        using MemoryStream destinationStream = new MemoryStream(destinationBuffer, destinationOffset, sourceSize / 2);
        using BinaryWriter destinationWriter = new BinaryWriter(destinationStream);

        while (sourceStream.Position < sourceStream.Length)
        {
            short sourceSample = sourceReader.ReadInt16();
            byte destinationSample = ALawEncoder.LinearToALawSample(sourceSample);
            destinationWriter.Write(destinationSample);
        }

        return (int)destinationStream.Position;
    }
    public byte[] EncodeAudio(byte[] sourceBuffer, int sourceOffset, int sourceSize)
    {
        byte[] buffer = new byte[sourceSize / 2];
        EncodeAudio(sourceBuffer, sourceOffset, sourceSize, buffer, 0);

        return buffer;
    }
    public byte[] EncodeAudio(byte[] sourceBuffer) => EncodeAudio(sourceBuffer, 0, sourceBuffer.Length);


    public int DecodeAudio(byte[] sourceBuffer, int sourceOffset, int sourceSize, byte[] destinationBuffer, int destinationOffset)
    {
        using MemoryStream sourceStream = new MemoryStream(sourceBuffer, sourceOffset, sourceSize);
        using BinaryReader sourceReader = new BinaryReader(sourceStream);

        using MemoryStream destinationStream = new MemoryStream(destinationBuffer, destinationOffset, sourceSize * 2);
        using BinaryWriter destinationWriter = new BinaryWriter(destinationStream);

        while (sourceStream.Position < sourceStream.Length)
        {
            byte sourceSample = sourceReader.ReadByte();
            short destinationSample = ALawDecoder.ALawToLinearSample(sourceSample);
            destinationWriter.Write(destinationSample);
        }

        return (int)destinationStream.Position;
    }
    public byte[] DecodeAudio(byte[] sourceBuffer, int sourceOffset, int sourceSize)
    {
        byte[] buffer = new byte[sourceSize * 2];
        DecodeAudio(sourceBuffer, sourceOffset, sourceSize, buffer, 0);

        return buffer;
    }
    public byte[] DecodeAudio(byte[] sourceBuffer) => DecodeAudio(sourceBuffer, 0, sourceBuffer.Length);
}
