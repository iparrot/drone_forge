using System;
using System.Collections.Generic;
using Conna.Inventory;
using Sandbox;

namespace DroneForge;

/// <summary>
/// Manages all inventory-related systems for a player, including main inventory,
/// equipment, weight calculations, and integration with the wallet system.
/// </summary>
[Title( "Player Inventory Manager" )]
[Category( "Inventory" )]
public sealed class PlayerInventoryManager : Component
{
	#region Properties

	/// <summary>
	/// The player's main inventory (grid-based, Tetris-style).
	/// </summary>
	public BaseInventory MainInventory { get; private set; }

	/// <summary>
	/// The player's equipment inventory (for items that provide bonuses).
	/// </summary>
	public BaseInventory EquipmentInventory { get; private set; }

	/// <summary>
	/// Reference to the player's wallet component.
	/// </summary>
	public PlayerWallet Wallet { get; private set; }

	/// <summary>
	/// Base carry capacity in kilograms (before equipment bonuses).
	/// </summary>
	[Property, Group( "Settings" )]
	public float BaseCapacity { get; set; } = InventoryWeightCalculator.DefaultBaseCapacity;

	/// <summary>
	/// Main inventory grid dimensions (width x height in cells).
	/// </summary>
	[Property, Group( "Settings" )]
	public int MainInventoryWidth { get; set; } = 10;

	[Property, Group( "Settings" )]
	public int MainInventoryHeight { get; set; } = 6;

	/// <summary>
	/// Equipment inventory grid dimensions.
	/// </summary>
	[Property, Group( "Settings" )]
	public int EquipmentInventoryWidth { get; set; } = 4;

	[Property, Group( "Settings" )]
	public int EquipmentInventoryHeight { get; set; } = 4;

	/// <summary>
	/// Whether networking is enabled for this inventory system.
	/// </summary>
	[Property, Group( "Networking" )]
	public bool EnableNetworking { get; set; } = true;

	/// <summary>
	/// Current total weight of items in main inventory.
	/// </summary>
	public float CurrentWeight => MainInventory?.CalculateTotalWeight() ?? 0f;

	/// <summary>
	/// Maximum carry capacity including equipment bonuses.
	/// </summary>
	public float MaxCarryCapacity => InventoryWeightCalculator.GetMaxCarryCapacity( EquipmentInventory, BaseCapacity );

	/// <summary>
	/// Remaining weight capacity.
	/// </summary>
	public float RemainingCapacity => MaxCarryCapacity - CurrentWeight;

	/// <summary>
	/// Current encumbrance state.
	/// </summary>
	public EncumbranceState EncumbranceState => InventoryWeightCalculator.GetEncumbranceState( MainInventory, EquipmentInventory, BaseCapacity );

	/// <summary>
	/// Movement speed multiplier based on encumbrance.
	/// </summary>
	public float SpeedMultiplier => InventoryWeightCalculator.GetSpeedMultiplier( MainInventory, EquipmentInventory, BaseCapacity );

	/// <summary>
	/// Fired when inventory weight changes.
	/// </summary>
	public event Action OnWeightChanged;

	/// <summary>
	/// Fired when encumbrance state changes.
	/// </summary>
	public event Action<EncumbranceState> OnEncumbranceChanged;

	private EncumbranceState _lastEncumbranceState;

	#endregion

	#region Initialization

	protected override void OnAwake()
	{
		// Create main inventory with unique ID
		MainInventory = new PlayerInventory( Guid.NewGuid() );

		// Create equipment inventory
		EquipmentInventory = new EquipmentInventory( Guid.NewGuid() );

		// Enable networking if configured
		if ( EnableNetworking )
		{
			MainInventory.Network.Enabled = true;
			EquipmentInventory.Network.Enabled = true;
		}

		// Subscribe to inventory change events
		MainInventory.OnInventoryChanged += OnMainInventoryChanged;
		EquipmentInventory.OnInventoryChanged += OnEquipmentInventoryChanged;
	}

