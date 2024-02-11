using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Speech;

namespace OmniBot.Azure.CognitiveServices
{
    public delegate void SpeechTranscribingEventHandler(string transcription);

    public class ContinousSpeechRecognizer : IContinuousSpeechTranscriber
    {
        private class InputStreamCallback : PullAudioInputStreamCallback
        {
            private readonly ContinousSpeechRecognizer _parent;

            private long silenceTimeout = 0;

            public InputStreamCallback(ContinousSpeechRecognizer continousSpeechRecognizer)
            {
                _parent = continousSpeechRecognizer;
            }

            public override int Read(byte[] dataBuffer, uint size)
            {
                if (_parent.currentAudioBuffer == null)
                {
                    long currentBufferTimeout = Environment.TickCount + size / 32;

                    while (_parent.audioBufferQueue.Count == 0)
                    {
                        if (Environment.TickCount > silenceTimeout)
                        {
                            _parent._logger?.LogTrace("Read(): No buffer available, blocking read loop");

                            while (_parent.audioBufferQueue.Count == 0)
                                Thread.Sleep(100);

                            continue;
                        }
                        if (Environment.TickCount > currentBufferTimeout)
                        {
                            _parent._logger?.LogTrace("Read(): No buffer available, generating silence");

                            Array.Clear(dataBuffer, 0, (int)size);
                            return (int)size;
                        }

                        Thread.Sleep(10);
                    }

                    _parent.currentAudioBuffer = _parent.audioBufferQueue.Dequeue();
                    _parent.currentAudioBufferPosition = 0;
                }

                silenceTimeout = Environment.TickCount + 5000;

                int read = Math.Min(_parent.currentAudioBuffer.Data.Length - _parent.currentAudioBufferPosition, (int)size);

                Buffer.BlockCopy(_parent.currentAudioBuffer.Data, _parent.currentAudioBufferPosition, dataBuffer, 0, read);
                _parent.currentAudioBufferPosition += read;

                if (_parent.currentAudioBufferPosition == _parent.currentAudioBuffer.Data.Length)
                    _parent.currentAudioBuffer = null;

                _parent._logger?.LogTrace($"InputStreamCallback.Read(): Read {read} bytes");

                return read;
            }
        }

        public event SpeechTranscribingEventHandler OnSpeechTranscribing;
        public event SpeechTranscribedEventHandler OnSpeechTranscribed;

        public bool Transcribing { get; set; } = true;

        private readonly ILogger<ContinousSpeechRecognizer> _logger;

        private SpeechConfig speechConfig;
        private Microsoft.CognitiveServices.Speech.SpeechRecognizer speechRecognizer;

        private Queue<AudioBuffer> audioBufferQueue = new();
        private AudioBuffer currentAudioBuffer = null;
        private int currentAudioBufferPosition = 0;

        public ContinousSpeechRecognizer(ILogger<ContinousSpeechRecognizer> logger, string speechKey, string serviceRegion, IAudioSource audioSource, params Language[] languages)
        {
            _logger = logger;

            // Listen to audio source
            audioSource.OnAudioBufferReceived += b => audioBufferQueue.Enqueue(b);

            // Setup speech recognizer
            speechConfig = SpeechConfig.FromSubscription(speechKey, serviceRegion);
            speechConfig.SetProfanity(ProfanityOption.Raw);
            speechConfig.OutputFormat = OutputFormat.Detailed;

            string[] languageTags = (languages ?? new[] { Language.English })
                .Select(c => c.GetIetfTag())
                .Distinct()
                .ToArray();

            var languageConfig = AutoDetectSourceLanguageConfig.FromLanguages(languageTags);
            var audioConfig = AudioConfig.FromStreamInput(new InputStreamCallback(this), AudioStreamFormat.GetWaveFormatPCM((uint)audioSource.Format.SampleRate, (byte)audioSource.Format.BitsPerSample, (byte)audioSource.Format.ChannelCount));

            speechRecognizer = new Microsoft.CognitiveServices.Speech.SpeechRecognizer(speechConfig, languageConfig, audioConfig);

            speechRecognizer.Canceled += (s, e) => _logger?.LogDebug($"Cancelled: {e.ErrorDetails}");
            speechRecognizer.SessionStarted += (s, e) => _logger?.LogDebug($"Session started (id: {e.SessionId})");
            speechRecognizer.SessionStopped += (s, e) => _logger?.LogDebug($"Session stopped (id: {e.SessionId})");
            speechRecognizer.SpeechEndDetected += (s, e) => _logger?.LogDebug($"Speech end detected (offset: {e.Offset} ticks)");

            speechRecognizer.Recognizing += (s, e) =>
            {
                if (!Transcribing)
                    return;

                string transcription = e.Result.Text.Trim();
                _logger?.LogDebug($"Recognizing speech \"{transcription}\"");

                OnSpeechTranscribing?.Invoke(transcription);
            };
            speechRecognizer.Recognized += (s, e) =>
            {
                if (!Transcribing)
                    return;

                string transcription = e.Result.Text.Trim();
                _logger?.LogDebug($"Recognized speech \"{e.Result.Text}\"");

                if (string.IsNullOrEmpty(transcription))
                    return;
                if (transcription.Length < 3)
                    return;
                if (transcription.ToLower().Contains("sous-titr"))
                    return;

                OnSpeechTranscribed?.Invoke(CreateSpeechRecording(e.Result));
            };

            speechRecognizer.StartContinuousRecognitionAsync();
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
