using Sandbox;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public enum AIProvider
{
	Ollama,
	OpenAI,
	Anthropic,
	Google,
	Custom,
	GitHub
}

/// <summary>
/// S&box AI Assistant â€” built-in knowledge of S&box APIs, patterns, and conventions.
/// </summary>
public sealed class AIChatbot
{
	public AIProvider Provider { get; set; } = AIProvider.Ollama;
	public string ApiKey { get; set; } = "";
	public string Model { get; set; } = "llama3";
	public string CustomUrl { get; set; } = "";
	public int MaxHistory { get; set; } = 20;

	private List<ChatMsg> _history = new();
	public string LastResponse { get; private set; } = "";
	public bool IsThinking { get; private set; } = false;

	public static readonly Dictionary<AIProvider, string> DefaultModels = new()
	{
		{ AIProvider.Ollama, "llama3" },
		{ AIProvider.OpenAI, "gpt-4o-mini" },
		{ AIProvider.Anthropic, "claude-3-haiku-20240307" },
		{ AIProvider.Google, "gemini-1.5-flash" },
		{ AIProvider.Custom, "" },
		{ AIProvider.GitHub, "gpt-4o-mini" },
	};

	/// <summary>
	/// Comprehensive S&box system prompt with baked-in engine knowledge.
	/// </summary>
	public static string SboxSystemPrompt => @"You are an expert S&box game development assistant. You help developers build games in S&box (by Facepunch Studios).

## S&box Overview
- S&box is built on Valve's Source 2 engine with C#/.NET 10
- Games are made with Scenes (JSON), GameObjects, and Components
- Code uses the Sandbox namespace
- Editor tools use the Editor namespace
- Docs: https://sbox.game/dev/doc/
- Source: https://github.com/Facepunch/sbox-public

## Project Structure
- .sbproj â€” project config (JSON)
- Code/*.cs â€” game code (.csproj)
- Editor/*.cs â€” editor tools (.editor.csproj)
- Assets/scenes/*.scene â€” scene files (JSON)
- Assets/models/*.vmdl â€” model definitions
- Assets/materials/*.vmat â€” materials
- Assets/shaders/*.shader or *.shdrgrph â€” shaders

## Core Patterns

### Components (most important)
```csharp
using Sandbox;

[Title( ""My Component"" )]
[Category( ""Gameplay"" )]
[Icon( ""star"" )]
public sealed class MyComponent : Component
{
    [Property] public float Speed { get; set; } = 200f;
    [Property] public GameObject Target { get; set; }

    protected override void OnStart() { }
    protected override void OnUpdate() { }
    protected override void OnFixedUpdate() { }
    protected override void OnEnabled() { }
    protected override void OnDisabled() { }
    protected override void OnDestroy() { }
}
```

### Input
```csharp
if ( Input.Pressed( ""attack1"" ) ) { }
if ( Input.Down( ""forward"" ) ) { }
var mouseDelta = Input.MouseDelta;
var analogMove = Input.AnalogMove; // Vector3
```

### Physics / Traces
```csharp
var tr = Scene.Trace.Ray( pos, pos + dir * 1000f )
    .WithTag( ""solid"" )
    .Run();
if ( tr.Hit ) { var hitPos = tr.HitPosition; }
```

### Networking
```csharp
[Sync] public float Health { get; set; } = 100f;
[Broadcast] public void TakeDamage( float amount ) { Health -= amount; }
if ( IsProxy ) return; // Don't run on non-owner
Connection.Local // local player
```

### Common Components
- ModelRenderer â€” renders a 3D model
- SkinnedModelRenderer â€” animated models
- CameraComponent â€” camera
- Rigidbody â€” physics body
- Collider (BoxCollider, SphereCollider, MeshCollider, ModelCollider)
- CharacterController â€” player movement
- NavMeshAgent â€” AI pathfinding
- AudioSource â€” sound
- ParticleEffect â€” particles
- ScreenPanel â€” UI root

### Model/Animation
- .vmdl files define models (KV3 format)
- base_model_name references parent model
- AnimFile nodes reference .fbx animation files
- SkinnedModelRenderer.Set(""param"", value) for animgraph params
- citizen.vmdl is the default player model (95 bones)

### Materials (.vmat)
```
shader = ""shaders/complex.shader""
TextureColor = ""path/to/color.png""
TextureNormal = ""path/to/normal.png""
TextureRoughness = ""path/to/rough.png""
g_flMetalness = 0.0
```

### UI (Razor)
```razor
@using Sandbox.UI
<root>
    <div class=""hud"">
        <label>Health: @Health</label>
    </div>
</root>
```

### Terrain
- Terrain component with heightmap (.r16 format)
- Terrain materials use .tmat files
- Splatmap for painting layers
- Import heightmap via editor

### HTTP (for APIs)
```csharp
var content = new StringContent( json, Encoding.UTF8, ""application/json"" );
var response = await Http.RequestAsync( url, ""POST"", content );
var result = await response.Content.ReadAsStringAsync();
```
Note: S&box only allows domains (no IPs), localhost on ports 80/443/8080/8443.

### Editor Tools
```csharp
using Editor;

[Dock( ""Editor"", ""My Tool"", ""icon_name"" )]
public class MyTool : Widget
{
    public MyTool( Widget parent ) : base( parent )
    {
        Layout = Layout.Column();
        // Add widgets: Label, Button, LineEdit, ComboBox, etc.
    }
}
```

### Important S&box Rules
- ALL float literals need 'f' suffix: 0f, 10f, 0.5f
- RootNamespace is Sandbox (game code) or Editor (editor code)
- Use [Property] for inspector-visible fields
- Use [Sync] for networked properties
- Use [Broadcast] for networked methods
- Scene.Active gets the current scene
- GameObject.AddComponent<T>() to add components
- Components are sealed classes inheriting Component
- Use Time.Delta for frame-independent movement
- MathF is not available â€” use System.Math or float casts
- FileSystem.Mounted for reading project files
- Log.Info() / Log.Warning() / Log.Error() for console output

### S&box Naming Conventions
- PascalCase for classes, methods, properties
- camelCase for local variables
- Use [Title], [Category], [Icon] attributes on components
- Prefer sealed classes for components
- Use tabs for indentation, Allman brace style

## Your Role
- Write S&box-compatible C# code
- Follow S&box naming conventions and patterns
- Reference official docs when helpful
- Explain S&box-specific concepts clearly
- Help with scenes, components, networking, UI, shaders, models
- Suggest best practices for S&box game development";

	public void Reset()
	{
		_history.Clear();
		_history.Add( new ChatMsg { Role = "system", Content = SboxSystemPrompt } );
		Log.Info( $"AI Chat: System prompt loaded ({SboxSystemPrompt.Length} chars)" );
	}

	public string GetApiUrl()
	{
		return Provider switch
		{
			AIProvider.Ollama => "http://localhost:8080/api/chat",
			AIProvider.OpenAI => "https://api.openai.com/v1/chat/completions",
			AIProvider.Anthropic => "https://api.anthropic.com/v1/messages",
			AIProvider.Google => $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}",
			AIProvider.Custom => CustomUrl,
			AIProvider.GitHub => "https://models.inference.ai.azure.com/chat/completions",
			_ => ""
		};
	}

	public async Task<string> SendMessage( string userMessage )
	{
		if ( IsThinking ) return "Still thinking...";
		if ( string.IsNullOrWhiteSpace( userMessage ) ) return "";

		IsThinking = true;
		_history.Add( new ChatMsg { Role = "user", Content = userMessage } );

		while ( _history.Count > MaxHistory + 1 )
			_history.RemoveAt( 1 );

		try
		{
			LastResponse = Provider switch
			{
				AIProvider.Ollama => await CallOllama(),
				AIProvider.OpenAI => await CallOpenAI(),
				AIProvider.Anthropic => await CallAnthropic(),
				AIProvider.Google => await CallGoogle(),
				AIProvider.Custom => await CallOpenAI(),
				AIProvider.GitHub => await CallGitHub(),
				_ => "Unknown provider"
			};

			_history.Add( new ChatMsg { Role = "assistant", Content = LastResponse } );
			return LastResponse;
		}
		catch ( Exception e )
		{
			LastResponse = $"Error: {e.Message}";
			return LastResponse;
		}
		finally
		{
			IsThinking = false;
		}
	}

	private async Task<string> CallOllama()
	{
		var body = JsonSerializer.Serialize( new Dictionary<string, object>
		{
			{ "model", Model },
			{ "messages", _history },
			{ "stream", false }
		} );
		var result = await DoPost( GetApiUrl(), body );
		var resp = JsonSerializer.Deserialize<OllamaResp>( result, JsonOpts );
		return resp?.Message?.Content ?? "Empty response";
	}

	private async Task<string> CallOpenAI()
	{
		var body = JsonSerializer.Serialize( new Dictionary<string, object>
		{
			{ "model", Model },
			{ "messages", _history },
			{ "max_tokens", 1000 }
		} );
		var url = Provider == AIProvider.Custom ? CustomUrl : GetApiUrl();
		var result = await DoPost( url, body, $"Bearer {ApiKey}" );
		var resp = JsonSerializer.Deserialize<OpenAIResp>( result, JsonOpts );
		return resp?.Choices?[0]?.Message?.Content ?? "Empty response";
	}

	private async Task<string> CallAnthropic()
	{
		var messages = new List<object>();
		foreach ( var m in _history )
		{
			if ( m.Role == "system" ) continue;
			messages.Add( new { role = m.Role, content = m.Content } );
		}
		var body = JsonSerializer.Serialize( new Dictionary<string, object>
		{
			{ "model", Model },
			{ "max_tokens", 1000 },
			{ "system", SboxSystemPrompt },
			{ "messages", messages }
		} );
		var response = await Http.RequestAsync( GetApiUrl(), "POST",
			new StringContent( body, Encoding.UTF8, "application/json" ) );
		var result = await response.Content.ReadAsStringAsync();
		var resp = JsonSerializer.Deserialize<AnthropicResp>( result, JsonOpts );
		return resp?.Content?[0]?.Text ?? "Empty response";
	}

	private async Task<string> CallGoogle()
	{
		var parts = new List<object>();
		foreach ( var m in _history )
		{
			if ( m.Role == "system" ) continue;
			parts.Add( new { role = m.Role == "assistant" ? "model" : "user", parts = new[] { new { text = m.Content } } } );
		}
		var body = JsonSerializer.Serialize( new Dictionary<string, object>
		{
			{ "contents", parts },
			{ "systemInstruction", new { parts = new[] { new { text = SboxSystemPrompt } } } }
		} );
		var result = await DoPost( GetApiUrl(), body );
		var resp = JsonSerializer.Deserialize<GeminiResp>( result, JsonOpts );
		return resp?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? "Empty response";
	}

	
	private async Task<string> CallGitHub()
	{
		var body = JsonSerializer.Serialize( new Dictionary<string, object>
		{
			{ "model", Model },
			{ "messages", _history },
			{ "max_tokens", 1000 }
		} );
		var result = await DoPost( GetApiUrl(), body, $"Bearer {ApiKey}" );
		var resp = JsonSerializer.Deserialize<OpenAIResp>( result, JsonOpts );
		return resp?.Choices?[0]?.Message?.Content ?? "Empty response";
	}
	private async Task<string> DoPost( string url, string body, string auth = null )
	{
		var content = new StringContent( body, Encoding.UTF8, "application/json" );
		var headers = new Dictionary<string, string>();

		if ( !string.IsNullOrEmpty( auth ) )
			headers["Authorization"] = auth;

		var result = await Http.RequestStringAsync( url, "POST", content, headers );

		Log.Info( $"AI API: {result[..System.Math.Min( 300, result.Length )]}" );
		return result;
	}

	private static JsonSerializerOptions JsonOpts => new() { PropertyNameCaseInsensitive = true };

	public class ChatMsg
	{
		[JsonPropertyName( "role" )] public string Role { get; set; }
		[JsonPropertyName( "content" )] public string Content { get; set; }
	}

	private class OllamaResp { [JsonPropertyName( "message" )] public ChatMsg Message { get; set; } }
	private class OpenAIResp { [JsonPropertyName( "choices" )] public List<OpenAIChoice> Choices { get; set; } }
	private class OpenAIChoice { [JsonPropertyName( "message" )] public ChatMsg Message { get; set; } }
	private class AnthropicResp { [JsonPropertyName( "content" )] public List<AnthropicContent> Content { get; set; } }
	private class AnthropicContent { [JsonPropertyName( "text" )] public string Text { get; set; } }
	private class GeminiResp { [JsonPropertyName( "candidates" )] public List<GeminiCandidate> Candidates { get; set; } }
	private class GeminiCandidate { [JsonPropertyName( "content" )] public GeminiContent Content { get; set; } }
	private class GeminiContent { [JsonPropertyName( "parts" )] public List<GeminiPart> Parts { get; set; } }
	private class GeminiPart { [JsonPropertyName( "text" )] public string Text { get; set; } }
}
