using Sandbox;
using Sandbox.UI;
using Conna.Inventory;
using System;
using System.Linq;

namespace DroneForge;

/// <summary>
/// Player inventory UI with TAB toggle.
/// Features: weight display, credits display, tooltips, and basic drag-and-drop support.
/// </summary>
[Title( "Player Inventory UI" )]
[Category( "Inventory" )]
public class PlayerInventoryUI : Component
{
	[Property] public PlayerInventoryManager InventoryManager { get; set; }
	[Property] public PlayerWallet Wallet { get; set; }
	[Property] public SoundEvent OpenSound { get; set; }
	[Property] public SoundEvent CloseSound { get; set; }
	[Property] public SoundEvent MoveSound { get; set; }

	private ScreenPanel _panel;
	private Panel _root;
	private Panel _gridRoot;
	private Label _weightLabel;
	private Label _creditsLabel;
	private Panel _tooltip;
	private Label _tooltipLabel;
	private bool _open;
	private Panel _draggedSlot;
	private EconomyItem _draggedItem;
	private int _dragStartX;
	private int _dragStartY;
	private const int CellSize = 64;
	private const int GridWidth = 10;
	private const int GridHeight = 6;

	protected override void OnStart()
	{
		if ( InventoryManager == null )
			InventoryManager = Components.Get<PlayerInventoryManager>();

		if ( Wallet == null )
			Wallet = GameObject?.Components.Get<PlayerWallet>( FindMode.EnabledInSelfAndDescendants )
				?? GameObject?.Parent?.Components.Get<PlayerWallet>( FindMode.EnabledInSelfAndDescendants );

		BuildUI();
		_panel.Enabled = false;

		// Subscribe to inventory changes for live updates
		if ( InventoryManager != null )
		{
			InventoryManager.OnWeightChanged += OnWeightChanged;
		}

		if ( Wallet != null )
		{
			Wallet.OnCreditsChanged += OnCreditsChanged;
		}
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( "inventory" ) )
		{
			ToggleInventory();
		}

