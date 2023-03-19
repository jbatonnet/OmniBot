using Microsoft.Extensions.Logging;

using OmniBot.Common.Speech;

namespace OmniBot.Common.Audio;

public class VolumeSpeechDetector : ISpeechDetector
{
    public class SilenceDetectionParameters
    {
        public bool Enabled { get; set; } = true;
        //public TimeSpan SpeechDetectionThreshold { get; set; } = TimeSpan.Zero;
        public TimeSpan SilenceDetectionThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

        public TimeSpan DBSamplingWindow { get; set; } = TimeSpan.FromMilliseconds(1);
        //public TimeSpan DetectionBucketDuration { get; set; } = TimeSpan.FromMilliseconds(1000);
        public int DetectionBucketCount { get; set; } = 3;
    }

    public SilenceDetectionParameters SilenceDetection { get; set; } = new SilenceDetectionParameters();

    public static readonly int CHANNEL_COUNT = 1;
    //public static readonly int BITS_PER_SAMPLE = 16;

    //private static readonly int DB_EXTREMA_SECONDS = 3;
    //private static readonly int SILENCE_MILLISECONDS_THRESHOLD = 500;

    public event SpeechStartedEventHandler OnSpeechStarted;
    public event SpeechStoppedEventHandler OnSpeechStopped;

    public bool Recording { get; set; }

    public bool Detecting => throw new NotImplementedException();

    private readonly ILogger<VolumeSpeechDetector> _logger;
    private readonly IAudioSource _audioSource;

    private int totalSampleCount;
    private int dbWindowSamples = 10;

    private bool isSilence, isRecording;
    private int silenceStart;

    private Queue<byte> lastBytes;
    private Queue<double> lastDBs;

    private List<double> minDBs, maxDBs;
    private double dBSum;

    public VolumeSpeechDetector(IAudioSource audioSource) : this(null, audioSource) { }
    public VolumeSpeechDetector(ILogger<VolumeSpeechDetector> logger, IAudioSource audioSource)
    {
        _logger = logger;
        _audioSource = audioSource;

        // TODO: Improve audio format compatibility
        // TODO: Use a LinearAudioConverter
        if (_audioSource.Format.ChannelCount != CHANNEL_COUNT || (_audioSource.Format.BitsPerSample != 8 && _audioSource.Format.BitsPerSample != 16))
            throw new NotSupportedException("The provided audio source format is not supported");
    }

    private void StartRecording()
    {
        // Processing setup
        totalSampleCount = 0;

        isSilence = true;
        silenceStart = 0;

        isRecording = false;

        dbWindowSamples = (int)SilenceDetection.DBSamplingWindow.TotalMilliseconds * _audioSource.Format.SampleRate / 1000;

        lastBytes = new Queue<byte>(dbWindowSamples * 4);
        lastDBs = new Queue<double>(dbWindowSamples);

        minDBs = new List<double>(SilenceDetection.DetectionBucketCount);
        maxDBs = new List<double>(SilenceDetection.DetectionBucketCount);

        for (int i = 0; i < SilenceDetection.DetectionBucketCount; i++)
        {
            minDBs.Add(0);
            maxDBs.Add(-90);
        }

        for (int i = 0; i < dbWindowSamples; i++)
        {
            lastBytes.Enqueue(0);
            if (_audioSource.Format.BitsPerSample >= 16)
                lastBytes.Enqueue(0);

            lastDBs.Enqueue(-90);
        }

        dBSum = lastDBs.Sum();

        // Listen for audio data
        _audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;

        Recording = true;

        _logger?.LogTrace("Starting recording");
    }
    private void StopRecording()
    {
        // Stop listening for audio data
        _audioSource.OnAudioBufferReceived -= AudioSource_OnAudioBufferReceived;

        Recording = false;

        _logger?.LogTrace("Stopping recording");

        //OnAudioRecorded?.Invoke(FinalizeAudioRecording());
    }

    private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        if (!SilenceDetection.Enabled)
        {
            totalSampleCount += audioBuffer.Data.Length;
            return;
        }

        int bytesPerSample = _audioSource.Format.BitsPerSample / 8;
        byte[] bytes = new byte[4];

        for (int i = 0; i < audioBuffer.Data.Length; i += bytesPerSample)
        {
            double dB = 0;

            if (_audioSource.Format.BitsPerSample == 8)
            {
                bytes[0] = audioBuffer.Data[i + 0];

                float sample = bytes[0] / 128f;

                dB = bytes[0] == 0 ? -100 : 20 * Math.Log10(Math.Abs(sample));

                lastBytes.Dequeue();
                lastBytes.Enqueue(bytes[0]);
            }
            else if (_audioSource.Format.BitsPerSample == 16)
            {
                bytes[0] = audioBuffer.Data[i + 0];
                bytes[1] = audioBuffer.Data[i + 1];

                short sampleBytes = (short)((bytes[1] << 8) | bytes[0]);
                float sample = sampleBytes / 32768f;

                dB = sampleBytes == 0 ? -100 : 20 * Math.Log10(Math.Abs(sample));

                lastBytes.Dequeue();
                lastBytes.Dequeue();
                lastBytes.Enqueue(bytes[0]);
                lastBytes.Enqueue(bytes[1]);
            }

            dBSum -= lastDBs.Dequeue();
            dBSum += dB;

            lastDBs.Enqueue(dB);

            double averageDB = dBSum / dbWindowSamples;

            // Aggregation over 1 second
            if (totalSampleCount % _audioSource.Format.SampleRate == 0)
            {
                if (minDBs.Count == minDBs.Capacity)
                    minDBs.RemoveAt(minDBs.Count - 1);
                if (maxDBs.Count == maxDBs.Capacity)
                    maxDBs.RemoveAt(maxDBs.Count - 1);

                minDBs.Insert(0, averageDB);
                maxDBs.Insert(0, averageDB);
            }
            else
            {
                if (dB < minDBs[0])
                    minDBs[0] = averageDB;

                if (dB > maxDBs[0])
                    maxDBs[0] = averageDB;
            }

            // Thresholds to detect silence transition
            double silenceUpperThreshold = minDBs.Average() + 15;
            double silenceLowerThreshold = minDBs.Average() + 10;

            if (isSilence)
            {
                if (averageDB > silenceUpperThreshold)
                {
                    isSilence = false;

                    // Silence stopped, start recording
                    if (!isRecording)
                    {
                        _logger?.LogDebug("Silence stopped, starting to record audio");
                        OnSpeechStarted?.Invoke(TimeSpan.Zero);

                        isRecording = true;
                    }
                }

                if (isSilence && isRecording)
                {
                    int silenceMilliseconds = (totalSampleCount - silenceStart) * 1000 / _audioSource.Format.SampleRate;
                    if (silenceMilliseconds > SilenceDetection.SilenceDetectionThreshold.TotalMilliseconds)
                    {
                        isRecording = false;

                        _logger?.LogDebug("Silence started, stopping recording");
                        OnSpeechStopped?.Invoke(TimeSpan.Zero, TimeSpan.Zero);
                    }
                }
            }
            else
            {
                if (averageDB < silenceLowerThreshold)
                {
                    isSilence = true;
                    silenceStart = totalSampleCount;
                }
            }

            totalSampleCount++;
        }
    }
}
