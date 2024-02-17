using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

using OpenAI_API;

namespace OmniBot.OpenAI.Whisper
{
    public class WhisperSpeechTranscriber : ISpeechTranscriber
    {
        private string _apiKey;
        private string _apiEndpoint;

        private OpenAIAPI openAiApi;

        public WhisperSpeechTranscriber(string apiKey) : this(apiKey, "https://api.openai.com/v1") { }
        public WhisperSpeechTranscriber(string apiKey, string apiEndpoint)
        {
            _apiKey = apiKey;
            _apiEndpoint = apiEndpoint;

            openAiApi = new OpenAIAPI()
            {
                ApiUrlFormat = apiEndpoint + "/{1}",
                Auth = new APIAuthentication(apiKey)
            };
        }

        public async Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language languageHint)
        {
            byte[] waveBytes = audioBuffer.ToWaveBytes();
            using MemoryStream waveStream = new MemoryStream(waveBytes);

            var result = await openAiApi.Transcriptions.GetWithDetailsAsync
            (
                audioStream: waveStream,
                filename: "audio.wav",
                language: languageHint?.GetTwoLettersCode()
            );

            SpeechRecording speechTranscription = new SpeechRecording(audioBuffer)
            {
                Transcription = result.text,
                Language = result.language switch
                {
                    "english" => Language.English,
                    "french" => Language.French,
                    _ => Language.English
                }
            };

            return speechTranscription;
        }
    }
}
