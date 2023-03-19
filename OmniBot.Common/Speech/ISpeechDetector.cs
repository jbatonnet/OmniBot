namespace OmniBot.Common.Speech;

public delegate void SpeechStartedEventHandler(TimeSpan timecode);
public delegate void SpeechStoppedEventHandler(TimeSpan startTimecode, TimeSpan stopTimecode);

public interface ISpeechDetector
{
    event SpeechStartedEventHandler OnSpeechStarted;
    event SpeechStoppedEventHandler OnSpeechStopped;

    bool Detecting { get; }
}
