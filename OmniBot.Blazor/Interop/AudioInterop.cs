using Microsoft.JSInterop;

using OmniBot.Common.Audio;

namespace OmniBot.Blazor.Interop
{
    public class AudioInterop : BaseInterop
    {
        public AudioInterop(IJSRuntime jsRuntime) : base(jsRuntime)
        {
        }

        public override async Task InitializeAsync()
        {
            _jsInterop = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/OmniBot.Blazor/AudioInterop.js");
            await _jsInterop.InvokeVoidAsync("_initialize");
        }

        public async Task AddOnAudioDataReceived(Func<string, Task> callback)
        {
            var callbackInfo = new JSCallbackInfo<string>(callback);
            var callbackRef = DotNetObjectReference.Create<JSCallbackInfo>(callbackInfo);

            _callbackIndex.TryAdd(callback, callbackInfo.Id);
            _callbackReferences.TryAdd(callbackInfo.Id, callbackRef);

            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("add_OnAudioDataReceived", callbackInfo.Id, callbackRef);
        }
        public async Task RemoveOnAudioDataReceived(Func<string, Task> callback)
        {
            if (!_callbackIndex.TryRemove(callback, out var callbackId))
                return;
            if (!_callbackReferences.TryRemove(callbackId, out var callbackRef))
                return;

            callbackRef.Dispose();

            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("remove_OnAudioDataReceived", callbackId);
        }

        public async Task<bool> GetListening()
        {
            ThrowIfNotInitialized();
            return await _jsInterop.InvokeAsync<bool>("get_Listening");
        }
        public async Task<AudioFormat> GetFormat()
        {
            ThrowIfNotInitialized();
            return await _jsInterop.InvokeAsync<AudioFormat>("get_Format");
        }

        public async Task StartListening()
        {
            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("startListening");
        }
        public async Task StopListening()
        {
            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("stopListening");
        }

        public async Task PlayAsync(string base64)
        {
            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("playAsync", base64);
        }

        public override async ValueTask DisposeAsync()
        {
            ThrowIfNotInitialized();
            await _jsInterop.InvokeVoidAsync("_dispose");
        }
    }
}
