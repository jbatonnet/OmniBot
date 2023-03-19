namespace OmniBot.Common.Speech;

public delegate void SpeechTranscribedEventHandler(SpeechRecording speechTranscription);

public interface IContinuousSpeechTranscriber
{
    event SpeechTranscribedEventHandler OnSpeechTranscribed;

    bool Transcribing { get; set; }
}
