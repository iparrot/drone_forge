using System;
using System.Linq;
using Conna.Inventory;

namespace DroneForge;

/// <summary>
/// Utility class for calculating inventory weight and managing carry capacity.
/// Provides methods to compute total weight, check capacity, and determine if items can fit.
/// </summary>
public static class InventoryWeightCalculator
{
	/// <summary>
	/// Default base carry capacity in kilograms for a player without any equipment bonuses.
	/// </summary>
	public const float DefaultBaseCapacity = 50f;

	/// <summary>
	/// Weight threshold (as percentage of max capacity) at which player becomes overencumbered.
	/// </summary>
	public const float OverencumberedThreshold = 1.0f; // 100% = at or over max

	/// <summary>
	/// Weight threshold (as percentage) at which movement speed starts being reduced.
	/// </summary>
	public const float EncumbranceStartThreshold = 0.7f; // 70% of max

	/// <summary>
	/// Calculates the total weight of all items in an inventory.
	/// </summary>
	/// <param name="inventory">The inventory to calculate weight for.</param>
	/// <returns>Total weight in kilograms.</returns>
	public static float CalculateTotalWeight( this BaseInventory inventory )
	{
		if ( inventory == null )
			return 0f;

		float totalWeight = 0f;

		foreach ( var entry in inventory.Entries )
		{
			if ( entry.Item is EconomyItem ecoItem )
			{
				totalWeight += ecoItem.CurrentWeight;
			}
			else
			{
				// For non-economy items, use a default weight of 1kg per item
				totalWeight += 1f * entry.Item.StackCount;
			}
		}

		return totalWeight;
	}

	/// <summary>
	/// Calculates the total capacity bonus from equipped items in an inventory.
	/// Looks for items with CapacityBonus > 0 (e.g., backpacks, belts of carrying).
	/// </summary>
	/// <param name="inventory">The inventory to check for capacity bonuses.</param>
	/// <returns>Total capacity bonus in kilograms.</returns>
	public static float CalculateCapacityBonus( this BaseInventory inventory )
	{
		if ( inventory == null )
			return 0f;

		float totalBonus = 0f;

		foreach ( var entry in inventory.Entries )
		{
			if ( entry.Item is EconomyItem ecoItem )
			{
				// Assume equipped items provide their capacity bonus
				// You may want to add an "IsEquipped" check based on your game's equipment system
				totalBonus += ecoItem.CapacityBonus;
			}
		}

		return totalBonus;
	}

	/// <summary>
	/// Gets the maximum carry capacity for a player.
	/// </summary>
	/// <param name="equipmentInventory">Optional equipment inventory to check for capacity bonuses.</param>
	/// <param name="baseCapacity">Override for default base capacity.</param>
	/// <returns>Maximum carry capacity in kilograms.</returns>
	public static float GetMaxCarryCapacity( BaseInventory equipmentInventory = null, float baseCapacity = DefaultBaseCapacity )
	{
		float capacity = baseCapacity;

		if ( equipmentInventory != null )
		{
			capacity += equipmentInventory.CalculateCapacityBonus();
		}

		return capacity;
	}

	/// <summary>
	/// Checks if an item can fit in an inventory based on both weight and grid space.
	/// </summary>
	/// <param name="inventory">The target inventory.</param>
	/// <param name="item">The item to check.</param>
	/// <param name="maxCapacity">Maximum carry capacity.</param>
	/// <returns>True if the item can be added without exceeding limits.</returns>
	public static bool CanItemFit( this BaseInventory inventory, EconomyItem item, float maxCapacity )
	{
		if ( inventory == null || item == null )
			return false;

		// Check grid space
		float currentWeight = inventory.CalculateTotalWeight();
		float itemWeight = item.CurrentWeight;

		// Check weight
		if ( currentWeight + itemWeight > maxCapacity )
			return false;

		// Check if there's space in the grid (try to find placement)
		return inventory.CanPlaceItemAt( item, 0, 0, item );
	}

	/// <summary>
	/// Checks if an item can fit in an inventory based on grid space only (ignoring weight).
	/// Useful for bank storage or containers that don't have weight limits.
	/// </summary>
	/// <param name="inventory">The target inventory.</param>
	/// <param name="item">The item to check.</param>
	/// <returns>True if the item can be placed in the grid.</returns>
	public static bool CanItemFitGridOnly( this BaseInventory inventory, EconomyItem item )
	{
		if ( inventory == null || item == null )
			return false;

		return inventory.CanPlaceItemAt( item, 0, 0, item );
	}

