using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech;

using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

namespace OmniBot.Azure.CognitiveServices;

public class KeywordRecognizer : IKeywordDetector, IContinuousKeywordDetector
{
    public static KeywordRecognizer CreateFromDefaultMicrophone(string modelPath)
    {
        return new KeywordRecognizer(modelPath, AudioConfig.FromDefaultMicrophoneInput());
    }
    public static KeywordRecognizer CreateFromInputDeviceId(string modelPath, string inputDeviceId)
    {
        return new KeywordRecognizer(modelPath, AudioConfig.FromMicrophoneInput(inputDeviceId));
    }
    public static KeywordRecognizer CreateFromAudioSource(string modelPath, IAudioSource audioSource)
    {
        return new KeywordRecognizer(modelPath, AudioConfig.FromStreamInput(new ContinuousAudioInputStream(audioSource)));
    }
    public static KeywordRecognizer CreateForDiscreteRecordings(string modelPath)
    {
        return new KeywordRecognizer(modelPath, null);
    }

    public event KeywordDetectedEventHandler OnKeywordDetected;

    private readonly AudioConfig _audioConfig;

    private KeywordRecognitionModel keywordRecognitionModel;
    private Microsoft.CognitiveServices.Speech.KeywordRecognizer defaultKeywordRecognizer;

    public bool Detecting { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    private KeywordRecognizer(string modelPath, AudioConfig audioConfig)
    {
        _audioConfig = audioConfig;

        keywordRecognitionModel = KeywordRecognitionModel.FromFile(modelPath);

        if (_audioConfig != null)
        {
            defaultKeywordRecognizer = new Microsoft.CognitiveServices.Speech.KeywordRecognizer(audioConfig);
            
            defaultKeywordRecognizer.Recognized += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Result.Text.Trim()))
                    return;

                OnKeywordDetected?.Invoke(TimeSpan.Zero);
            };
        }
    }

    public Task StartDetecting()
    {
        throw new NotImplementedException();
    }
    public Task StopDetecting()
    {
        throw new NotImplementedException();
    }

    public Task<TimeSpan?> DetectAsync(AudioBuffer audioBuffer)
    {
        TaskCompletionSource<TimeSpan?> detectionCompletionSouce = new TaskCompletionSource<TimeSpan?>();

        var audioConfig = AudioConfig.FromStreamInput(new DiscreteAudioInputStream(audioBuffer), AudioStreamFormat.GetWaveFormatPCM((uint)audioBuffer.Format.SampleRate, (byte)audioBuffer.Format.BitsPerSample, (byte)audioBuffer.Format.ChannelCount));
        var keywordRecognizer = new Microsoft.CognitiveServices.Speech.KeywordRecognizer(audioConfig);

        keywordRecognizer.Recognized += async (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedKeyword)
                detectionCompletionSouce.TrySetResult(TimeSpan.FromTicks((long)e.Offset));
        };
        keywordRecognizer.Canceled += (s, e) =>
        {
            detectionCompletionSouce.TrySetResult(null);
        };

        _ = keywordRecognizer.RecognizeOnceAsync(keywordRecognitionModel);

        return detectionCompletionSouce.Task;
    }
}