		// Handle drag-and-drop release
		if ( _draggedSlot != null && Input.Released( "attack1" ) )
		{
			DropItem();
		}
	}

	protected override void OnDestroy()
	{
		if ( InventoryManager != null )
			InventoryManager.OnWeightChanged -= OnWeightChanged;

		if ( Wallet != null )
			Wallet.OnCreditsChanged -= OnCreditsChanged;
	}

	private void ToggleInventory()
	{
		_open = !_open;
		_panel.Enabled = _open;

		if ( _open )
		{
			RefreshGrid();
			UpdateWeightDisplay();
			UpdateCreditsDisplay();
			PlaySound( OpenSound );
		}
		else
		{
			PlaySound( CloseSound );
		}
	}

	private void PlaySound( SoundEvent sound )
	{
		if ( sound != null )
		{
			// Play sound at camera position
			var cam = Scene.Camera;
			if ( cam != null && cam.GameObject != null )
			{
				Sound.Play( sound, cam.GameObject.WorldPosition );
			}
		}
	}

	private void OnWeightChanged()
	{
		UpdateWeightDisplay();
	}

	private void OnCreditsChanged( int oldCredits, int newCredits )
	{
		UpdateCreditsDisplay();
	}

	private void BuildUI()
	{
		_panel = Components.Create<ScreenPanel>();
		_root = _panel.GetPanel();

		// Main panel styling
		_root.Style.BackgroundColor = new Color( 0.08f, 0.08f, 0.1f, 0.95f );
		_root.Style.Width = Length.Pixels( 720 );
		_root.Style.Height = Length.Pixels( 520 );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels( 100 );
		_root.Style.Top = Length.Pixels( 100 );

		// Title
		var title = new Label { Parent = _root, Text = "INVENTORY" };
		title.Style.FontSize = Length.Pixels( 24 );
		title.Style.FontWeight = 700;
		title.Style.Top = Length.Pixels( 16 );
		title.Style.Left = Length.Pixels( 20 );
		title.Style.FontColor = Color.White;

		// Status bar (weight and credits)
		var statusBar = new Panel { Parent = _root };
		statusBar.Style.Display = DisplayMode.Flex;
		statusBar.Style.FlexDirection = FlexDirection.Row;
		statusBar.Style.Top = Length.Pixels( 50 );
		statusBar.Style.Left = Length.Pixels( 20 );
		statusBar.Style.Right = Length.Pixels( 20 );
		statusBar.Style.Height = Length.Pixels( 24 );

		// Weight display
		_weightLabel = new Label { Parent = statusBar, Text = "Weight: 0 / 50 kg" };
		_weightLabel.Style.FontSize = Length.Pixels( 14 );
		_weightLabel.Style.FontColor = Color.White;

		// Credits display
		_creditsLabel = new Label { Parent = statusBar, Text = "Credits: 0" };
		_creditsLabel.Style.FontSize = Length.Pixels( 14 );
		_creditsLabel.Style.FontColor = new Color( 1f, 0.84f, 0f ); // Gold color

		// Inventory grid container - using absolute positioning for cells
		_gridRoot = new Panel { Parent = _root };
		_gridRoot.Style.Position = PositionMode.Absolute;
		_gridRoot.Style.Top = Length.Pixels( 90 );
		_gridRoot.Style.Left = Length.Pixels( 20 );
		_gridRoot.Style.Width = Length.Pixels( GridWidth * CellSize );
		_gridRoot.Style.Height = Length.Pixels( GridHeight * CellSize );

		// Create empty grid cells
		for ( int y = 0; y < GridHeight; y++ )
		{
			for ( int x = 0; x < GridWidth; x++ )
			{
				var cell = new Panel { Parent = _gridRoot };
				cell.Style.Position = PositionMode.Absolute;
				cell.Style.Left = Length.Pixels( x * CellSize );
				cell.Style.Top = Length.Pixels( y * CellSize );
				cell.Style.Width = Length.Pixels( CellSize );
				cell.Style.Height = Length.Pixels( CellSize );
				cell.Style.BackgroundColor = new Color( 0.15f, 0.15f, 0.18f );
				cell.Style.BorderColor = new Color( 0.25f, 0.25f, 0.3f );
				cell.Style.BorderWidth = Length.Pixels( 1 );
				cell.SetAttribute( "data-index", $"{x},{y}" );
			}
		}

		// Tooltip
		_tooltip = new Panel { Parent = _root };
		_tooltip.Style.Position = PositionMode.Absolute;
		_tooltip.Style.Display = DisplayMode.None;
		_tooltip.Style.BackgroundColor = new Color( 0.05f, 0.05f, 0.08f, 0.95f );
		_tooltip.Style.Padding = Length.Pixels( 10 );
		_tooltip.Style.BorderColor = new Color( 0.3f, 0.3f, 0.4f );
		_tooltip.Style.BorderWidth = Length.Pixels( 1 );
		_tooltip.Style.ZIndex = 1000;
		_tooltip.Style.MaxWidth = Length.Pixels( 250 );

		_tooltipLabel = new Label { Parent = _tooltip, Text = "" };
		_tooltipLabel.Style.FontSize = Length.Pixels( 12 );
		_tooltipLabel.Style.FontColor = Color.White;
	}

	private void RefreshGrid()
	{
		// Clear existing item slots (keep empty cells)
		var children = _gridRoot.Children.ToList();
		foreach ( var child in children )
		{
			if ( child.GetAttribute( "data-index" ) == null )
			{
				child.Delete();
			}
			else
			{
				// Reset cell background
				child.Style.BackgroundColor = new Color( 0.15f, 0.15f, 0.18f );
			}
		}

		if ( InventoryManager?.MainInventory == null )
			return;

		// Place items in grid
		foreach ( var entry in InventoryManager.MainInventory.Entries )
		{
			if ( entry.Item is not EconomyItem eco )
				continue;

			int x = entry.Slot.X;
			int y = entry.Slot.Y;
			int w = entry.Slot.W;
			int h = entry.Slot.H;

			// Create item slot overlay using absolute positioning
			var itemSlot = new Panel { Parent = _gridRoot };
			itemSlot.SetAttribute( "data-item", "true" );
			itemSlot.SetAttribute( "data-item-id", entry.Item.Id.ToString() );
			itemSlot.SetAttribute( "data-x", x.ToString() );
			itemSlot.SetAttribute( "data-y", y.ToString() );

			itemSlot.Style.Position = PositionMode.Absolute;
			itemSlot.Style.Left = Length.Pixels( x * CellSize );
			itemSlot.Style.Top = Length.Pixels( y * CellSize );
			itemSlot.Style.Width = Length.Pixels( w * CellSize );
			itemSlot.Style.Height = Length.Pixels( h * CellSize );
			itemSlot.Style.BackgroundColor = new Color( 0.25f, 0.25f, 0.3f );
			itemSlot.Style.BorderColor = new Color( 0.4f, 0.4f, 0.5f );
			itemSlot.Style.BorderWidth = Length.Pixels( 1 );
			itemSlot.Style.Cursor = "pointer";

			// Add icon if available
			if ( eco.Icon != null )
			{
				// Create a label with the item name as fallback
				var nameLabel = new Label { Parent = itemSlot, Text = eco.DisplayName };
				nameLabel.Style.FontSize = Length.Pixels( 11 );
				nameLabel.Style.FontColor = Color.White;
				nameLabel.Style.TextAlign = TextAlign.Center;
				nameLabel.Style.Position = PositionMode.Absolute;
				nameLabel.Style.Left = Length.Pixels( 0 );
				nameLabel.Style.Right = Length.Pixels( 0 );
				nameLabel.Style.Top = Length.Pixels( 50 );
			}

			// Stack count
			if ( eco.StackCount > 1 )
			{
				var stackLabel = new Label { Parent = itemSlot, Text = eco.StackCount.ToString() };
				stackLabel.Style.FontSize = Length.Pixels( 13 );
				stackLabel.Style.FontWeight = 700;
				stackLabel.Style.FontColor = Color.White;
				stackLabel.Style.Position = PositionMode.Absolute;
				stackLabel.Style.Right = Length.Pixels( 4 );
				stackLabel.Style.Bottom = Length.Pixels( 2 );
			}

			// Weight label
			var weightLabel = new Label { Parent = itemSlot, Text = $"{eco.UnitWeight}kg" };
			weightLabel.Style.FontSize = Length.Pixels( 10 );
			weightLabel.Style.FontColor = new Color( 0.7f, 0.7f, 0.7f );
			weightLabel.Style.Position = PositionMode.Absolute;
			weightLabel.Style.Left = Length.Pixels( 2 );
			weightLabel.Style.Bottom = Length.Pixels( 2 );

			// Tooltip on hover
			itemSlot.AddEventListener( "mouseenter", e => ShowTooltip( eco ) );
			itemSlot.AddEventListener( "mouseleave", e => HideTooltip() );

			// Drag start
			itemSlot.AddEventListener( "mousedown", e => StartDrag( itemSlot, eco, x, y ) );
		}

		UpdateWeightDisplay();
		UpdateCreditsDisplay();
	}

	private void ShowTooltip( EconomyItem item )
	{
		_tooltipLabel.Text = $"<b>{item.DisplayName}</b>\n{item.Description}\n\nWeight: {item.UnitWeight}kg\nValue: {item.UnitBuyPrice} credits";
		_tooltip.Style.Display = DisplayMode.Flex;
	}

	private void HideTooltip()
	{
		_tooltip.Style.Display = DisplayMode.None;
	}

	private void StartDrag( Panel slot, EconomyItem item, int x, int y )
	{
		_draggedSlot = slot;
		_draggedItem = item;
		_dragStartX = x;
		_dragStartY = y;

		// Visual feedback
		slot.Style.Opacity = 0.5f;
		slot.Style.ZIndex = 100;
	}

	private void DropItem()
	{
		if ( _draggedSlot == null || _draggedItem == null )
			return;

		// Restore visual
		_draggedSlot.Style.Opacity = 1f;
		_draggedSlot.Style.ZIndex = 0;

		// TODO: Implement actual drop logic (calculate drop position and move item)
		// For now, just cancel the drag
		_draggedSlot = null;
		_draggedItem = null;

		PlaySound( MoveSound );
	}

	private void UpdateWeightDisplay()
	{
		if ( _weightLabel.IsValid() && InventoryManager != null )
		{
			float current = InventoryManager.CurrentWeight;
			float max = InventoryManager.MaxCarryCapacity;
			_weightLabel.Text = $"Weight: {current:F1} / {max:F0} kg";

			// Color based on encumbrance
			float ratio = current / max;
			if ( ratio >= 1.0f )
				_weightLabel.Style.FontColor = new Color( 1f, 0.3f, 0.3f ); // Red - overencumbered
			else if ( ratio >= 0.7f )
				_weightLabel.Style.FontColor = new Color( 1f, 0.8f, 0.2f ); // Yellow - encumbered
			else
				_weightLabel.Style.FontColor = new Color( 0.3f, 1f, 0.3f ); // Green - normal
		}
	}

	private void UpdateCreditsDisplay()
	{
		if ( _creditsLabel.IsValid() && Wallet != null )
		{
			_creditsLabel.Text = $"Credits: {Wallet.Credits:N0}";
		}
	}
}