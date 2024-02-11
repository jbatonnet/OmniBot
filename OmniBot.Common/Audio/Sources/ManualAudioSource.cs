namespace OmniBot.Common.Audio;

public class ManualAudioSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public bool Listening { get; set; } = true;
    public AudioFormat Format { get; }

    private TimeSpan timecode = TimeSpan.Zero;

    public ManualAudioSource(AudioFormat audioFormat)
    {
        Format = audioFormat;
    }

    public void SendAudioBuffer(AudioBuffer audioBuffer)
    {
        if (Format != audioBuffer.Format)
            throw new NotSupportedException();

        timecode += audioBuffer.GetDuration();

        if (Listening)
        {
            AudioBuffer resyncedAudioBuffer = new AudioBuffer()
            {
                Format = audioBuffer.Format,
                Data = audioBuffer.Data,
                Timecode = timecode
            };

            OnAudioBufferReceived?.Invoke(resyncedAudioBuffer);
        }
    }
}
