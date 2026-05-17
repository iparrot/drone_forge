using System;
using Sandbox.UI;

/// <summary>
/// Per-player credits (GDD V4.2 whole-credit economy). V1 is single-player / local;
/// multiplayer sync can be added later with [Sync] on this component when in scope.
/// </summary>
[Title( "Player Wallet" )]
[Category( "Economy" )]
public sealed class PlayerWallet : Component
{
	[Property] public int Credits { get; private set; }

	/// <summary>Fired after a successful balance change (oldCredits, newCredits).</summary>
	public event Action<int, int> OnCreditsChanged;

	/// <summary>Add credits (sales, pickups). Ignores non-positive amounts.</summary>
	public void AddCredits( int amount )
	{
		if ( amount <= 0 )
			return;

		SetCredits( Credits + amount );
	}

	/// <summary>Spend credits (upgrades, unlocks). Returns false if insufficient balance.</summary>
	public bool TrySpend( int amount )
	{
		if ( amount <= 0 )
			return true;

		if ( Credits < amount )
			return false;

		SetCredits( Credits - amount );
		return true;
	}

	/// <summary>Used by admin / dev only; clamps to zero.</summary>
	public void SetCredits( int value )
	{
		var next = Math.Max( 0, value );
		if ( next == Credits )
			return;

		var prev = Credits;
		Credits = next;
		OnCreditsChanged?.Invoke( prev, Credits );
	}

	/// <summary>Resolve a wallet from a player root (self + descendants). Not for per-frame use.</summary>
	public static bool TryGet( GameObject root, out PlayerWallet wallet )
	{
		wallet = root?.Components.Get<PlayerWallet>( FindMode.EnabledInSelfAndDescendants );
		return wallet is not null;
	}
}

/// <summary>
/// Minimal screen HUD for credits; updates from <see cref="PlayerWallet.OnCreditsChanged"/> (GDD: UI driven by events).
/// Wire <see cref="Wallet"/> in the inspector, or place on the same GameObject as <see cref="PlayerWallet"/>.
/// </summary>
[Title( "Player Wallet HUD" )]
[Category( "Economy" )]
public sealed class PlayerWalletCreditsHud : Component
{
	[Property] public PlayerWallet Wallet { get; set; }

	[Property] public string Prefix { get; set; } = "Credits: ";

	GameObject _hudObject;
	ScreenPanel _screenPanel;
	Label _label;
	bool _ownsHudObject;

	protected override void OnStart()
	{
		ResolveWallet();
		if ( Wallet is null )
		{
			Log.Warning( $"{nameof( PlayerWalletCreditsHud )} on {GameObject?.Name}: assign {nameof( Wallet )} or add {nameof( PlayerWallet )} to the same hierarchy." );
			return;
		}

		Wallet.OnCreditsChanged += OnCreditsChanged;
		BuildHud();
		RefreshLabel( Wallet.Credits );
	}

	protected override void OnDestroy()
	{
		if ( Wallet is not null )
			Wallet.OnCreditsChanged -= OnCreditsChanged;

		if ( _ownsHudObject && _hudObject.IsValid() )
			_hudObject.Destroy();
	}

	void ResolveWallet()
	{
		if ( Wallet.IsValid() )
			return;

		Wallet = Components.Get<PlayerWallet>( FindMode.EnabledInSelf )
			?? GameObject?.Parent?.Components.Get<PlayerWallet>( FindMode.EnabledInSelfAndDescendants );
	}

	void BuildHud()
	{
		_hudObject = new GameObject( true, "PlayerWalletCreditsHud" );
		_hudObject.Flags = GameObjectFlags.None;
		_screenPanel = _hudObject.Components.Create<ScreenPanel>();
		_screenPanel.ZIndex = 100;

		var root = _screenPanel.GetPanel();
		_label = new Label { Parent = root };
		_label.Style.Position = PositionMode.Absolute;
		_label.Style.Top = Length.Pixels( 24 );
		_label.Style.Right = Length.Pixels( 24 );
		_label.Style.FontColor = Color.White;
		_label.Style.FontSize = Length.Pixels( 22 );

		_ownsHudObject = true;
	}

	void OnCreditsChanged( int _, int newCredits )
	{
		RefreshLabel( newCredits );
	}

	void RefreshLabel( int credits )
	{
		if ( !_label.IsValid() )
			return;

		_label.Text = $"{Prefix}{credits:N0}";
	}
}
