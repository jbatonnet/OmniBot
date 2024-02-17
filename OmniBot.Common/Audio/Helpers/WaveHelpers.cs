using NAudio.Wave;

namespace OmniBot.Common.Audio;

public static class WaveHelpers
{
    public static void SaveWaveFile(string path, AudioFormat audioFormat, byte[] audioData)
    {
        using (FileStream waveStream = File.OpenWrite(path))
        using (WaveFileWriter waveWriter = new WaveFileWriter(waveStream, audioFormat.ToWaveFormat()))
            waveWriter.Write(audioData);
    }
    public static void SaveWaveFile(string path, AudioFormat audioFormat, IEnumerable<byte[]> audioData)
    {
        using (FileStream waveStream = File.OpenWrite(path))
        using (WaveFileWriter waveWriter = new WaveFileWriter(waveStream, audioFormat.ToWaveFormat()))
        {
            foreach (var data in audioData)
                waveWriter.Write(data);
        }
    }

    public static byte[] ToWaveBytes(this AudioBuffer audioBuffer)
    {
        using (MemoryStream waveStream = new MemoryStream())
        using (WaveFileWriter waveWriter = new WaveFileWriter(waveStream, audioBuffer.Format.ToWaveFormat()))
        {
            waveWriter.Write(audioBuffer.Data);
            return waveStream.ToArray();
        }
    }

    public static void SaveWaveFile(this AudioBuffer audioBuffer, string path) => SaveWaveFile(path, audioBuffer.Format, audioBuffer.Data);
    public static void SaveWaveFile(this IEnumerable<AudioBuffer> audioBuffers, string path) => SaveWaveFile(path, audioBuffers.First().Format, audioBuffers.Select(b => b.Data));
}
