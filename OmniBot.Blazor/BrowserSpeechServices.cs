using Microsoft.JSInterop;

using OmniBot.Blazor.Interop;
using OmniBot.Common;
using OmniBot.Common.Speech;

namespace OmniBot.Blazor
{
    public class BrowserSpeechServices : ISpeechSynthesizer, IAsyncDisposable
    {
        private readonly SpeechServicesInterop _speechServicesInterop;
        private bool initialized = false;

        public BrowserSpeechServices(IJSRuntime jsRuntime, string speechKey, string serviceRegion)
        {
            _speechServicesInterop = new SpeechServicesInterop(jsRuntime, speechKey, serviceRegion);
        }

        public async Task InitializeAsync()
        {
            if (initialized)
                return;

            await _speechServicesInterop.InitializeAsync();

            initialized = true;
        }

        public async Task<SpeechRecording> SynthesizeAsync(string text, Language language = null)
        {
            var result = await _speechServicesInterop.SynthesizeAsync(text, language?.GetIetfTag());

            SpeechRecording speechRecording = new()
            {
                Data = Convert.FromBase64String(result.data),
                Transcription = text,
                Language = language
            };

            return speechRecording;
        }

        public ValueTask DisposeAsync() => _speechServicesInterop.DisposeAsync();
    }
}
