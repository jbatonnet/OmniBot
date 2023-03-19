namespace OmniBot.Common.Speech;

public delegate void KeywordDetectedEventHandler(TimeSpan timecode);

public interface IContinuousKeywordDetector
{
    event KeywordDetectedEventHandler OnKeywordDetected;

    bool Detecting { get; set; }
}
