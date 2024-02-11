using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

using Whisper.net;

namespace OmniBot.ML;

public class WhisperSpeechTranscriber : ISpeechTranscriber
{
    public string Prompt { get; set; }

    private WhisperFactory whisperFactory;

    public WhisperSpeechTranscriber(string modelPath = "Models/whisper-cpp-tiny-q5_1.ggml")
    {
        whisperFactory = WhisperFactory.FromPath(modelPath);
    }

    public async Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language languageHint = null)
    {
        // Build the Whisper processor
        var builder = whisperFactory.CreateBuilder()
            .WithProbabilities()
            .WithSingleSegment();

        if (!string.IsNullOrWhiteSpace(Prompt))
            builder = builder.WithPrompt(Prompt);

        if (languageHint != null)
            builder = builder.WithLanguage(languageHint.GetTwoLettersCode());
        else
            builder = builder.WithLanguageDetection();

        var processor = builder.Build();

        // Process the audio buffer
        var convertedAudioBuffer = audioBuffer.ConvertTo(AudioFormat.Default);
        float[] samples = convertedAudioBuffer.GetNormalizedSamples();

        var segments = processor.ProcessAsync(samples);
        var segmentsEnumerator = segments.GetAsyncEnumerator();

        if (!await segmentsEnumerator.MoveNextAsync())
            return null;
        
        var segment = segmentsEnumerator.Current;
        if (segment == null)
            return null;

        // Console.WriteLine($"{segment.Start}->{segment.End}: {segment.Text} ({segment.Language}, {segment.Probability})");

        var speechRecording = new SpeechRecording(audioBuffer)
        {
            Language = Language.GetByIetfTag(segment.Language) ?? languageHint,
            Transcription = segment.Text
        };

        return speechRecording;
    }
}
