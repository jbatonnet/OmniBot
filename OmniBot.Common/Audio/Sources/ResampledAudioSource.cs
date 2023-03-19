namespace OmniBot.Common.Audio;

public class ResampledAudioSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public bool Listening
    {
        get => _audioSource.Listening;
        set => _audioSource.Listening = value;
    }
    public AudioFormat Format { get; set; }

    private readonly IAudioSource _audioSource;

    public ResampledAudioSource(IAudioSource audioSource, int sampleRate)
    {
        _audioSource = audioSource;
        _audioSource.OnAudioBufferReceived += b =>
        {
            AudioBuffer audioBuffer = new AudioBuffer()
            {
                Format = Format,
                Data = b.Data,
                Timecode = b.Timecode * b.Format.SampleRate / Format.SampleRate
            };

            OnAudioBufferReceived?.Invoke(audioBuffer);
        };

        Format = new AudioFormat() { BitsPerSample = audioSource.Format.BitsPerSample, ChannelCount = audioSource.Format.ChannelCount, SampleRate = sampleRate };
    }
}
