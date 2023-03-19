using OmniBot.Common.Audio;

namespace OmniBot.Common.Speech;

public interface ISpeechTranscriber
{
    Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language languageHint = null);
}
