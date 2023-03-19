namespace OmniBot.Common.Speech;

public interface ISpeechSynthesizer
{
    Task<SpeechRecording> SynthesizeAsync(string text, Language language = null);
}
