let voiceSpeed = 1.15;
let style = "cheerful";

let recognizer = null;
let synthesizer = null;

function uint6ToB64(nUint6) {
    return nUint6 < 26
        ? nUint6 + 65
        : nUint6 < 52
            ? nUint6 + 71
            : nUint6 < 62
                ? nUint6 - 4
                : nUint6 === 62
                    ? 43
                    : nUint6 === 63
                        ? 47
                        : 65;
}
function base64EncArr(aBytes) {
    let nMod3 = 2;
    let sB64Enc = "";

    const nLen = aBytes.length;
    let nUint24 = 0;
    for (let nIdx = 0; nIdx < nLen; nIdx++) {
        nMod3 = nIdx % 3;
        // To break your base64 into several 80-character lines, add:
        //   if (nIdx > 0 && ((nIdx * 4) / 3) % 76 === 0) {
        //      sB64Enc += "\r\n";
        //    }

        nUint24 |= aBytes[nIdx] << ((16 >>> nMod3) & 24);
        if (nMod3 === 2 || aBytes.length - nIdx === 1) {
            sB64Enc += String.fromCodePoint(
                uint6ToB64((nUint24 >>> 18) & 63),
                uint6ToB64((nUint24 >>> 12) & 63),
                uint6ToB64((nUint24 >>> 6) & 63),
                uint6ToB64(nUint24 & 63)
            );
            nUint24 = 0;
        }
    }
    return (
        sB64Enc.substring(0, sB64Enc.length - 2 + nMod3) +
        (nMod3 === 2 ? "" : nMod3 === 1 ? "=" : "==")
    );
}

export function _initialize(speechKey, serviceRegion) {
    if (typeof SpeechSDK === 'undefined') {
        console.error("Please include https://aka.ms/csspeech/jsbrowserpackageraw");
        return;
        //<script src="https://aka.ms/csspeech/jsbrowserpackageraw"></script>
    }

    const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(speechKey, serviceRegion);
    //speechConfig.SetProfanity(ProfanityOption.Raw);
    speechConfig.requestWordLevelTimestamps();

    //const audioConfig = speechsdk.AudioConfig.fromDefaultMicrophoneInput();
    //const audioConfig = speechsdk.AudioConfig.fromWavFileInput(audioFile);

    /*let pushStream = sdk.AudioInputStream.createPushStream();
    fs.createReadStream("YourAudioFile.wav").on('data', function(arrayBuffer) {
        pushStream.write(arrayBuffer.slice());
    }).on('end', function() {
        pushStream.close();
    });
 
    let audioConfig = sdk.AudioConfig.fromStreamInput(pushStream);*/

    recognizer = new SpeechSDK.SpeechRecognizer(speechConfig, null);
    synthesizer = new SpeechSDK.SpeechSynthesizer(speechConfig, null);

    // Voice list: https:// ??
    // en-US-JennyNeural, fr-FR-YvetteNeural, en-US-JennyMultilingualNeural, fr-CA-SylvieNeural, fr-FR-DeniseNeural

    //defaultVoice = "en-US-JennyMultilingualNeural";
    //languageVoices[Language.French.GetIetfTag()] = "fr-FR-YvetteNeural";
    //languageVoices[Language.English.GetIetfTag()] = "en-US-JennyNeural";
}

export async function synthesizeAsync(text, language) {
    language = language || "en-US";
    let voice = "fr-FR-YvetteNeural";

    const ssml =
        `<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xmlns:mstts="http://www.w3.org/2001/mstts" xml:lang="${language}">
            <voice name="${voice}">
                <lang xml:lang="${language}">
                    <mstts:silence type="Sentenceboundary" value="30ms" />
                    <mstts:silence type="comma-exact" value="20ms" />
                    <mstts:silence type="semicolon-exact" value="100ms" />
                    <mstts:silence type="enumerationcomma-exact" value="150ms" />

                    <prosody rate="${(voiceSpeed > 0 ? "+" : "-")}${Math.floor(voiceSpeed * 100 - 100)}%">
                        <mstts:express-as style="${style}">
                            ${text.trim()}
                        </mstts:express-as>
                    </prosody >
                </lang >
            </voice>
        </speak>`;

    return new Promise(resolve => {

        synthesizer.speakSsmlAsync(
            ssml,
            function (result) {
                const byteArray = new Uint8Array(result.audioData);
                const base64 = btoa([].reduce.call(byteArray, function (p, c) { return p + String.fromCharCode(c) }, ''));

                resolve({
                    data: base64,
                    duration: result.audioDuration / 10000
                });
            });

    });
}

export function _dispose() {
    _execute_disposeCallbacks();
}
