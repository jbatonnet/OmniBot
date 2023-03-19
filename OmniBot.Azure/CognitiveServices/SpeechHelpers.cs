using NAudio.CoreAudioApi;

using OmniBot.Common;

namespace OmniBot.Azure.CognitiveServices
{
    public static class SpeechHelpers
    {
        public static void ListDevices()
        {
            var enumerator = new MMDeviceEnumerator();

            Console.WriteLine("Input");
            foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                Console.WriteLine("  {0}: {1}", endpoint.FriendlyName, endpoint.ID);

            Console.WriteLine();
            Console.WriteLine("Output");
            foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                Console.WriteLine("  {0}: {1}", endpoint.FriendlyName, endpoint.ID);

            Console.ReadLine();
        }

        public static Microsoft.CognitiveServices.Speech.SynthesisVoiceGender ToSynthesisVoiceGender(this PersonGender gender)
        {
            switch (gender)
            {
                case PersonGender.Male: return Microsoft.CognitiveServices.Speech.SynthesisVoiceGender.Male;
                case PersonGender.Female: return Microsoft.CognitiveServices.Speech.SynthesisVoiceGender.Female;
                default: return Microsoft.CognitiveServices.Speech.SynthesisVoiceGender.Unknown;
            }
        }
    }
}