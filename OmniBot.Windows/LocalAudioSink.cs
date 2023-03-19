using System.Collections.Concurrent;

using NAudio.Wave;

using OmniBot.Common.Audio;

namespace OmniBot.Windows
{
    public class LocalAudioSink : IAudioSink
    {
        private class WaveProviderWrapper : IWaveProvider
        {
            public WaveFormat WaveFormat => audioSource?.Format.ToWaveFormat();

            private IAudioSource audioSource;
            private ConcurrentQueue<byte[]> bufferQueue = new();
            private int bufferPosition = 0;

            public void ChangeAudioSource(IAudioSource audioSource)
            {
                if (this.audioSource != null)
                    this.audioSource.OnAudioBufferReceived -= AudioSource_OnAudioBufferReceived;

                this.audioSource = audioSource;

                bufferQueue.Clear();
                bufferPosition = 0;

                if (audioSource != null)
                    audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;

            }
            public int Read(byte[] buffer, int offset, int count)
            {
                int n = 0;

                while (n < count)
                {
                    if (!bufferQueue.TryDequeue(out byte[] nextBuffer))
                    {
                        Array.Clear(buffer, offset + n, count - n);
                        break;
                    }

                    if (nextBuffer.Length - bufferPosition > count - n)
                    {
                        Array.Copy(nextBuffer, bufferPosition, buffer, offset + n, count - n);
                        bufferPosition += count - n;
                        break;
                    }

                    Array.Copy(nextBuffer, bufferPosition, buffer, offset + n, nextBuffer.Length - bufferPosition);
                    bufferPosition = 0;
                    n += nextBuffer.Length;
                }

                return n;
            }

            private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
            {
                bufferQueue.Enqueue(audioBuffer.Data);
            }
        }

        public AudioFormat Format => waveOutEvent?.OutputWaveFormat.ToAudioFormat() ?? AudioFormat.Default;

        private WaveProviderWrapper waveProviderWrapper = new();
        private WaveOutEvent waveOutEvent;

        public LocalAudioSink()
        {
        }

        public Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default)
        {
            if (waveOutEvent != null)
            {
                waveOutEvent.Stop();
                waveOutEvent.Dispose();
            }

            waveProviderWrapper.ChangeAudioSource(audioSource);

            if (audioSource != null)
            {
                waveOutEvent = new WaveOutEvent();
                waveOutEvent.Init(waveProviderWrapper);
                waveOutEvent.Play();

                if (cancellationToken != default)
                {
                    Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(t =>
                    {
                        PlayAsync(null);
                    });
                }
            }

            return Task.CompletedTask;
        }
    }
}
