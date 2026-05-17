using System.Collections.Generic;
using Conna.Inventory;
using Sandbox;

namespace DroneForge;

/// <summary>
/// Runtime inventory item instance based on EconomyItemAsset.
/// This is the actual item that gets placed in inventories, with all economy properties
/// inherited from the asset template.
/// </summary>
public class EconomyItem : GameResourceItem<EconomyItemAsset>
{
	/// <summary>
	/// Override to use the asset's weight property.
	/// Derived from StackCount (which is networked), so updates automatically on clients.
	/// </summary>
	public float CurrentWeight => Resource?.GetStackWeight( StackCount ) ?? 0f;

	/// <summary>
	/// Override to use the asset's sell price.
	/// </summary>
	public int CurrentSellPrice => Resource?.GetStackSellPrice( StackCount ) ?? 0;

	/// <summary>
	/// Override to use the asset's buy price.
	/// </summary>
	public int CurrentBuyPrice => Resource?.GetStackBuyPrice( StackCount ) ?? 0;

	/// <summary>
	/// Override to use the asset's display name, with stack count if > 1.
	/// </summary>
	public override string DisplayName
	{
		get
		{
			var name = Resource?.DisplayName ?? base.DisplayName;
			if ( StackCount > 1 )
				return $"{name} x{StackCount}";
			return name;
		}
	}

	/// <summary>
	/// Override to use the asset's category.
	/// </summary>
	public override string Category => Resource?.ItemCategory.ToString() ?? base.Category;

	/// <summary>
	/// Override to use the asset's max stack size.
	/// </summary>
	public override int MaxStackSize => Resource?.MaxStackSize ?? base.MaxStackSize;

	/// <summary>
	/// Override to use the asset's dimensions.
	/// </summary>
	public override int Width => Resource?.Width ?? base.Width;
	public override int Height => Resource?.Height ?? base.Height;

	/// <summary>
	/// Get the item's rarity for visual effects and loot filtering.
	/// </summary>
	public ItemRarity Rarity => Resource?.Rarity ?? ItemRarity.Common;

	/// <summary>
	/// Whether this item can be dropped.
	/// </summary>
	public bool IsDroppable => Resource?.IsDroppable ?? true;

	/// <summary>
	/// Whether this item can be sold.
	/// </summary>
	public bool IsSellable => Resource?.IsSellable ?? true;

	/// <summary>
	/// Whether this item can be bought from merchants.
	/// </summary>
	public bool IsBuyable => Resource?.IsBuyable ?? true;

	/// <summary>
	/// Whether this item can appear in loot.
	/// </summary>
	public bool IsLootable => Resource?.IsLootable ?? true;

	/// <summary>
	/// Capacity bonus if this item is equipped (e.g., backpack).
	/// </summary>
	public float CapacityBonus => Resource?.CapacityBonus ?? 0f;

	/// <summary>
	/// Gets the weight of a single unit of this item.
	/// </summary>
	public float UnitWeight => Resource?.Weight ?? 1f;

	/// <summary>
	/// Gets the sell price of a single unit.
	/// </summary>
	public int UnitSellPrice => Resource?.SellPrice ?? 1;

	/// <summary>
	/// Gets the buy price of a single unit.
	/// </summary>
	public int UnitBuyPrice => Resource?.BuyPrice ?? 2;

	/// <summary>
	/// Gets the item's description for tooltips.
	/// </summary>
	public string Description => Resource?.Description ?? string.Empty;

	/// <summary>
	/// Gets the item's icon for UI display.
	/// </summary>
	public Sprite Icon => Resource?.Icon;

	/// <summary>
	/// Gets the item's category enum.
	/// </summary>
	public ItemCategory ItemCategory => Resource?.ItemCategory ?? ItemCategory.Misc;

	/// <summary>
	/// Create an EconomyItem from an EconomyItemAsset.
	/// </summary>
	/// <param name="asset">The item asset template.</param>
	/// <returns>A new EconomyItem instance.</returns>
	public static EconomyItem CreateFromAsset( EconomyItemAsset asset )
	{
		var item = new EconomyItem();
		item.LoadFromResource( asset );
		return item;
	}

	/// <summary>
	/// Create an EconomyItem from an EconomyItemAsset with a specific stack count.
	/// </summary>
	/// <param name="asset">The item asset template.</param>
	/// <param name="stackCount">Initial stack count.</param>
	/// <returns>A new EconomyItem instance.</returns>
	public static EconomyItem CreateFromAsset( EconomyItemAsset asset, int stackCount )
	{
		var item = CreateFromAsset( asset );
		item.StackCount = stackCount;
		return item;
	}

	/// <summary>
	/// Override to include ResourceId in serialization (handled by base) plus any custom properties.
	/// </summary>
	public override void Serialize( Dictionary<string, object> data )
	{
		base.Serialize( data );
		// Base class handles ResourceId, but we can add custom networked properties here if needed
	}

	/// <summary>
	/// Override to handle custom properties during deserialization.
	/// </summary>
	public override void Deserialize( Dictionary<string, object> data )
	{
		base.Deserialize( data );
		// Base class handles ResourceId restoration
	}

	/// <summary>
	/// Override to ensure items can only stack if they're the same asset and have compatible properties.
	/// </summary>
	public override bool CanStackWith( InventoryItem other )
	{
		if ( !base.CanStackWith( other ) )
			return false;

		if ( other is not EconomyItem ecoOther )
			return false;

		// Can only stack if they reference the same asset
		return ecoOther.Resource?.ResourceId == Resource?.ResourceId;
	}

	/// <summary>
	/// Override to copy resource reference when creating stack clones.
	/// </summary>
	public override InventoryItem CreateStackClone( int stackCount )
	{
		var clone = new EconomyItem();
		clone.LoadFromResource( Resource );
		clone.StackCount = stackCount;
		return clone;
	}

	/// <summary>
	/// Called when this item is added to an inventory.
	/// </summary>
	public override void OnAdded( BaseInventory inventory )
	{
		base.OnAdded( inventory );
		// Could trigger weight recalculation here if needed
	}

	/// <summary>
	/// Called when this item is removed from an inventory.
	/// </summary>
	public override void OnRemoved( BaseInventory inventory )
	{
		base.OnRemoved( inventory );
		// Could trigger weight recalculation here if needed
	}
}