using System.Diagnostics;

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;

using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;
using OmniBot.Common.Speech;

namespace OmniBot.ML;

// https://www.tensorflow.org/hub/tutorials/yamnet
// https://raw.githubusercontent.com/tensorflow/models/master/research/audioset/yamnet/yamnet_class_map.csv
public class YAMNetSpeechDetector : ISpeechDetector
{
    private static readonly int[] SPEECH_CLASSES = new[] { 0, 1, 2, 3, 4, 5, 9, 10, 11, 12, 24, 25, 26, 27, 29, 30, 31 };

    public event SpeechStartedEventHandler OnSpeechStarted;
    public event SpeechStoppedEventHandler OnSpeechStopped;

    public bool Detecting
    {
        get => detecting;
        set
        {
            detecting = value;

            if (!detecting)
            {
                Array.Clear(mainBuffer);
                Array.Clear(nextBuffer);

                bufferIndex = 0;
            }
        }
    }
    public float DetectionThreshold { get; set; } = 0.1f;
    public TimeSpan PreSpeechDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan PostSpeechDelay { get; set; } = TimeSpan.FromSeconds(0.5);
    public bool SpeechDetected => wasSpeaking;

    private readonly IAudioSource _audioSource;
    private readonly LinearAudioConverter audioConverter;

    private MLContext mlContext;
    private OnnxTransformer model;
    private DataViewSchema modelInputSchema;

    private const int MODEL_SAMPLE_COUNT = 15600;
    private const int HALF_BUFFER_SIZE = MODEL_SAMPLE_COUNT;

    private byte[] mainBuffer = new byte[HALF_BUFFER_SIZE * 2],
                   nextBuffer = new byte[HALF_BUFFER_SIZE];
    private float[] inputBuffer = new float[MODEL_SAMPLE_COUNT];

    private bool detecting = true;
    private int bufferIndex = 0;

    private bool wasSpeaking = false;
    private TimeSpan speechStart;
    private TimeSpan speechStopTimeout = TimeSpan.MaxValue;

    public YAMNetSpeechDetector(IAudioSource audioSource)
    {
        _audioSource = audioSource;

        // Prepare audio conversion
        audioConverter = new LinearAudioConverter(_audioSource.Format, new AudioFormat(16000, 1, 16));

        Array.Clear(mainBuffer);
        Array.Clear(nextBuffer);

        // Load YAMNet model
        mlContext = new MLContext();

        var pipeline = mlContext.Transforms.ApplyOnnxModel("Models/yamnet_v1.onnx");

        IDataView fitDataView = mlContext.Data.LoadFromEnumerable(new[] { new { waveform_binary = new float[0] } });
        model = pipeline.Fit(fitDataView);

        var inputSchemaBuilder = new DataViewSchema.Builder();
        inputSchemaBuilder.AddColumn("waveform_binary", new VectorDataViewType(NumberDataViewType.Single, MODEL_SAMPLE_COUNT));

        modelInputSchema = inputSchemaBuilder.ToSchema();

        // Start processing data
        audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
    }

    private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        if (!Detecting)
            return;

        byte[] convertedBuffer = audioConverter.ConvertAudio(audioBuffer.Data);

        int index = 0;

        while (true)
        {
            int count = Math.Min(convertedBuffer.Length - index, nextBuffer.Length - bufferIndex);
            Array.Copy(convertedBuffer, index, nextBuffer, bufferIndex, count);

            index += count;
            bufferIndex += count;

            if (bufferIndex == nextBuffer.Length)
            {
                // If next buffer is full, roll it into the main buffer and run detection
                // TODO: Swap buffers instead of copying data
                Array.Copy(mainBuffer, MODEL_SAMPLE_COUNT, mainBuffer, 0, MODEL_SAMPLE_COUNT);
                Array.Copy(nextBuffer, 0, mainBuffer, MODEL_SAMPLE_COUNT, MODEL_SAMPLE_COUNT);

                bool speaking = DetectSpeech();
                TimeSpan timecode = audioBuffer.Timecode + audioBuffer.GetDuration();

                if (speaking)
                {
                    if (!wasSpeaking)
                    {
                        speechStart = audioBuffer.Timecode - PreSpeechDelay;
                        OnSpeechStarted?.Invoke(speechStart);
                    }

                    speechStopTimeout = timecode + PostSpeechDelay;
                    wasSpeaking = true;
                }
                else if (wasSpeaking && timecode >= speechStopTimeout)
                {
                    OnSpeechStopped?.Invoke(speechStart, speechStopTimeout);
                    speechStopTimeout = TimeSpan.MaxValue;

                    wasSpeaking = false;
                }

                Array.Clear(nextBuffer);
                bufferIndex = 0;
            }

            if (index == convertedBuffer.Length)
                break;
        }
    }
    private bool DetectSpeech()
    {
        // Prepare samples
        // TODO: Half of this buffer should be already processed
        for (int i = 0; i < MODEL_SAMPLE_COUNT; i++)
            inputBuffer[i] = (float)(mainBuffer[i * 2 + 1] << 8 | mainBuffer[i * 2]) / short.MaxValue - 1;

        float maxSample = Math.Max(inputBuffer.Max(), Math.Abs(inputBuffer.Min()));
        for (int i = 0; i < MODEL_SAMPLE_COUNT; i++)
            inputBuffer[i] /= maxSample;

        var inputObject = new { waveform_binary = inputBuffer };
        IDataView inputDataView = mlContext.Data.LoadFromEnumerable(new[] { inputObject }, modelInputSchema);

        // Run model
        IDataView outputDataView = model.Transform(inputDataView);

        // Get output data
        VBuffer<float> outputBuffer = outputDataView.GetColumn<VBuffer<float>>("tower0/network/layer32/final_output").First();
        float[] outputData = outputBuffer.DenseValues().ToArray();

        // Map data to help debugging
        if (Debugger.IsAttached)
        {
            Dictionary<int, string> classMapping = new Dictionary<int, string>()
            {
                { 0, "Speech" },
                { 1, "Child speech" },
                { 2, "Conversation" },
                { 3, "Narration" },
                { 4, "Babbling" },
                { 5, "Speech synthesizer" },
                { 9, "Yell" },
                { 10, "Children shouting" },
                { 11, "Screaming" },
                { 12, "Whispering" },
                { 24, "Singing" },
                { 25, "Choir" },
                { 26, "Yodeling" },
                { 27, "Chant" },
                { 29, "Child singing" },
                { 30, "Synthetic singing" },
                { 31, "Rapping" }
            };

            var classResults = SPEECH_CLASSES
                //.Where(i => outputData[i] > DetectionThreshold)
                .ToDictionary(i => classMapping[i], i => outputData[i]);

            if (classResults.Count > 0)
            {
                //Console.WriteLine(string.Join(", ", classResults.Select(p => $"{p.Key}: {p.Value}")));
                //Console.WriteLine(string.Join(", ", classResults.Select(p => $"{p.Value}")));
            }
        }

        // Match against speech detection classes
        bool speechDetected = SPEECH_CLASSES.Any(i => outputData[i] > DetectionThreshold);
        return speechDetected;
    }
}