	protected override void OnStart()
	{
		// Find wallet component
		Wallet = Components.Get<PlayerWallet>( FindMode.EnabledInSelfAndDescendants )
			?? GameObject?.Parent?.Components.Get<PlayerWallet>( FindMode.EnabledInSelfAndDescendants );

		if ( Wallet == null )
		{
			Log.Warning( $"PlayerInventoryManager on {GameObject?.Name}: No PlayerWallet found. Add one to enable economy features." );
		}
		else
		{
			Wallet.OnCreditsChanged += OnWalletChanged;
		}

		_lastEncumbranceState = EncumbranceState;
	}

	protected override void OnDestroy()
	{
		if ( Wallet != null )
			Wallet.OnCreditsChanged -= OnWalletChanged;

		MainInventory?.OnInventoryChanged -= OnMainInventoryChanged;
		EquipmentInventory?.OnInventoryChanged -= OnEquipmentInventoryChanged;

		MainInventory?.Dispose();
		EquipmentInventory?.Dispose();
	}

	#endregion

	#region Inventory Management

	/// <summary>
	/// Adds an item to the player's main inventory.
	/// </summary>
	/// <param name="item">The item to add.</param>
	/// <returns>Result of the operation.</returns>
	public InventoryResult AddItem( EconomyItem item )
	{
		if ( item == null )
			return InventoryResult.ItemWasNull;

		// Check weight limit
		if ( CurrentWeight + item.CurrentWeight > MaxCarryCapacity )
			return InventoryResult.NoSpaceAvailable;

		return MainInventory.TryAdd( item );
	}

	/// <summary>
	/// Removes an item from the player's main inventory.
	/// </summary>
	/// <param name="item">The item to remove.</param>
	/// <returns>Result of the operation.</returns>
	public InventoryResult RemoveItem( EconomyItem item )
	{
		return MainInventory.TryRemove( item );
	}

	/// <summary>
	/// Drops an item on the ground at the player's position.
	/// </summary>
	/// <param name="item">The item to drop.</param>
	/// <returns>True if dropped successfully.</returns>
	public bool DropItem( EconomyItem item )
	{
		if ( item == null || !item.IsDroppable )
			return false;

		var result = MainInventory.TryRemove( item );
		if ( result != InventoryResult.Success )
			return false;

		// TODO: Spawn world entity for dropped item
		// SpawnDroppedItemEntity( item, GameObject.WorldPosition );

		return true;
	}

	/// <summary>
	/// Picks up an item from the world into the player's inventory.
	/// </summary>
	/// <param name="item">The item to pick up.</param>
	/// <returns>True if picked up successfully.</returns>
	public bool PickupItem( EconomyItem item )
	{
		if ( item == null )
			return false;

		// Check weight
		if ( CurrentWeight + item.CurrentWeight > MaxCarryCapacity )
			return false;

		var result = MainInventory.TryAdd( item );
		return result == InventoryResult.Success;
	}

	/// <summary>
	/// Checks if an item can fit in the player's inventory.
	/// </summary>
	/// <param name="item">The item to check.</param>
	/// <returns>True if it can fit.</returns>
	public bool CanFitItem( EconomyItem item )
	{
		return MainInventory.CanItemFit( item, MaxCarryCapacity );
	}

	/// <summary>
	/// Equips an item from inventory to the equipment slot.
	/// </summary>
	/// <param name="item">The item to equip.</param>
	/// <returns>Result of the operation.</returns>
	public InventoryResult EquipItem( EconomyItem item )
	{
		if ( item == null )
			return InventoryResult.ItemWasNull;

		// Remove from main inventory
		var removeResult = MainInventory.TryRemove( item );
		if ( removeResult != InventoryResult.Success )
			return removeResult;

		// Add to equipment inventory
		var addResult = EquipmentInventory.TryAdd( item );
		if ( addResult != InventoryResult.Success )
		{
			// Put back in main inventory
			MainInventory.TryAdd( item );
			return addResult;
		}

		return InventoryResult.Success;
	}

