namespace OmniBot.Common.Audio;

public interface IAudioSink
{
    AudioFormat Format { get; }

    Task PlayAsync(IAudioSource audioSource, CancellationToken cancellationToken = default);
}
