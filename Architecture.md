# Architecture

```mermaid
flowchart TD
	UnityApp --> OneAPI_1
	OneAPI_1 --chat--> FastGPT
	OneAPI_1 --tts&whisper--> OpenAI
	OneAPI_1 --any--> other1(other)
	FastGPT --> OneAPI_2
	OneAPI_2 --chat--> GPT4o
	OneAPI_2 --embedding--> text-embedding-3-small
	OneAPI_2 --any--> other2(other)
```

