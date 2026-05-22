using System.Linq;
using Conna.Inventory;
using Sandbox;
using Sandbox.UI;

namespace DroneForge;

/// <summary>
/// Screen panel that displays a shop's stock and lets the player buy items.
/// Opened by <see cref="ShopInteractor"/> when the player presses Use nearby.
/// </summary>
[Title( "Shop UI" )]
[Category( "Inventory" )]
public sealed class ShopUI : Component
{
	private GameObject _hudObject;
	private ScreenPanel _panel;
	private Panel _root;
	private Label _titleLabel;
	private Label _creditsLabel;
	private Panel _itemList;
	private Label _statusLabel;

	private ShopInventory _shop;
	private PlayerInventoryManager _player;
	private PlayerWallet _wallet;
	private bool _open;

	protected override void OnStart()
	{
		BuildUI();
		_panel.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( _open && Input.EscapePressed )
		{
			Close();
		}
	}

	protected override void OnDestroy()
	{
		UnsubscribeShop();
		UnsubscribeWallet();
		if ( _hudObject.IsValid() )
			_hudObject.Destroy();
	}

	public void Toggle( ShopInventory shop, PlayerInventoryManager player, PlayerWallet wallet )
	{
		if ( _open && _shop == shop )
		{
			Close();
			return;
		}

		Open( shop, player, wallet );
	}

	public void Open( ShopInventory shop, PlayerInventoryManager player, PlayerWallet wallet )
	{
		if ( _panel == null ) BuildUI();

		UnsubscribeShop();
		UnsubscribeWallet();

		_shop = shop;
		_player = player;
		_wallet = wallet;

		if ( _shop != null )
			_shop.OnTransactionComplete += OnTransactionComplete;
		if ( _wallet != null )
			_wallet.OnCreditsChanged += OnCreditsChanged;

		_panel.Enabled = true;
		_open = true;
		Refresh();
	}

	public void Close()
	{
		_panel.Enabled = false;
		_open = false;
		ClearStatus();
		UnsubscribeShop();
		UnsubscribeWallet();
		_shop = null;
		_player = null;
		_wallet = null;
	}

	private void UnsubscribeShop()
	{
		if ( _shop != null )
			_shop.OnTransactionComplete -= OnTransactionComplete;
	}

	private void UnsubscribeWallet()
	{
		if ( _wallet != null )
			_wallet.OnCreditsChanged -= OnCreditsChanged;
	}

	private void OnTransactionComplete( ShopTransactionResultInfo info )
	{
		Refresh();
	}

	private void OnCreditsChanged( int oldCredits, int newCredits )
	{
		UpdateCreditsDisplay();
	}

	private void BuildUI()
	{
		_hudObject = new GameObject( true, "ShopUI" );
		_panel = _hudObject.Components.Create<ScreenPanel>();
		_root = _panel.GetPanel();

		_root.Style.BackgroundColor = new Color( 0.08f, 0.08f, 0.1f, 0.95f );
		_root.Style.Width = Length.Pixels( 520 );
		_root.Style.Height = Length.Pixels( 520 );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels( 840 );
		_root.Style.Top = Length.Pixels( 100 );
		_root.Style.BorderColor = new Color( 0.3f, 0.3f, 0.4f );
		_root.Style.BorderWidth = Length.Pixels( 1 );

		_titleLabel = new Label { Parent = _root, Text = "SHOP" };
		_titleLabel.Style.FontSize = Length.Pixels( 24 );
		_titleLabel.Style.FontWeight = 700;
		_titleLabel.Style.Top = Length.Pixels( 16 );
		_titleLabel.Style.Left = Length.Pixels( 20 );
		_titleLabel.Style.FontColor = Color.White;

		_creditsLabel = new Label { Parent = _root, Text = "Credits: 0" };
		_creditsLabel.Style.FontSize = Length.Pixels( 14 );
		_creditsLabel.Style.FontColor = new Color( 1f, 0.84f, 0f );
		_creditsLabel.Style.Top = Length.Pixels( 22 );
		_creditsLabel.Style.Right = Length.Pixels( 20 );
		_creditsLabel.Style.Position = PositionMode.Absolute;

		_itemList = new Panel { Parent = _root };
		_itemList.Style.Position = PositionMode.Absolute;
		_itemList.Style.Top = Length.Pixels( 60 );
		_itemList.Style.Left = Length.Pixels( 20 );
		_itemList.Style.Right = Length.Pixels( 20 );
		_itemList.Style.Bottom = Length.Pixels( 60 );
		_itemList.Style.FlexDirection = FlexDirection.Column;
		_itemList.Style.Display = DisplayMode.Flex;
		_itemList.Style.OverflowY = OverflowMode.Scroll;

		_statusLabel = new Label { Parent = _root, Text = "" };
		_statusLabel.Style.FontSize = Length.Pixels( 13 );
		_statusLabel.Style.FontColor = Color.White;
		_statusLabel.Style.Position = PositionMode.Absolute;
		_statusLabel.Style.Bottom = Length.Pixels( 20 );
		_statusLabel.Style.Left = Length.Pixels( 20 );
		_statusLabel.Style.Right = Length.Pixels( 20 );
	}

