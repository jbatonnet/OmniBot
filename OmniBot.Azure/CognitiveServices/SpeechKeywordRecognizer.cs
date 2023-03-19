using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

namespace OmniBot.Azure.CognitiveServices;

public class SpeechKeywordRecognizer : IContinuousKeywordDetector, IDisposable
{
    public event KeywordDetectedEventHandler OnKeywordDetected;

    public bool Detecting { get; set; } = true;

    private readonly ILogger<SpeechKeywordRecognizer> _logger;
    private readonly IAudioSource _audioSource;

    private SpeechConfig speechConfig;
    private KeywordRecognitionModel keywordRecognitionModel;
    private Microsoft.CognitiveServices.Speech.SpeechRecognizer speechRecognizer;

    public SpeechKeywordRecognizer(IAudioSource audioSource, string modelPath) : this(null, audioSource, modelPath) { }
    public SpeechKeywordRecognizer(ILogger<SpeechKeywordRecognizer> logger, IAudioSource audioSource, string modelPath)
    {
        _logger = logger;
        _audioSource = audioSource;

        speechConfig = SpeechConfig.FromEndpoint(new Uri("http://127.0.0.1"), "-");
        keywordRecognitionModel = KeywordRecognitionModel.FromFile(modelPath);

        speechRecognizer = CreateSpeechRecognizer();
        speechRecognizer.StartKeywordRecognitionAsync(keywordRecognitionModel);
    }

    private Microsoft.CognitiveServices.Speech.SpeechRecognizer CreateSpeechRecognizer()
    {
        var speechRecognizer = new Microsoft.CognitiveServices.Speech.SpeechRecognizer(speechConfig, AudioConfig.FromStreamInput(new ContinuousAudioInputStream(_audioSource), AudioStreamFormat.GetWaveFormatPCM((uint)_audioSource.Format.SampleRate, (byte)_audioSource.Format.BitsPerSample, (byte)_audioSource.Format.ChannelCount)));

        speechRecognizer.Recognizing += SpeechRecognizer_Recognizing;
        speechRecognizer.Recognized += SpeechRecognizer_Recognized;

        return speechRecognizer;
    }

    private void SpeechRecognizer_Recognizing(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizingKeyword)
            _logger?.LogTrace("Recognizing keyword");
    }
    private async void SpeechRecognizer_Recognized(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedKeyword)
        {
            _logger?.LogTrace("Recognized keyword");

            if (Detecting)
                OnKeywordDetected?.Invoke(TimeSpan.Zero);

            _ = Task.Run(() => speechRecognizer.Dispose());

            speechRecognizer = CreateSpeechRecognizer();
            await speechRecognizer.StartKeywordRecognitionAsync(keywordRecognitionModel);
        }
    }

    public void Dispose()
    {
        speechRecognizer?.Dispose();
    }
}
