using System.Collections.Concurrent;

using Microsoft.JSInterop;

namespace OmniBot.Blazor
{
    public class JSCallbackInfo
    {
        public Guid Id { get; init; }
    }
    public class JSCallbackInfo<T> : JSCallbackInfo
    {
        private readonly Func<T, Task> _callback;

        public JSCallbackInfo(Func<T, Task> callback)
        {
            Id = Guid.NewGuid();
            _callback = callback;
        }
        public JSCallbackInfo(Guid id, Func<T, Task> callback)
        {
            Id = id;
            _callback = callback;
        }

        [JSInvokable("Call")]
        public Task Call(T arg)
        {
            return _callback.Invoke(arg);
        }
    }
    public class JSCallbackInfo<T1, T2> : JSCallbackInfo
    {
        private readonly Func<T1, T2, Task> _callback;

        public JSCallbackInfo(Func<T1, T2, Task> callback)
        {
            Id = Guid.NewGuid();
            _callback = callback;
        }
        public JSCallbackInfo(Guid id, Func<T1, T2, Task> callback)
        {
            Id = id;
            _callback = callback;
        }

        [JSInvokable("Call")]
        public Task Call(T1 arg1, T2 arg2)
        {
            return _callback.Invoke(arg1, arg2);
        }
    }

    public abstract class BaseInterop : IAsyncDisposable
    {
        protected readonly IJSRuntime _jsRuntime;

        protected readonly ConcurrentDictionary<object, Guid> _callbackIndex = new();
        protected readonly ConcurrentDictionary<Guid, DotNetObjectReference<JSCallbackInfo>> _callbackReferences = new();

        protected IJSObjectReference _jsInterop = null;

        public BaseInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public abstract Task InitializeAsync();
        public abstract ValueTask DisposeAsync();

        protected void ThrowIfNotInitialized()
        {
            if (_jsInterop == null)
                throw new InvalidOperationException("You need to call InitializeAsync before any other method");
        }
    }
}