	private void Refresh()
	{
		_titleLabel.Text = _shop?.ShopName?.ToUpperInvariant() ?? "SHOP";
		UpdateCreditsDisplay();
		RebuildItemList();
	}

	private void UpdateCreditsDisplay()
	{
		if ( _creditsLabel.IsValid() && _wallet != null )
			_creditsLabel.Text = $"Credits: {_wallet.Credits:N0}";
	}

	private void RebuildItemList()
	{
		_itemList.DeleteChildren( true );

		if ( _shop == null ) return;

		foreach ( var shopItem in _shop.ForSale.ToList() )
		{
			if ( shopItem?.Asset == null ) continue;

			var row = new Panel { Parent = _itemList };
			row.Style.Display = DisplayMode.Flex;
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.Height = Length.Pixels( 56 );
			row.Style.MarginBottom = Length.Pixels( 6 );
			row.Style.PaddingLeft = Length.Pixels( 10 );
			row.Style.PaddingRight = Length.Pixels( 10 );
			row.Style.BackgroundColor = new Color( 0.13f, 0.13f, 0.16f );
			row.Style.BorderColor = new Color( 0.25f, 0.25f, 0.3f );
			row.Style.BorderWidth = Length.Pixels( 1 );

			var info = new Label
			{
				Parent = row,
				Text = $"{shopItem.Asset.DisplayName}   [{shopItem.Asset.Width}x{shopItem.Asset.Height}]   {shopItem.Asset.Weight}kg"
			};
			info.Style.FontSize = Length.Pixels( 14 );
			info.Style.FontColor = Color.White;
			info.Style.FlexGrow = 1;

			var stock = new Label
			{
				Parent = row,
				Text = shopItem.MaxStock > 0 ? $"x{shopItem.StockCount}" : "x∞"
			};
			stock.Style.FontSize = Length.Pixels( 12 );
			stock.Style.FontColor = new Color( 0.7f, 0.7f, 0.7f );
			stock.Style.MarginRight = Length.Pixels( 12 );

			var price = new Label { Parent = row, Text = $"{shopItem.BuyPrice}c" };
			price.Style.FontSize = Length.Pixels( 14 );
			price.Style.FontColor = new Color( 1f, 0.84f, 0f );
			price.Style.MarginRight = Length.Pixels( 12 );

			var capturedItem = shopItem;
			var buyButton = new Button( "Buy" ) { Parent = row };
			buyButton.Style.PaddingLeft = Length.Pixels( 12 );
			buyButton.Style.PaddingRight = Length.Pixels( 12 );
			buyButton.Style.PaddingTop = Length.Pixels( 6 );
			buyButton.Style.PaddingBottom = Length.Pixels( 6 );
			buyButton.Style.BackgroundColor = new Color( 0.2f, 0.5f, 0.8f );
			buyButton.Style.FontColor = Color.White;

			if ( shopItem.IsInStock )
			{
				buyButton.AddEventListener( "onclick", e => TryBuy( capturedItem ) );
			}
			else
			{
				row.Style.Opacity = 0.5f;
				buyButton.Style.BackgroundColor = new Color( 0.3f, 0.3f, 0.3f );
			}
		}
	}

	private void TryBuy( ShopItem shopItem )
	{
		if ( _shop == null || _player?.MainInventory == null || _wallet == null )
		{
			ShowStatus( "Shop not initialized.", error: true );
			return;
		}

		var result = _shop.PurchaseItem(
			_player.MainInventory,
			_wallet,
			shopItem,
			quantity: 1,
			maxCarryCapacity: _player.MaxCarryCapacity
		);

		if ( result.IsSuccess )
			ShowStatus( $"Purchased {result.PurchasedItem.DisplayName} for {result.CreditsSpent}c.", error: false );
		else
			ShowStatus( result.Message, error: true );
	}

	private void ShowStatus( string message, bool error )
	{
		_statusLabel.Text = message;
		_statusLabel.Style.FontColor = error
			? new Color( 1f, 0.4f, 0.4f )
			: new Color( 0.4f, 1f, 0.5f );
	}

	private void ClearStatus()
	{
		_statusLabel.Text = "";
	}
}
