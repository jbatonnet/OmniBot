using System.ClientModel;

using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

using OpenAI;
using OpenAI.Audio;

namespace OmniBot.OpenAI.Whisper;

public class WhisperSpeechTranscriber : ISpeechTranscriber
{
    private string _apiKey;
    private string _apiEndpoint;

    private OpenAIClient openAIClient;

    public WhisperSpeechTranscriber(string apiKey) : this(apiKey, OpenAIOptions.OpenAIEndpoint) { }
    public WhisperSpeechTranscriber(string apiKey, string apiEndpoint)
    {
        _apiKey = apiKey;
        _apiEndpoint = apiEndpoint;

        var options = new OpenAIClientOptions()
        {
            Endpoint = new Uri(apiEndpoint ?? OpenAIOptions.OpenAIEndpoint)
        };

        openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
    }

    public async Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language languageHint)
    {
        byte[] waveBytes = audioBuffer.ToWaveBytes();
        using MemoryStream waveStream = new MemoryStream(waveBytes);

        var audioClient = openAIClient.GetAudioClient("whisper-1");

        var result = await audioClient.TranscribeAudioAsync(
            audio: waveStream,
            audioFilename: "audio.wav",
            options: new AudioTranscriptionOptions()
            {
                Language = languageHint?.GetTwoLettersCode()
            });

        var speechTranscription = new SpeechRecording(audioBuffer)
        {
            Transcription = result.Value.Text,
            Language = result.Value.Language switch
            {
                "english" => Language.English,
                "french" => Language.French,
                _ => Language.English
            }
        };

        return speechTranscription;
    }
}
