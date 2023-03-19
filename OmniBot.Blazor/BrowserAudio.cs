using Microsoft.JSInterop;

using OmniBot.Blazor.Interop;
using OmniBot.Common.Audio;

namespace OmniBot.Blazor
{
    public class BrowserAudio : IAudioSource, IAsyncDisposable
    {
        public event AudioBufferReceivedEventHandler OnAudioBufferReceived;

        public AudioFormat Format { get; private set; }
        public bool Listening
        {
            get => listening;
            set
            {
                if (listening == value)
                    return;

                listening = value;

                if (listening)
                    _ = _audioInterop.StartListening();
                else
                    _ = _audioInterop.StopListening();
            }
        }

        private readonly AudioInterop _audioInterop;
        private bool listening = false;
        private bool initialized = false;

        public BrowserAudio(IJSRuntime jsRuntime)
        {
            _audioInterop = new AudioInterop(jsRuntime);
        }

        public async Task InitializeAsync()
        {
            if (initialized)
                return;

            await _audioInterop.InitializeAsync();

            await _audioInterop.AddOnAudioDataReceived(JSAudioInteropHandler_OnAudioDataReceived);
            Format = await _audioInterop.GetFormat();

            initialized = true;
        }

        private async Task JSAudioInteropHandler_OnAudioDataReceived(string base64)
        {
            if (OnAudioBufferReceived == null)
                return;

            AudioBuffer audioBuffer = new AudioBuffer()
            {
                Format = Format,
                Data = Convert.FromBase64String(base64),
                Timecode = TimeSpan.Zero
            };

            OnAudioBufferReceived?.Invoke(audioBuffer);
        }

        public async Task PlayAsync(AudioRecording audioRecording, CancellationToken cancellationToken = default)
        {
            string base64 = Convert.ToBase64String(audioRecording.Data);
            await _audioInterop.PlayAsync(base64);

            // TODO: Support interruption
        }

        public ValueTask DisposeAsync() => _audioInterop.DisposeAsync();
    }
}
