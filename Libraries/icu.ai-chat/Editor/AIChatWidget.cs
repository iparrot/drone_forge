using Editor;
using Sandbox;
using System;

/// <summary>
/// AI Chat — dockable editor panel.
/// Supports Ollama, OpenAI, Anthropic Claude, Google Gemini, GitHub Models.
/// </summary>
[Dock( "Editor", "AI Chat", "smart_toy" )]
public class AIChatDock : Widget
{
	private AIChatbot _bot = new();
	private LineEdit _inputField;
	private LineEdit _modelField;
	private LineEdit _apiKeyField;
	private Button _sendButton;
	private ScrollArea _scrollArea;
	private Widget _chatContainer;
	private ComboBox _providerBox;
	private string _lastAiResponse = "";

	public AIChatDock( Widget parent ) : base( parent )
	{
		MinimumSize = new Vector2( 350, 450 );
		_bot.Reset();

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		// Provider selector
		var providerRow = Layout.Add( Layout.Row() );
		providerRow.Spacing = 4;
		providerRow.Add( new Label( "Provider:" ) );
		_providerBox = new ComboBox( this );
		_providerBox.AddItem( "Ollama (Local)", null, () => SetProvider( AIProvider.Ollama ) );
		_providerBox.AddItem( "OpenAI", null, () => SetProvider( AIProvider.OpenAI ) );
		_providerBox.AddItem( "Anthropic (Claude)", null, () => SetProvider( AIProvider.Anthropic ) );
		_providerBox.AddItem( "Google (Gemini)", null, () => SetProvider( AIProvider.Google ) );
		_providerBox.AddItem( "GitHub Models", null, () => SetProvider( AIProvider.GitHub ) );
		_providerBox.AddItem( "Custom Endpoint", null, () => SetProvider( AIProvider.Custom ) );
		_providerBox.CurrentIndex = 0;
		providerRow.Add( _providerBox, 1 );

		// Model selector
		var modelRow = Layout.Add( Layout.Row() );
		modelRow.Spacing = 4;
		modelRow.Add( new Label( "Model:" ) );
		_modelField = new LineEdit( _bot.Model );
		_modelField.TextEdited += ( t ) => _bot.Model = t;
		modelRow.Add( _modelField, 1 );

		// API Key
		var keyRow = Layout.Add( Layout.Row() );
		keyRow.Spacing = 4;
		keyRow.Add( new Label( "API Key:" ) );
		_apiKeyField = new LineEdit( "" );
		_apiKeyField.PlaceholderText = "Not needed for Ollama";
		_apiKeyField.TextEdited += ( t ) => _bot.ApiKey = t;
		keyRow.Add( _apiKeyField, 1 );

		// System prompt label
		var promptRow = Layout.Add( Layout.Row() );
		promptRow.Spacing = 4;
		promptRow.Add( new Label( "System: S&box Expert (built-in)" ) );

		// Scrollable chat area
		_scrollArea = new ScrollArea( this );
		_scrollArea.Canvas = new Widget( _scrollArea );
		_scrollArea.Canvas.Layout = Layout.Column();
		_scrollArea.Canvas.Layout.Spacing = 4;
		_chatContainer = _scrollArea.Canvas;
		Layout.Add( _scrollArea, 1 );

		AddChatBubble( "System", "AI Chat ready. Select a provider and type a message.\n\nOllama: Free & local\nOpenAI/Claude/Gemini/GitHub: Needs API key" );

		// Input row
		var inputRow = Layout.Add( Layout.Row() );
		inputRow.Spacing = 4;

		_inputField = new LineEdit( "" );
		_inputField.PlaceholderText = "Type a message...";
		_inputField.ReturnPressed += OnSend;
		inputRow.Add( _inputField, 1 );

		_sendButton = new Button( "Send" );
		_sendButton.Clicked += OnSend;
		inputRow.Add( _sendButton );

		var clearBtn = new Button( "Clear" );
		clearBtn.Clicked += OnClear;
		inputRow.Add( clearBtn );
	}

	private void SetProvider( AIProvider provider )
	{
		_bot.Provider = provider;
		_bot.Model = AIChatbot.DefaultModels[provider];
		_modelField.Text = _bot.Model;
		_apiKeyField.PlaceholderText = provider == AIProvider.Ollama ? "Not needed for Ollama" : "Enter API key...";
		_bot.Reset();
		AddChatBubble( "System", $"Switched to {provider}. Model: {_bot.Model}" );
	}

	private void OnClear()
	{
		_chatContainer.Layout.Clear( true );
		_bot.Reset();
		AddChatBubble( "System", "Chat cleared." );
	}

	private Label _thinkingLabel;

	private async void OnSend()
	{
		var msg = _inputField.Text?.Trim();
		if ( string.IsNullOrEmpty( msg ) || _bot.IsThinking ) return;

		_inputField.Text = "";
		_sendButton.Enabled = false;

		AddChatBubble( "You", msg );
		_thinkingLabel = AddChatBubble( "AI", "Thinking..." );

		var response = await _bot.SendMessage( msg );
		_lastAiResponse = response;

		// Remove thinking label and add real response
		if ( _thinkingLabel != null )
		{
			_thinkingLabel.Parent.Destroy();
			_thinkingLabel = null;
		}

		AddChatBubble( "AI", response, true );
		_sendButton.Enabled = true;
	}

