# AI Chat for S&box

An AI chatbot integrated directly into the S&box editor. Chat with a local AI assistant while you build your game.

## Features
- Dockable editor panel — dock it anywhere in your layout
- Uses **Ollama** for free, local AI (no API key needed)
- Supports any Ollama model (llama3, mistral, phi3, etc)
- Conversation history maintained during session
- Customisable system prompt

## Setup

### 1. Install Ollama
Download from [ollama.com](https://ollama.com) and install it.

### 2. Pull a model
```
ollama pull llama3
```

### 3. Start Ollama on port 8080
S&box only allows localhost on specific ports. Start Ollama with:

**CMD:**
```
set OLLAMA_HOST=0.0.0.0:8080 && ollama serve
```

**PowerShell:**
```
$env:OLLAMA_HOST="0.0.0.0:8080"; ollama serve
```

### 4. Open AI Chat in S&box
Find it in **View > AI Chat** or look for the AI Chat panel in your dock options.

## Usage
- Type a message in the input field
- Press **Enter** or click **Send**
- The AI responds using your local Ollama model
- Change the model name in the field at the top

## Notes
- Ollama must be running on port 8080 before using the chat
- All processing is local — nothing is sent to external servers
- Larger models give better responses but are slower

## Credits
Made by ICU
