using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using OmniBot.Common;
using OmniBot.Common.Audio;
using OmniBot.Common.Audio.Converters;
using OmniBot.Common.Speech;

namespace OmniBot.ML;

// https://github.com/snakers4/silero-models/blob/master/src/silero/silero.py
public class SileroSpeechTranscriber : ISpeechTranscriber
{
    public SileroSpeechTranscriber()
    {
    }

    public Task<SpeechRecording> TranscribeAsync(AudioBuffer audioBuffer, Language languageHint = null)
    {
        audioBuffer = audioBuffer.ConvertTo(AudioFormat.Default);

        // Normalize samples
        float[] samples = new float[audioBuffer.Data.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(audioBuffer.Data, i * 2) / (float)short.MaxValue;

        // Run model
        var inputValue = FixedBufferOnnxValue.CreateFromTensor(new DenseTensor<float>(samples.AsMemory(), new[] { 1, samples.Length }));

        var inputNames = new[] { "input" };
        var inputValues = new[] { inputValue };

        var outputNames = new[] { "output" };

        var inferenceSession = new InferenceSession("Models/silero_stt_en_v5.onnx");
        var result = inferenceSession.Run(inputNames, inputValues, outputNames);

        return Task.FromResult(new SpeechRecording());
    }
}