	private Label AddChatBubble( string sender, string text, bool copyable = false )
	{
		var bubble = new Widget( _chatContainer );
		bubble.Layout = Layout.Column();
		bubble.Layout.Margin = 4;

		// Sender label
		var senderLabel = new Label( sender, bubble );
		senderLabel.SetStyles( "font-weight: bold; font-size: 11px; color: " + sender switch
		{
			"You" => "#6699cc",
			"AI" => "#66cc66",
			_ => "#999999"
		} + ";" );
		bubble.Layout.Add( senderLabel );

		// Message text
		var msgLabel = new Label( text, bubble );
		msgLabel.WordWrap = true;
		msgLabel.SetStyles( "font-size: 13px; color: white; padding: 4px;" );
		bubble.Layout.Add( msgLabel );

		// Copy button for AI responses
		if ( copyable )
		{
			var btnRow = bubble.Layout.Add( Layout.Row() );
			btnRow.Spacing = 4;

			var copyAllBtn = new Button( "Copy All", bubble );
			copyAllBtn.Clicked += () =>
			{
				EditorUtility.Clipboard.Copy( text );
				Log.Info( "Copied full response to clipboard" );
			};
			btnRow.Add( copyAllBtn );

			// If response contains code blocks, add Copy Code + Apply Code buttons
			if ( text.Contains( "```" ) )
			{
				var copyCodeBtn = new Button( "Copy Code", bubble );
				copyCodeBtn.Clicked += () =>
				{
					var code = ExtractCode( text );
					EditorUtility.Clipboard.Copy( code );
					Log.Info( "Copied code to clipboard" );
				};
				btnRow.Add( copyCodeBtn );

				var applyBtn = new Button( "Apply Code", bubble );
				applyBtn.SetStyles( "background-color: #2d5a2d; color: white;" );
				applyBtn.Clicked += () =>
				{
					var code = ExtractCode( text );
					ApplyCode( code );
				};
				btnRow.Add( applyBtn );
			}
		}

		_chatContainer.Layout.Add( bubble );
		return msgLabel;
	}

	/// <summary>
	/// Extract code from markdown code blocks.
	/// </summary>
	private static string ExtractCode( string text )
	{
		var result = "";
		var lines = text.Split( '\n' );
		bool inBlock = false;

		foreach ( var line in lines )
		{
			if ( line.TrimStart().StartsWith( "```" ) )
			{
				if ( inBlock )
				{
					result += "\n";
					inBlock = false;
				}
				else
				{
					inBlock = true;
				}
				continue;
			}

			if ( inBlock )
			{
				result += line + "\n";
			}
		}

		return result.Trim();
	}

	/// <summary>
	/// Extract class name from C# code.
	/// </summary>
	private static string ExtractClassName( string code )
	{
		var lines = code.Split( '\n' );
		foreach ( var line in lines )
		{
			var trimmed = line.Trim();
			// Match: public sealed class ClassName : Component
			if ( trimmed.Contains( "class " ) )
			{
				var parts = trimmed.Split( ' ' );
				for ( int i = 0; i < parts.Length - 1; i++ )
				{
					if ( parts[i] == "class" )
						return parts[i + 1].Split( ':' )[0].Split( '<' )[0].Trim();
				}
			}
		}
		return "AIGenerated";
	}

	/// <summary>
	/// Apply code — saves as a .cs file in the project's Code folder.
	/// S&box auto-recompiles when files change.
	/// </summary>
	private void ApplyCode( string code )
	{
		if ( string.IsNullOrWhiteSpace( code ) )
		{
			AddChatBubble( "System", "No code found to apply." );
			return;
		}

		// Get class name for filename
		var className = ExtractClassName( code );
		var fileName = $"{className}.cs";

		// Find the project's Code directory
		var project = Project.Current;
		if ( project == null )
		{
			AddChatBubble( "System", "Error: No active project found." );
			return;
		}

		var codePath = System.IO.Path.Combine( project.GetCodePath(), fileName );

		// Check if file already exists
		if ( System.IO.File.Exists( codePath ) )
		{
			// Add a number suffix
			var i = 1;
			while ( System.IO.File.Exists( codePath ) )
			{
				codePath = System.IO.Path.Combine( project.GetCodePath(), $"{className}_{i}.cs" );
				i++;
			}
			fileName = System.IO.Path.GetFileName( codePath );
		}

		try
		{
			System.IO.File.WriteAllText( codePath, code );
			AddChatBubble( "System", $"Code saved to: Code/{fileName}\nS&box will auto-recompile. The new component will appear in the editor." );
			Log.Info( $"AI Chat: Applied code to {codePath}" );
		}
		catch ( System.Exception e )
		{
			AddChatBubble( "System", $"Error saving file: {e.Message}" );
			Log.Error( $"AI Chat apply error: {e.Message}" );
		}
	}
}