	/// <summary>
	/// Unequips an item from equipment to main inventory.
	/// </summary>
	/// <param name="item">The item to unequip.</param>
	/// <returns>Result of the operation.</returns>
	public InventoryResult UnequipItem( EconomyItem item )
	{
		if ( item == null )
			return InventoryResult.ItemWasNull;

		// Check if it fits in main inventory
		if ( !MainInventory.CanItemFit( item, MaxCarryCapacity ) )
			return InventoryResult.NoSpaceAvailable;

		// Remove from equipment
		var removeResult = EquipmentInventory.TryRemove( item );
		if ( removeResult != InventoryResult.Success )
			return removeResult;

		// Add to main inventory
		return MainInventory.TryAdd( item );
	}

	#endregion

	#region Events

	private void OnMainInventoryChanged()
	{
		OnWeightChanged?.Invoke();
		CheckEncumbranceChange();
	}

	private void OnEquipmentInventoryChanged()
	{
		OnWeightChanged?.Invoke();
		CheckEncumbranceChange();
	}

	private void OnWalletChanged( int oldCredits, int newCredits )
	{
		// Could trigger UI updates or other systems
	}

	private void CheckEncumbranceChange()
	{
		var newState = EncumbranceState;
		if ( newState != _lastEncumbranceState )
		{
			_lastEncumbranceState = newState;
			OnEncumbranceChanged?.Invoke( newState );
		}
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Gets all items in the main inventory.
	/// </summary>
	public IEnumerable<BaseInventory.Entry> GetAllItems()
	{
		return MainInventory?.Entries ?? Array.Empty<BaseInventory.Entry>();
	}

	/// <summary>
	/// Gets all items in the main inventory of a specific category.
	/// </summary>
	public IEnumerable<EconomyItem> GetItemsByCategory( ItemCategory category )
	{
		foreach ( var entry in MainInventory?.Entries ?? Array.Empty<BaseInventory.Entry>() )
		{
			if ( entry.Item is EconomyItem ecoItem && ecoItem.ItemCategory == category )
				yield return ecoItem;
		}
	}

	/// <summary>
	/// Gets the total value of all items in inventory if sold.
	/// </summary>
	public int GetTotalSellValue()
	{
		int total = 0;
		foreach ( var entry in MainInventory?.Entries ?? Array.Empty<BaseInventory.Entry>() )
		{
			if ( entry.Item is EconomyItem ecoItem && ecoItem.IsSellable )
				total += ecoItem.CurrentSellPrice;
		}
		return total;
	}

	/// <summary>
	/// Sells all sellable items in inventory.
	/// </summary>
	/// <returns>Total credits received.</returns>
	public int SellAllItems()
	{
		if ( Wallet == null )
			return 0;

		int totalReceived = 0;
		var itemsToSell = new List<EconomyItem>();

		// Collect sellable items
		foreach ( var entry in MainInventory?.Entries ?? Array.Empty<BaseInventory.Entry>() )
		{
			if ( entry.Item is EconomyItem ecoItem && ecoItem.IsSellable )
				itemsToSell.Add( ecoItem );
		}

		// Sell each item
		foreach ( var item in itemsToSell )
		{
			int value = item.CurrentSellPrice;
			var result = MainInventory.TryRemove( item );
			if ( result == InventoryResult.Success )
			{
				Wallet.AddCredits( value );
				totalReceived += value;
			}
		}

		return totalReceived;
	}

	#endregion
}

/// <summary>
/// Player's main inventory implementation.
/// </summary>
public class PlayerInventory : BaseInventory
{
	public PlayerInventory( Guid id ) : base( id, 10, 6 ) { }

	// Can override validation methods here for player-specific rules
	protected override bool CanInsertItem( InventoryItem item )
	{
		// Example: Prevent quest items from being removed
		if ( item is EconomyItem ecoItem && ecoItem.ItemCategory == ItemCategory.Quest )
			return false;

		return base.CanInsertItem( item );
	}
}

/// <summary>
/// Player's equipment inventory implementation.
/// </summary>
public class EquipmentInventory : BaseInventory
{
	public EquipmentInventory( Guid id ) : base( id, 4, 4 ) { }

	// Equipment may have special rules
	protected override bool CanInsertItem( InventoryItem item )
	{
		// Example: Only allow equippable items
		// You could add an "IsEquippable" property to EconomyItemAsset

		return base.CanInsertItem( item );
	}
}