	/// <summary>
	/// Gets the remaining weight capacity for a player.
	/// </summary>
	/// <param name="inventory">The player's inventory.</param>
	/// <param name="equipmentInventory">Optional equipment inventory for capacity bonuses.</param>
	/// <param name="baseCapacity">Override for default base capacity.</param>
	/// <returns>Remaining capacity in kilograms (can be negative if overencumbered).</returns>
	public static float GetRemainingCapacity( this BaseInventory inventory, BaseInventory equipmentInventory = null, float baseCapacity = DefaultBaseCapacity )
	{
		float maxCapacity = GetMaxCarryCapacity( equipmentInventory, baseCapacity );
		float currentWeight = inventory.CalculateTotalWeight();
		return maxCapacity - currentWeight;
	}

	/// <summary>
	/// Gets the encumbrance state based on current weight vs max capacity.
	/// </summary>
	/// <param name="currentWeight">Current carried weight.</param>
	/// <param name="maxCapacity">Maximum carry capacity.</param>
	/// <returns>Encumbrance state enum.</returns>
	public static EncumbranceState GetEncumbranceState( float currentWeight, float maxCapacity )
	{
		if ( maxCapacity <= 0 )
			return EncumbranceState.Overencumbered;

		float ratio = currentWeight / maxCapacity;

		if ( ratio >= OverencumberedThreshold )
			return EncumbranceState.Overencumbered;
		else if ( ratio >= EncumbranceStartThreshold )
			return EncumbranceState.Encumbered;
		else
			return EncumbranceState.Normal;
	}

	/// <summary>
	/// Gets the encumbrance state for a player's inventory.
	/// </summary>
	/// <param name="inventory">The player's inventory.</param>
	/// <param name="equipmentInventory">Optional equipment inventory for capacity bonuses.</param>
	/// <param name="baseCapacity">Override for default base capacity.</param>
	/// <returns>Encumbrance state enum.</returns>
	public static EncumbranceState GetEncumbranceState( this BaseInventory inventory, BaseInventory equipmentInventory = null, float baseCapacity = DefaultBaseCapacity )
	{
		float currentWeight = inventory.CalculateTotalWeight();
		float maxCapacity = GetMaxCarryCapacity( equipmentInventory, baseCapacity );
		return GetEncumbranceState( currentWeight, maxCapacity );
	}

	/// <summary>
	/// Calculates movement speed multiplier based on encumbrance.
	/// </summary>
	/// <param name="currentWeight">Current carried weight.</param>
	/// <param name="maxCapacity">Maximum carry capacity.</param>
	/// <param name="baseSpeed">Base movement speed (optional, for custom scaling).</param>
	/// <returns>Speed multiplier (0.0 to 1.0).</returns>
	public static float GetSpeedMultiplier( float currentWeight, float maxCapacity, float baseSpeed = 1.0f )
	{
		if ( maxCapacity <= 0 )
			return 0f; // Cannot move if no capacity

		float ratio = currentWeight / maxCapacity;

		if ( ratio >= OverencumberedThreshold )
			return 0f; // Cannot move when overencumbered

		if ( ratio >= EncumbranceStartThreshold )
		{
			// Linear reduction from 70% to 100% capacity
			float reduction = (ratio - EncumbranceStartThreshold) / (OverencumberedThreshold - EncumbranceStartThreshold);
			return baseSpeed * (1.0f - (0.5f * reduction)); // Reduce by up to 50%
		}

		return baseSpeed; // Normal speed
	}

	/// <summary>
	/// Calculates movement speed multiplier for a player's inventory.
	/// </summary>
	/// <param name="inventory">The player's inventory.</param>
	/// <param name="equipmentInventory">Optional equipment inventory for capacity bonuses.</param>
	/// <param name="baseCapacity">Override for default base capacity.</param>
	/// <param name="baseSpeed">Base movement speed.</param>
	/// <returns>Speed multiplier (0.0 to 1.0).</returns>
	public static float GetSpeedMultiplier( this BaseInventory inventory, BaseInventory equipmentInventory = null, float baseCapacity = DefaultBaseCapacity, float baseSpeed = 1.0f )
	{
		float currentWeight = inventory.CalculateTotalWeight();
		float maxCapacity = GetMaxCarryCapacity( equipmentInventory, baseCapacity );
		return GetSpeedMultiplier( currentWeight, maxCapacity, baseSpeed );
	}
}

/// <summary>
/// Encumbrance states for player movement.
/// </summary>
public enum EncumbranceState
{
	/// <summary>
	/// Normal movement speed, no penalties.
	/// </summary>
	Normal,

	/// <summary>
	/// Movement speed is reduced due to heavy load.
	/// </summary>
	Encumbered,

	/// <summary>
	/// Cannot move, over maximum capacity.
	/// </summary>
	Overencumbered
}