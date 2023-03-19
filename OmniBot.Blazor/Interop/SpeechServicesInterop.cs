using Microsoft.JSInterop;

namespace OmniBot.Blazor.Interop
{
    public class SpeechServicesInterop : BaseInterop
    {
        public record SynthesizeResult(string data, double duration);

        private readonly string _speechKey;
        private readonly string _serviceRegion;

        public SpeechServicesInterop(IJSRuntime jsRuntime, string speechKey, string serviceRegion) : base(jsRuntime)
        {
            _speechKey = speechKey;
            _serviceRegion = serviceRegion;
        }

        public override async Task InitializeAsync()
        {
            _jsInterop = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/OmniBot.Blazor/SpeechServicesInterop.js");
            await _jsInterop.InvokeVoidAsync("_initialize", _speechKey, _serviceRegion);
        }

        public async Task<SynthesizeResult> SynthesizeAsync(string text, string language)
        {
            ThrowIfNotInitialized();
            return await _jsInterop.InvokeAsync<SynthesizeResult>("synthesizeAsync", text, language);
        }

        public override async ValueTask DisposeAsync()
        {
            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("_dispose");
        }
    }
}
