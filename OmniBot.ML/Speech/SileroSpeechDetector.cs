using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;
using OmniBot.Common.Speech;

namespace OmniBot.ML;

// https://github.com/snakers4/silero-vad/blob/master/utils_vad.py
public class SileroSpeechDetector : ISpeechDetector
{
    private const int ModelBatchSize = 512;

    public event SpeechStartedEventHandler OnSpeechStarted;
    public event SpeechStoppedEventHandler OnSpeechStopped;

    public bool Detecting
    {
        get => detecting;
        set
        {
            detecting = value;

            if (detecting)
            {
                Array.Clear(input);
                Array.Clear(output);
                Array.Clear(h0);
                Array.Clear(h1);
                Array.Clear(c0);
                Array.Clear(c1);
            }
        }
    }
    public float DetectionThreshold { get; set; } = 0.5f;
    public TimeSpan PreSpeechDelay { get; set; } = TimeSpan.FromSeconds(0.25);
    public TimeSpan PostSpeechDelay { get; set; } = TimeSpan.FromSeconds(0.5);

    private readonly IAudioSource _audioSource;
    private readonly LinearAudioConverter audioConverter;

    private bool detecting = true;
    private bool wasSpeaking = false;
    private TimeSpan speechStart;
    private TimeSpan speechStopTimeout = TimeSpan.MaxValue;

    private float[] input = new float[ModelBatchSize];
    private float[] output = new float[1];
    private float[] h0 = new float[2 * 1 * 64];
    private float[] h1 = new float[2 * 1 * 64];
    private float[] c0 = new float[2 * 1 * 64];
    private float[] c1 = new float[2 * 1 * 64];

    private InferenceSession inferenceSession;
    private FixedBufferOnnxValue inputValue;
    private FixedBufferOnnxValue outputValue;
    private FixedBufferOnnxValue srValue;
    private FixedBufferOnnxValue h0Value, h1Value;
    private FixedBufferOnnxValue c0Value, c1Value;
    private string[] inputNames = new[] { "input", "sr", "h", "c" };
    private string[] outputNames = new[] { "output", "hn", "cn" };
    private FixedBufferOnnxValue[] input0Values, input1Values;
    private FixedBufferOnnxValue[] output0Values, output1Values;
    private bool hcSwapState = false;

    public SileroSpeechDetector(IAudioSource audioSource, string modelPath = "Models/silero_vad_v4.onnx")
    {
        _audioSource = audioSource;

        // Prepare audio conversion
        audioConverter = new LinearAudioConverter(_audioSource.Format, new AudioFormat(16000, 1, 16));

        // Prepare YAMNet model
        var sessionOptions = new SessionOptions()
        {
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
        };

        inferenceSession = new InferenceSession(modelPath, sessionOptions);

        inputValue = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(input.AsMemory(), new[] { 1, ModelBatchSize }));
        outputValue = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(output.AsMemory(), new[] { 1, 1 }));
        srValue = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<long>(new[] { 16000L }.AsMemory(), new int[0]));
        h0Value = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(h0.AsMemory(), new[] { 2, 1, 64 }));
        h1Value = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(h1.AsMemory(), new[] { 2, 1, 64 }));
        c0Value = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(c0.AsMemory(), new[] { 2, 1, 64 }));
        c1Value = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(c1.AsMemory(), new[] { 2, 1, 64 }));
        input0Values = new[] { inputValue, srValue, h0Value, c0Value };
        input1Values = new[] { inputValue, srValue, h1Value, c1Value };
        output0Values = new[] { outputValue, h0Value, c0Value };
        output1Values = new[] { outputValue, h1Value, c1Value };

        // Start processing data
        audioSource.OnAudioBufferReceived += AudioSource_OnAudioBufferReceived;
    }

    private void AudioSource_OnAudioBufferReceived(AudioBuffer audioBuffer)
    {
        if (!Detecting)
            return;

        // Convert to 16-bit mono 16kHz
        byte[] buffer = audioConverter.ConvertAudio(audioBuffer.Data);

        // Run detection on blocks of 512 samples (32 ms)
        for (int b = 0; b < buffer.Length / (ModelBatchSize * 2); b++)
        {
            for (int i = 0; i < ModelBatchSize; i++)
            {
                int index = (b * ModelBatchSize + i) * 2;

                if (index < buffer.Length)
                    input[i] = BitConverter.ToInt16(buffer, index) / (float)short.MaxValue;
                else
                    input[i] = 0;
            }

            inferenceSession.Run(inputNames, hcSwapState ? input0Values : input1Values, outputNames, hcSwapState ? output0Values : output1Values);
            hcSwapState = !hcSwapState;

            var currentTimecode = audioBuffer.Timecode + TimeSpan.FromMilliseconds(b * ModelBatchSize / 16.0);

            //Console.WriteLine($"Result: {output[0]}");

            if (output[0] > DetectionThreshold)
            {
                if (!wasSpeaking)
                {
                    wasSpeaking = true;

                    speechStart = currentTimecode - PreSpeechDelay;
                    speechStopTimeout = currentTimecode + PostSpeechDelay;

                    OnSpeechStarted?.Invoke(speechStart);
                }
            }
            else
            {
                if (wasSpeaking && currentTimecode >= speechStopTimeout)
                {
                    wasSpeaking = false;
                    OnSpeechStopped?.Invoke(speechStart, currentTimecode);
                }
            }
        }
    }
}
