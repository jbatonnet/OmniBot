using System.Diagnostics;

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;

using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;
using OmniBot.Common.Speech;

namespace OmniBot.ML;

// https://github.com/snakers4/silero-vad/blob/master/utils_vad.py
public class SileroSpeechDetector : ISpeechDetector
{

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

    private bool detecting = true;
    private int bufferIndex = 0;

    private bool wasSpeaking = false;
    private TimeSpan speechStart;
    private TimeSpan speechStopTimeout = TimeSpan.MaxValue;

    public SileroSpeechDetector(IAudioSource audioSource)
    {
        _audioSource = audioSource;

        // Prepare audio conversion
        audioConverter = new LinearAudioConverter(_audioSource.Format, new AudioFormat(16000, 1, 16));

        // Load YAMNet model
        mlContext = new MLContext();

        var pipeline = mlContext.Transforms.ApplyOnnxModel("Models/silero_vad.onnx");

        IDataView fitDataView = mlContext.Data.LoadFromEnumerable(new[] { new { input = new float[0, 0], sr = 16000, h = new float[0, 0, 0], c = new float[0, 0, 0] } });
        model = pipeline.Fit(fitDataView);

        int batchSize = 30 * 16000 / 1000; // 30ms at 16kHz

        var inputSchemaBuilder = new DataViewSchema.Builder();
        inputSchemaBuilder.AddColumn("input", new VectorDataViewType(NumberDataViewType.Single, batchSize));
        inputSchemaBuilder.AddColumn("sr", NumberDataViewType.Int64);
        inputSchemaBuilder.AddColumn("h", new VectorDataViewType(NumberDataViewType.Single, 2, batchSize, 64));
        inputSchemaBuilder.AddColumn("c", new VectorDataViewType(NumberDataViewType.Single, 2, batchSize, 64));

        modelInputSchema = inputSchemaBuilder.ToSchema();

        // Start processing data
        audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
    }

    private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        if (!Detecting)
            return;

        // Convert to 16-bit mono 16kHz
        byte[] buffer = audioConverter.ConvertAudio(audioBuffer.Data);

        // Normalize samples
        float[] samples = new float[buffer.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)(buffer[i * 2 + 1] << 8 | buffer[i * 2]) / short.MaxValue - 1;
        
        // Prepare model inputs
        var inputObject = new
        {
            input = samples,
            sr = 16000,
            h = new float[0],
            c = new float[0]
        };
        IDataView inputDataView = mlContext.Data.LoadFromEnumerable(new[] { inputObject }, modelInputSchema);

        // Run model
        IDataView outputDataView = model.Transform(inputDataView);

        // Get output data
        VBuffer<float> outputBuffer = outputDataView.GetColumn<VBuffer<float>>("tower0/network/layer32/final_output").First();
        float[] outputData = outputBuffer.DenseValues().ToArray();


    }
}
