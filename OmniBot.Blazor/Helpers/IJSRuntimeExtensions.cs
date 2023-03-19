using Microsoft.JSInterop;

namespace OmniBot.Blazor.Helpers
{
    public static class IJSRuntimeExtensions
    {
        public static ValueTask Evaluate(this IJSRuntime jsRuntime, string js)
        {
            return jsRuntime.InvokeVoidAsync("eval", js);
        }
        public static ValueTask<T> Evaluate<T>(this IJSRuntime jsRuntime, string js)
        {
            return jsRuntime.InvokeAsync<T>("eval", js);
        }

        public static ValueTask InvalidateServiceWorker(this IJSRuntime jsRuntime)
        {
            string js = @"
                navigator.serviceWorker.getRegistration().then(function(reg) {
                    if (reg) {
                        reg.unregister().then(function() { window.location.reload(true); });
                    } else {
                        window.location.reload(true);
                    }
                });";

            return Evaluate(jsRuntime, js);
        }
        public static ValueTask Share(this IJSRuntime jsRuntime, string title, string text, Uri address)
        {
            string js = $"navigator.share({{ title: '{title}', text: '{text}', url: '{address}' }})";

            return Evaluate(jsRuntime, js);
        }
    }
}
