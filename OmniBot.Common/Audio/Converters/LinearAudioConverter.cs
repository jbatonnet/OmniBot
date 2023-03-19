namespace OmniBot.Common.Audio.Converters;

public class LinearAudioConverter
{
    private struct Sample
    {
        public double[] Channels;

        public Sample(int channelCount)
        {
            Channels = new double[channelCount];
        }
    }

    public readonly AudioFormat SourceFormat;
    public readonly AudioFormat DestinationFormat;

    private readonly bool sameFormat;

    public LinearAudioConverter(AudioFormat sourceFormat, AudioFormat destinationFormat)
    {
        // Supports only 1 or 2 channels
        if (sourceFormat.ChannelCount < 1 || sourceFormat.ChannelCount > 2)
            throw new NotSupportedException();
        if (destinationFormat.ChannelCount < 1 || destinationFormat.ChannelCount > 2)
            throw new NotSupportedException();

        // Supports only 8, 16 or 32 bits per sample
        if (sourceFormat.BitsPerSample != 8 && sourceFormat.BitsPerSample != 16 && sourceFormat.BitsPerSample != 32)
            throw new NotSupportedException();
        if (destinationFormat.BitsPerSample != 8 && destinationFormat.BitsPerSample != 16 && destinationFormat.BitsPerSample != 32)
            throw new NotSupportedException();

        // Supports only integer sample rate scaling
        if ((sourceFormat.SampleRate % 8000 > 0) && (destinationFormat.SampleRate % 8000 > 0))
            throw new NotSupportedException();
        if ((sourceFormat.SampleRate % destinationFormat.SampleRate > 0) && (destinationFormat.SampleRate % sourceFormat.SampleRate > 0))
            throw new NotSupportedException();

        // Supports only up to 3 times rate scaling
        if ((sourceFormat.SampleRate / destinationFormat.SampleRate > 3) || (destinationFormat.SampleRate / sourceFormat.SampleRate > 3))
            throw new NotSupportedException();

        SourceFormat = sourceFormat;
        DestinationFormat = destinationFormat;

        sameFormat = SourceFormat.SampleRate == DestinationFormat.SampleRate && SourceFormat.BitsPerSample == DestinationFormat.BitsPerSample && SourceFormat.ChannelCount == DestinationFormat.ChannelCount;
    }

