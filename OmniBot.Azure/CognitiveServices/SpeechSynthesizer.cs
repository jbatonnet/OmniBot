using Microsoft.CognitiveServices.Speech;

using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

namespace OmniBot.Azure.CognitiveServices
{
    public class SpeechSynthesizer : ISpeechSynthesizer
    {
        public float VoiceSpeed { get; set; } = 1.15f;
        public string Style { get; set; } = "cheerful";

        private SpeechConfig speechConfig;
        private Microsoft.CognitiveServices.Speech.SpeechSynthesizer speechSynthesizer;

        private AutoResetEvent speakEndEvent = new AutoResetEvent(false);

        private string defaultVoice;
        private IDictionary<string, string> languageVoices = new Dictionary<string, string>();

        public SpeechSynthesizer(string speechKey, string serviceRegion, SynthesisVoiceGender gender)
        {
            speechConfig = SpeechConfig.FromSubscription(speechKey, serviceRegion);
            speechConfig.SetProfanity(ProfanityOption.Raw);
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm);

            speechSynthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(speechConfig, null);
            speechSynthesizer.BookmarkReached += (s, e) => speakEndEvent.Set();

            // Voice list: https://speech.microsoft.com/portal

            if (gender == SynthesisVoiceGender.Male)
            {
                defaultVoice = "en-CA-LiamNeural";
                languageVoices[Language.French.GetIetfTag()] = "fr-BE-GerardNeural";
                languageVoices[Language.English.GetIetfTag()] = "en-CA-LiamNeural";
            }
            else
            {
                defaultVoice = "en-US-JennyMultilingualNeural";
                languageVoices[Language.French.GetIetfTag()] = "fr-FR-YvetteNeural";
                languageVoices[Language.English.GetIetfTag()] = "en-US-JennyNeural";
            }
        }

        public async Task<SpeechRecording> SynthesizeAsync(string text, Language language = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string languageTag = language?.GetIetfTag() ?? "en-US";

            if (!languageVoices.TryGetValue(languageTag, out string voice))
                voice = defaultVoice;

            //List<string> words = message.Split(' ').ToList();
            //words.Insert(Math.Max(0, words.Count - 2), "<bookmark mark=\"mark\"/>");
            //message = string.Join(" ", words);

            string ssml = $@"
                <speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xmlns:mstts=""http://www.w3.org/2001/mstts"" xml:lang=""{languageTag}"">
                    <voice name=""{voice}"">
                        <lang xml:lang=""{languageTag}"">
                            <mstts:silence type=""Sentenceboundary"" value=""30ms"" />
                            <mstts:silence type=""comma-exact"" value=""20ms"" />
                            <mstts:silence type=""semicolon-exact"" value=""100ms"" />
                            <mstts:silence type=""enumerationcomma-exact"" value=""150ms"" />

                            <prosody rate=""{(VoiceSpeed > 0 ? "+" : "-")}{(int)(VoiceSpeed * 100 - 100)}%"">
                                <mstts:express-as style=""{Style}"">
                                    {text.Trim()}
                                </mstts:express-as>
                            </prosody>
                        </lang>
                    </voice>
                </speak>";

            var result = await speechSynthesizer.SpeakSsmlAsync(ssml);
            var audioBuffer = AudioBuffer.FromWaveData(result.AudioData);
            
            return new SpeechRecording(audioBuffer)
            {
                Transcription = text.Trim(),
                Language = language ?? Language.English
            };
        }
    }
}
