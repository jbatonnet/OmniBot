namespace OmniBot.Common.Audio;

public delegate void AudioBufferReceivedEventHandler(AudioBuffer audioBuffer);

public interface IAudioSource
{
    event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    bool Listening { get; set; }
    AudioFormat Format { get; }
}
