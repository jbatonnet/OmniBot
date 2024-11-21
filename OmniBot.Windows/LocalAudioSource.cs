using NAudio.Wave;

using OmniBot.Common.Audio;

namespace OmniBot.Windows;

public class LocalAudioSource : IAudioSource
{
    public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

    public bool Listening { get; set; }
    public AudioFormat Format => _waveIn.WaveFormat.ToAudioFormat();

    private readonly WaveInEvent _waveIn;

    private TimeSpan timecode = TimeSpan.Zero;

    public LocalAudioSource() : this(AudioFormat.Default) { }
    public LocalAudioSource(AudioFormat audioFormat)
    {
        _waveIn = new WaveInEvent();
        _waveIn.WaveFormat = audioFormat.ToWaveFormat();

        _waveIn.RecordingStopped += (s, e) =>
        {
            if (Listening)
                _waveIn.StartRecording();
        };
        _waveIn.DataAvailable += (s, e) =>
        {
            if (!Listening)
                return;

            AudioBuffer audioBuffer = new AudioBuffer()
            {
                Format = Format,
                Data = e.Buffer,
                Timecode = timecode
            };

            timecode += audioBuffer.GetDuration();

            OnAudioBufferReceived?.Invoke(audioBuffer);
        };

        Listening = true;
        _waveIn.StartRecording();
    }
}
