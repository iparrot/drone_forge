using Sandbox;

namespace DroneForge;

/// <summary>
/// Extended item asset with economy properties (weight, prices) 
/// Inherits from Conna.Inventory's BaseItemAsset to leverage the Tetris Inventory system.
/// </summary>
[GameResource( "Economy Item Asset", "eco", "Item definition with weight and economy properties for the inventory system", Icon = "box" )]
public class EconomyItemAsset : Conna.Inventory.BaseItemAsset
{
	/// <summary>
	/// Weight of this item in kilograms. Used for carry capacity calculations.
	/// Default is 1kg. Set to 0 for weightless items.
	/// </summary>
	[Property, Group( "Economy" )]
	public float Weight { get; set; } = 1.0f;

	/// <summary>
	/// Default sell price in credits. This is what players receive when selling the item.
	/// Default is 1 credit as per GDD.
	/// </summary>
	[Property, Group( "Economy" )]
	public int SellPrice { get; set; } = 1;

	/// <summary>
	/// Default buy price in credits. This is what players pay to purchase the item.
	/// Default is 2 credits as per GDD (typically 2x sell price for merchant profit margin).
	/// </summary>
	[Property, Group( "Economy" )]
	public int BuyPrice { get; set; } = 2;

	/// <summary>
	/// Item rarity/tier. Used for loot generation and visual distinction.
	/// </summary>
	[Property, Group( "Economy" )]
	public ItemRarity Rarity { get; set; } = ItemRarity.Common;

	/// <summary>
	/// Icon sprite for UI display.
	/// </summary>
	[Property, Group( "Appearance" )]
	public Sprite Icon { get; set; }

	/// <summary>
	/// Detailed description of the item for tooltips.
	/// </summary>
	[Property, Group( "Appearance" ), TextArea]
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Category of item for filtering and organization.
	/// </summary>
	[Property, Group( "General" )]
	public ItemCategory ItemCategory { get; set; } = ItemCategory.Misc;

	/// <summary>
	/// Whether this item can be dropped on the ground when player dies or manually.
	/// </summary>
	[Property, Group( "Behavior" )]
	public bool IsDroppable { get; set; } = true;

	/// <summary>
	/// Whether this item can be sold to merchants.
	/// </summary>
	[Property, Group( "Behavior" )]
	public bool IsSellable { get; set; } = true;

	/// <summary>
	/// Whether this item can be purchased from merchants.
	/// </summary>
	[Property, Group( "Behavior" )]
	public bool IsBuyable { get; set; } = true;

	/// <summary>
	/// Whether this item can appear in loot containers.
	/// </summary>
	[Property, Group( "Behavior" )]
	public bool IsLootable { get; set; } = true;

	/// <summary>
	/// Maximum weight capacity this item provides if equipped (0 = no capacity bonus).
	/// </summary>
	[Property, Group( "Behavior" )]
	public float CapacityBonus { get; set; } = 0f;

	/// <summary>
	/// Gets the effective sell price for a stack of items.
	/// </summary>
	/// <param name="stackCount">Number of items in the stack.</param>
	/// <returns>Total credits received when selling.</returns>
	public int GetStackSellPrice( int stackCount = 1 )
	{
		return SellPrice * stackCount;
	}

	/// <summary>
	/// Gets the effective buy price for a stack of items.
	/// </summary>
	/// <param name="stackCount">Number of items in the stack.</param>
	/// <returns>Total credits required to purchase.</returns>
	public int GetStackBuyPrice( int stackCount = 1 )
	{
		return BuyPrice * stackCount;
	}

	/// <summary>
	/// Gets the weight for a stack of items.
	/// </summary>
	/// <param name="stackCount">Number of items in the stack.</param>
	/// <returns>Total weight in kilograms.</returns>
	public float GetStackWeight( int stackCount = 1 )
	{
		return Weight * stackCount;
	}
}

/// <summary>
/// Item rarity tiers for loot generation and visual distinction.
/// </summary>
public enum ItemRarity
{
	Common,      // White/Gray - Most frequent
	Uncommon,    // Green - Moderately frequent
	Rare,        // Blue - Less frequent
	Epic,        // Purple - Rare
	Legendary,   // Orange/Gold - Very rare
	Artifact     // Red - Extremely rare
}

/// <summary>
/// Item categories for organization and filtering.
/// </summary>
public enum ItemCategory
{
	Misc,        // Default category
	Weapon,      // Swords, axes, staves, etc.
	Armor,       // Helmets, chestpieces, boots, etc.
	Accessory,   // Rings, amulets, belts
	Consumable,  // Potions, food, scrolls
	Material,    // Crafting materials, ore, wood
	Quest,       // Quest items
	Currency,    // Special currency items
	Key,         // Keys for locks/chests
	Tool         // Torches, lockpicks, etc.
}