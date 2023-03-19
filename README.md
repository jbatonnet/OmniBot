# OmniBot

OmniBot is a small framework of libraries to ease the development of text and audio based bots in .NET. It includes several audio abstractions for Discord, Twilio or Whisper for speech transcription for example.
It is built to be multiplatform, allowing your bots to interactwith you users using PSTN, WebRTC, local or browser microphones.

It also includes different components working with ML and LLMs, with a focus on being able to run everything locally as much as possible. For example, speech detection is made possible using YAMnet or Silero VAD.

## Structure

- **OmniBot.Common** : Main library abstracting conversation, audio and speech interfaces.
- **OmniBot.Azure** : Abstractions of Azure Speech Services, including speech transcription and synthesis and offline keyword recognition.
- **OmniBot.Blazor** : Abstractions of audio sources and sinks using browser js interop.
- **OmniBot.Discord** : Library to connect and interact with a Discord server, providing abstractions for audio sources and sinks, as well as a text interface.
- **OmniBot.ML** : Different models to be used locally for speech detection and other things.
- **OmniBot.OpenAI** : Abstractions for OpenAI features, such as Chat completion and Whisper speech transcription.
- **OmniBot.SIPSorcery** : Integration of the SIPSorcery library in order to abstract WebRTC and IP interfaces, making your bot PSTN compatible using Twilio SIP Trunking for example.
- **OmniBot.Windows** : Abstraction of local microphone and speaker audio interface.

## Bots

I'm working on multiple bots using this framework, and I will try to open source them as much as possible.
