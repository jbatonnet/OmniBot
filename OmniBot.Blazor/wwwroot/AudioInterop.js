let _disposeCallbacks = [];

function _add_disposeCallback(id, callback) {
    if (callback === null)
        return;

    for (let i = 0; i < _disposeCallbacks.length; i++) {
        if (_disposeCallbacks[i].id === id) {
            return;
        }
    }

    _disposeCallbacks.push({
        id: id,
        callback: callback
    });
}
function _remove_disposeCallback(id) {
    for (let i = 0; i < _disposeCallbacks.length; i++) {
        if (_disposeCallbacks[i].id === id) {
            _disposeCallbacks.splice(i, 1);
            return;
        }
    }
}
function _execute_disposeCallbacks() {
    for (var i = 0; i < _disposeCallbacks.length; i++)
        _disposeCallbacks[i].callback(_disposeCallbacks[i].id);

    _disposeCallbacks = [];
}

// --- //

let listening = false;
let format = { SampleRate: 16000, ChannelCount: 1, BitsPerSample: 16 };

let audioContext = null;
let mediaStream = null;
let processorNode = null;

let bufferSize = 4096;
let numberOfInputChannels = 1;
let numberOfOutputChannels = 1;

let onAudioDataReceivedCallbacks = [];

export function _initialize() {
    window.AudioContext = window.AudioContext || window.webkitAudioContext;

    navigator.mediaDevices.getUserMedia({ audio: true })
        .then(microphoneMediaStream => {

            audioContext = new AudioContext({ sampleRate: format.SampleRate });
            mediaStream = audioContext.createMediaStreamSource(microphoneMediaStream);

            if (audioContext.createScriptProcessor)
                processorNode = audioContext.createScriptProcessor(bufferSize, numberOfInputChannels, numberOfOutputChannels);
            else
                processorNode = audioContext.createJavaScriptNode(bufferSize, numberOfInputChannels, numberOfOutputChannels);

            processorNode.onaudioprocess = e => {
                if (!listening)
                    return;

                const float32Array = e.inputBuffer.getChannelData(0);

                let int16Array = new Int16Array(float32Array.length);
                for (let i = 0; i < float32Array.length; i++)
                    int16Array[i] = float32Array[i] * 32767;

                const byteArray = new Uint8Array(int16Array.buffer);

                const base64 = btoa([].reduce.call(byteArray, (p, c) => p + String.fromCharCode(c), ''));

                for (let i = 0; i < onAudioDataReceivedCallbacks.length; i++)
                    if (onAudioDataReceivedCallbacks[i].callback !== null)
                        onAudioDataReceivedCallbacks[i].callback(base64);
            }

            mediaStream.connect(processorNode);
            processorNode.connect(audioContext.destination);
        })
        .catch(e => {
            console.log(e.message);
        });
}

export function add_OnAudioDataReceived(id, callbackRef) {
    _add_disposeCallback(id, id => remove_OnAudioDataReceived(id));

    onAudioDataReceivedCallbacks.push({
        id: id,
        callback: buffer => callbackRef.invokeMethodAsync("Call", buffer)
    });
}
export function remove_OnAudioDataReceived(id) {
    _remove_disposeCallback(id);

    for (let i = 0; i < onAudioDataReceivedCallbacks.length; i++) {
        if (onAudioDataReceivedCallbacks[i].id === id) {
            onAudioDataReceivedCallbacks.splice(i, 1);
            return;
        }
    }
}

export function get_Listening() {
    return listening;
}
export function get_Format() {
    console.log(format);
    return format;
}

export function startListening() {
    // TODO: Actually start and stop recording
    listening = true;
}
export function stopListening() {
    // TODO: Actually start and stop recording
    listening = false;
}

export function playAsync(base64) {
    const byteArray = Uint8Array.from(atob(base64), c => c.charCodeAt(0));

    const audioContext = new (window.AudioContext || window.webkitAudioContext)();
    let bufferSource = audioContext.createBufferSource();

    audioContext.decodeAudioData(byteArray.buffer, buffer => {
        bufferSource.buffer = buffer;
        bufferSource.connect(audioContext.destination);
        bufferSource.start();
    });
}

export function _dispose() {
    _execute_disposeCallbacks();

    if (mediaStream !== null) {
        processorNode.disconnect(audioContext.destination);
        mediaStream.disconnect(processorNode);
    }
}