    public int ConvertAudio(byte[] sourceBuffer, int sourceOffset, int sourceSize, byte[] destinationBuffer, int destinationOffset)
    {
        if (sameFormat)
        {
            if (destinationBuffer.Length - destinationOffset < sourceSize)
                throw new NotSupportedException();

            Array.Copy(sourceBuffer, sourceOffset, destinationBuffer, destinationOffset, sourceSize);

            return sourceSize;
        }

        int sourceSampleSize = SourceFormat.GetSampleSize();
        int destinationSampleSize = DestinationFormat.GetSampleSize();
        
        // Supports only aligned conversions
        if (sourceSize % sourceSampleSize > 0)
            throw new NotSupportedException();

        int sampleCount = sourceSize / sourceSampleSize;

        int scaleUp = Math.Max(1, DestinationFormat.SampleRate / SourceFormat.SampleRate);
        int scaleDown = Math.Max(1, SourceFormat.SampleRate / DestinationFormat.SampleRate);
        int destinationSize = sampleCount * destinationSampleSize * scaleUp / scaleDown;

        // Make sure we have enough space in destination buffer
        if (destinationSize > (destinationBuffer.Length - destinationOffset))
            throw new NotSupportedException();

        // OK, now let's convert data
        using MemoryStream sourceStream = new MemoryStream(sourceBuffer, sourceOffset, sourceSize);
        using BinaryReader sourceReader = new BinaryReader(sourceStream);

        using MemoryStream destinationStream = new MemoryStream(destinationBuffer, destinationOffset, destinationSize);
        using BinaryWriter destinationWriter = new BinaryWriter(destinationStream);

        int conversionChannelCount = Math.Max(SourceFormat.ChannelCount, DestinationFormat.ChannelCount);

        Sample readSample()
        {
            Sample sample = new Sample(conversionChannelCount);

            for (int i = 0; i < SourceFormat.ChannelCount; i++)
            {
                switch (SourceFormat.BitsPerSample)
                {
                    case 8: sample.Channels[i] = sourceReader.ReadSByte() / (double)sbyte.MaxValue; break;
                    case 16: sample.Channels[i] = sourceReader.ReadInt16() / (double)short.MaxValue; break;
                    case 32: sample.Channels[i] = sourceReader.ReadInt32() / (double)int.MaxValue; break;
                    default: throw new NotSupportedException();
                }
            }

            if (SourceFormat.ChannelCount == 1 && DestinationFormat.ChannelCount == 2)
                sample.Channels[1] = sample.Channels[0];
            if (SourceFormat.ChannelCount == 2 && DestinationFormat.ChannelCount == 1)
                sample.Channels[0] = sample.Channels[1] = (sample.Channels[0] + sample.Channels[1]) / 2;

            return sample;
        }
        void writeSample(Sample sample)
        {
            for (int i = 0; i < DestinationFormat.ChannelCount; i++)
            {
                switch (DestinationFormat.BitsPerSample)
                {
                    case 8: destinationWriter.Write((sbyte)(sample.Channels[i] * sbyte.MaxValue)); break;
                    case 16: destinationWriter.Write((short)(sample.Channels[i] * short.MaxValue)); break;
                    case 32: destinationWriter.Write((int)(sample.Channels[i] * int.MaxValue)); break;
                    default: throw new NotSupportedException();
                }
            }
        }

        Sample lastSample = new Sample(conversionChannelCount);

        while (sourceStream.Position < sourceStream.Length)
        {
            long bytesLeft = sourceStream.Length - sourceStream.Position;

            // 1/3x (ie. 48k > 16k)
            if (SourceFormat.SampleRate == DestinationFormat.SampleRate * 3)
            {
                if (bytesLeft < sourceSampleSize * 3)
                    break;

                Sample firstSample = readSample();
                Sample secondSample = readSample();
                Sample thirdSample = readSample();

                Sample sample = new Sample(conversionChannelCount);

                for (int i = 0; i < conversionChannelCount; i++)
                    sample.Channels[i] = (firstSample.Channels[i] + secondSample.Channels[i] + thirdSample.Channels[i]) / 3;

                writeSample(sample);
            }

            // 1/2x (ie. 32k > 16k)
            else if (SourceFormat.SampleRate == DestinationFormat.SampleRate * 2)
            {
                if (bytesLeft < sourceSampleSize * 2)
                    break;

                Sample firstSample = readSample();
                Sample secondSample = readSample();

                Sample sample = new Sample(conversionChannelCount);

                for (int i = 0; i < conversionChannelCount; i++)
                    sample.Channels[i] = (firstSample.Channels[i] + secondSample.Channels[i]) / 2;

                writeSample(sample);
            }

            // 1x (ie. 16k > 16k)
            else if(SourceFormat.SampleRate == DestinationFormat.SampleRate)
                writeSample(readSample());

            // 2x (ie. 16k > 32k)
            else if(SourceFormat.SampleRate * 2 == DestinationFormat.SampleRate)
            {
                Sample sample = readSample();
                
                writeSample(sample);
                writeSample(sample);
                
                lastSample = sample;
            }

            // 3x (ie. 16k > 48k)
            else if(SourceFormat.SampleRate * 3 == DestinationFormat.SampleRate)
            {
                Sample sample = readSample();

                writeSample(sample);
                writeSample(sample);
                writeSample(sample);

                lastSample = sample;
            }
        }

        return destinationSize;
    }
    public byte[] ConvertAudio(byte[] sourceBuffer, int sourceOffset, int sourceSize)
    {
        if (sameFormat)
        {
            byte[] copyBuffer = new byte[sourceSize];
            Array.Copy(sourceBuffer, sourceOffset, copyBuffer, 0, sourceSize);
            return copyBuffer;
        }

        int sourceSampleSize = SourceFormat.GetSampleSize();
        int destinationSampleSize = DestinationFormat.GetSampleSize();

        // Supports only aligned conversions
        if (sourceSize % sourceSampleSize > 0)
            throw new NotSupportedException();

        int sampleCount = sourceSize / sourceSampleSize;

        int scaleUp = Math.Max(1, DestinationFormat.SampleRate / SourceFormat.SampleRate);
        int scaleDown = Math.Max(1, SourceFormat.SampleRate / DestinationFormat.SampleRate);
        int destinationSize = sampleCount * destinationSampleSize * scaleUp / scaleDown;

        byte[] buffer = new byte[destinationSize];
        ConvertAudio(sourceBuffer, sourceOffset, sourceSize, buffer, 0);

        return buffer;
    }
    public byte[] ConvertAudio(byte[] sourceBuffer) => ConvertAudio(sourceBuffer, 0, sourceBuffer.Length);
}
