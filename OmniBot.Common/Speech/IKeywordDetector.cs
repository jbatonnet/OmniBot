using OmniBot.Common.Audio;

namespace OmniBot.Common.Speech;

public interface IKeywordDetector
{
    Task<TimeSpan?> DetectAsync(AudioBuffer audioBuffer);
}
