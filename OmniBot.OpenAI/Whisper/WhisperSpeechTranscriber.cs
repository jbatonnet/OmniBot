using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace OmniBot.OpenAI.Whisper
{
    public class WhisperSpeechTranscriber : ISpeechTranscriber
    {
        private OpenAIService openAiService;

        public WhisperSpeechTranscriber(string apiKey)
        {
            OpenAiOptions openAiOptions = new OpenAiOptions();
            openAiOptions.ApiKey = apiKey;

            openAiService = new OpenAIService(openAiOptions);
        }

        public async Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language languageHint)
        {
            var audioCreateTranscriptionRequest = new AudioCreateTranscriptionRequest();

            // audioCreateTranscriptionRequest.Prompt can be used to give hints, about members for example
            audioCreateTranscriptionRequest.Model = "whisper-1";
            audioCreateTranscriptionRequest.File = audioBuffer.ToWaveData();
            audioCreateTranscriptionRequest.FileName = "audio.wav";
            audioCreateTranscriptionRequest.ResponseFormat = "verbose_json";
            audioCreateTranscriptionRequest.Language = languageHint?.GetTwoLettersCode();

            var response = await openAiService.Audio.CreateTranscription(audioCreateTranscriptionRequest);

            SpeechRecording speechTranscription = new SpeechRecording(audioBuffer)
            {
                Transcription = response.Text,
                Language = response?.Language switch
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
