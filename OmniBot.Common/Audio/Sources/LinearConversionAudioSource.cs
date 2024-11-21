namespace OmniBot.Common.Audio;

public class LinearConversionAudioSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public bool Listening { get; set; } = true;
    public AudioFormat Format { get; init; }

    private readonly IAudioSource _audioSource;

    public LinearConversionAudioSource(IAudioSource audioSource, AudioFormat audioFormat)
    {
        _audioSource = audioSource;
        Format = audioFormat;

        _audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
    }

    public void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        if (!Listening)
            return;

        audioBuffer = audioBuffer.ConvertTo(Format);
        OnAudioBufferReceived?.Invoke(audioBuffer);
    }
}
