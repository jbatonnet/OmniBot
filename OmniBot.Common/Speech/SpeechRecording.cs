using OmniBot.Common.Audio;

namespace OmniBot.Common.Speech;

public class SpeechRecording : AudioBuffer
{
    public string Transcription { get; init; }
    public Language Language { get; init; }

    public SpeechRecording() { }
    public SpeechRecording(AudioBuffer audioBuffer)
    {
        Format = audioBuffer?.Format ?? default;
        Data = audioBuffer?.Data;
        Timecode = audioBuffer?.Timecode ?? default;
    }
}
