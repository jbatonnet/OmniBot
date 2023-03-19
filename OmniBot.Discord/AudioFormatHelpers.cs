using OmniBot.Common.Audio;

using DiscordAudioFormat = DSharpPlus.VoiceNext.AudioFormat;

namespace OmniBot.Discord
{
    public static class AudioFormatHelpers
    {
        public static DiscordAudioFormat ToDiscordAudioFormat(this AudioFormat audioFormat)
        {
            return new DiscordAudioFormat(audioFormat.SampleRate, audioFormat.ChannelCount);
        }
        public static AudioFormat ToAudioFormat(this DiscordAudioFormat audioFormat)
        {
            return new AudioFormat(audioFormat.SampleRate, audioFormat.ChannelCount, 16);
        }
    }
}
