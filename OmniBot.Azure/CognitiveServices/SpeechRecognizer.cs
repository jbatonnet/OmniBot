using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

namespace OmniBot.Azure.CognitiveServices
{
    public class SpeechRecognizer : ISpeechTranscriber, IContinuousSpeechTranscriber
    {
        public static SpeechRecognizer CreateFromDefaultMicrophone(ILogger<SpeechRecognizer> logger, string speechKey, string serviceRegion, params Language[] languages)
        {
            return new SpeechRecognizer(logger, speechKey, serviceRegion, AudioConfig.FromDefaultMicrophoneInput(), languages);
        }
        public static SpeechRecognizer CreateFromDefaultMicrophone(string speechKey, string serviceRegion, params Language[] languages) => CreateFromDefaultMicrophone(null, speechKey, serviceRegion, languages);
        public static SpeechRecognizer CreateFromInputDeviceId(ILogger<SpeechRecognizer> logger, string speechKey, string serviceRegion, string inputDeviceId, params Language[] languages)
        {
            return new SpeechRecognizer(logger, speechKey, serviceRegion, AudioConfig.FromMicrophoneInput(inputDeviceId), languages);
        }
        public static SpeechRecognizer CreateFromInputDeviceId(string speechKey, string serviceRegion, string inputDeviceId, params Language[] languages) => CreateFromInputDeviceId(null, speechKey, serviceRegion, inputDeviceId, languages);
        public static SpeechRecognizer CreateFromAudioSource(ILogger<SpeechRecognizer> logger, string speechKey, string serviceRegion, IAudioSource audioSource, params Language[] languages)
        {
            return new SpeechRecognizer(logger, speechKey, serviceRegion, AudioConfig.FromStreamInput(new ContinuousAudioInputStream(audioSource), AudioStreamFormat.GetWaveFormatPCM((uint)audioSource.Format.SampleRate, (byte)audioSource.Format.BitsPerSample, (byte)audioSource.Format.ChannelCount)), languages);
        }
        public static SpeechRecognizer CreateFromAudioSource(string speechKey, string serviceRegion, IAudioSource audioSource, params Language[] languages) => CreateFromAudioSource(null, speechKey, serviceRegion, audioSource, languages);
        public static SpeechRecognizer CreateForDiscreteRecordings(ILogger<SpeechRecognizer> logger, string speechKey, string serviceRegion, params Language[] languages)
        {
            return new SpeechRecognizer(logger, speechKey, serviceRegion, null, languages);
        }
        public static SpeechRecognizer CreateForDiscreteRecordings(string speechKey, string serviceRegion, params Language[] languages) => CreateForDiscreteRecordings(null, speechKey, serviceRegion, languages);

        public event Action OnSpeechDetected;
        public event SpeechTranscribedEventHandler OnSpeechTranscribed;

        public bool Transcribing
        {
            get => transcribing;
            set
            {
                transcribing = value;

                if (transcribing)
                {
                    if (_audioConfig == null)
                        throw new NotSupportedException();

                    _ = defaultSpeechRecognizer.StartContinuousRecognitionAsync();
                }
                else
                    _ = defaultSpeechRecognizer.StopContinuousRecognitionAsync();
            }
        }

        private readonly ILogger<SpeechRecognizer> _logger;
        private readonly AudioConfig _audioConfig;
        private readonly Language[] _languages;

        private bool transcribing = false;
        private SpeechConfig speechConfig;
        private Microsoft.CognitiveServices.Speech.SpeechRecognizer defaultSpeechRecognizer;

        private SpeechRecognizer(ILogger<SpeechRecognizer> logger, string speechKey, string serviceRegion, AudioConfig audioConfig, Language[] languages)
        {
            _logger = logger;
            _audioConfig = audioConfig;

            speechConfig = SpeechConfig.FromSubscription(speechKey, serviceRegion);
            speechConfig.SetProfanity(ProfanityOption.Raw);
            speechConfig.OutputFormat = OutputFormat.Detailed;

            _languages = languages ?? new[] { Language.English };

            if (_audioConfig != null)
            {
                defaultSpeechRecognizer = CreateSpeechRecognizer(_audioConfig, _languages);

                defaultSpeechRecognizer.SpeechEndDetected += (s, e) => _logger?.LogTrace($"Speech end detected (offset: {e.Offset} ticks)");
                defaultSpeechRecognizer.Recognizing += (s, e) => _logger?.LogTrace($"Recognizing speech \"{e.Result.Text}\"");

                defaultSpeechRecognizer.SpeechStartDetected += (s, e) =>
                {
                    _logger?.LogTrace($"Speech start detected (offset: {e.Offset} ticks)");

                    OnSpeechDetected?.Invoke();
                };
                defaultSpeechRecognizer.Recognized += (s, e) =>
                {
                    string transcription = e.Result.Text.Trim();

                    if (string.IsNullOrEmpty(transcription))
                        return;
                    if (transcription.Length < 3)
                        return;
                    if (transcription.ToLower().Contains("sous-titr"))
                        return;

                    _logger?.LogTrace($"Recognized speech \"{e.Result.Text}\"");

                    OnSpeechTranscribed?.Invoke(CreateSpeechRecording(e.Result));
                };
            }
        }

        public async Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language language)
        {
            AudioConfig audioConfig = AudioConfig.FromStreamInput(new DiscreteAudioInputStream(audioBuffer), AudioStreamFormat.GetWaveFormatPCM((uint)audioBuffer.Format.SampleRate, (byte)audioBuffer.Format.BitsPerSample, (byte)audioBuffer.Format.ChannelCount));

            var speechRecognizer = CreateSpeechRecognizer(audioConfig, language);
            SpeechRecognitionResult result = await speechRecognizer.RecognizeOnceAsync();

            return CreateSpeechRecording(result, audioBuffer);
        }

        private Microsoft.CognitiveServices.Speech.SpeechRecognizer CreateSpeechRecognizer(AudioConfig audioConfig, params Language[] languages)
        {
            if (defaultSpeechRecognizer != null && string.Join("|", _languages.Select(l => l.GetIetfTag()).OrderBy(l => l)).Contains(string.Join("|", languages.Select(l => l.GetIetfTag()).OrderBy(l => l))))
                return defaultSpeechRecognizer;

            string[] languageTags = languages
                .Select(c => c.GetIetfTag())
                .Distinct()
                .ToArray();

            var languageConfig = AutoDetectSourceLanguageConfig.FromLanguages(languageTags);
            return new Microsoft.CognitiveServices.Speech.SpeechRecognizer(speechConfig, languageConfig, audioConfig);
        }
        private SpeechRecording CreateSpeechRecording(SpeechRecognitionResult speechRecognitionResult, AudioBuffer audioBuffer = null)
        {
            var languageResult = AutoDetectSourceLanguageResult.FromResult(speechRecognitionResult);

            SpeechRecording speechRecording = new SpeechRecording(audioBuffer)
            {
                Transcription = speechRecognitionResult.Text,
                Language = Language.GetByIetfTag(languageResult?.Language)
            };

            return speechRecording;
        }
    }
}