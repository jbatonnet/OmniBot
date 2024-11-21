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

            private byte[] currentBuffer = null;
            private int currentBufferPosition = 0;

            public void ChangeAudioSource(IAudioSource audioSource)
            {
                if (this.audioSource != null)
                    this.audioSource.OnAudioBufferReceived -= AudioSource_OnAudioBufferReceived;

                this.audioSource = audioSource;

                bufferQueue.Clear();
                currentBufferPosition = 0;
                currentBuffer = null;

                if (audioSource != null)
                    audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;

            }
            public int Read(byte[] buffer, int offset, int count)
            {
                int n = 0;

                while (n < count)
                {
                    if (currentBuffer == null && !bufferQueue.TryDequeue(out currentBuffer))
                    {
                        Array.Clear(buffer, offset + n, count - n);
                        break;
                    }

                    if (currentBuffer.Length - currentBufferPosition > count - n)
                    {
                        Array.Copy(currentBuffer, currentBufferPosition, buffer, offset + n, count - n);
                        currentBufferPosition += count - n;
                        break;
                    }

                    Array.Copy(currentBuffer, currentBufferPosition, buffer, offset + n, currentBuffer.Length - currentBufferPosition);
                    n += currentBuffer.Length - currentBufferPosition;

                    currentBufferPosition = 0;
                    currentBuffer = null;
                }

                return count;
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
