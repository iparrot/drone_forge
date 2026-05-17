using System;
using System.Collections.Generic;
using System.Linq;
using Conna.Inventory;
using Sandbox;

namespace DroneForge;

/// <summary>
/// Defines loot drop parameters for an item asset.
/// </summary>
public class LootEntry
{
	/// <summary>
	/// The item asset that can drop.
	/// </summary>
	public EconomyItemAsset Asset { get; set; }

	/// <summary>
	/// Relative weight for random selection (higher = more common).
	/// </summary>
	public float Weight { get; set; } = 1f;

	/// <summary>
	/// Minimum stack count when this item drops.
	/// </summary>
	public int MinStack { get; set; } = 1;

	/// <summary>
	/// Maximum stack count when this item drops.
	/// </summary>
	public int MaxStack { get; set; } = 1;

	/// <summary>
	/// Minimum quantity of this item to drop (for multi-drop).
	/// </summary>
	public int MinQuantity { get; set; } = 1;

	/// <summary>
	/// Maximum quantity of this item to drop.
	/// </summary>
	public int MaxQuantity { get; set; } = 1;
}

/// <summary>
/// Loot table containing entries that can be rolled for random drops.
/// </summary>
public class LootTable
{
	/// <summary>
	/// Unique identifier for this loot table.
	/// </summary>
	public Guid Id { get; } = Guid.NewGuid();

	/// <summary>
	/// Display name for this loot table.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// All possible loot entries.
	/// </summary>
	public List<LootEntry> Entries { get; } = new();

	/// <summary>
	/// Minimum number of items to drop per roll.
	/// </summary>
	public int MinDrops { get; set; } = 1;

	/// <summary>
	/// Maximum number of items to drop per roll.
	/// </summary>
	public int MaxDrops { get; set; } = 3;

	/// <summary>
	/// Whether to guarantee at least one item drops.
	/// </summary>
	public bool GuaranteeDrop { get; set; } = true;

	/// <summary>
	/// Chance (0-1) that no items drop at all (overrides GuaranteeDrop if fails).
	/// </summary>
	public float NoDropChance { get; set; } = 0f;

	/// <summary>
	/// Adds an entry to this loot table.
	/// </summary>
	public LootTable AddEntry( EconomyItemAsset asset, float weight = 1f, int minStack = 1, int maxStack = 1, int minQty = 1, int maxQty = 1 )
	{
		Entries.Add( new LootEntry
		{
			Asset = asset,
			Weight = weight,
			MinStack = minStack,
			MaxStack = maxStack,
			MinQuantity = minQty,
			MaxQuantity = maxQty
		} );
		return this;
	}

	/// <summary>
	/// Rolls the loot table and returns a list of items to drop.
	/// </summary>
	/// <param name="luckBonus">Optional luck modifier (0 = normal, positive = better drops).</param>
	/// <returns>List of generated EconomyItems.</returns>
	public List<EconomyItem> Roll( float luckBonus = 0f )
	{
		var results = new List<EconomyItem>();

		if ( Entries.Count == 0 )
			return results;

		// Check for no-drop
		if ( Random.Shared.Float() < NoDropChance )
			return results;

		// Determine number of drops
		int dropCount = Random.Shared.Int( MinDrops, MaxDrops + 1 );
		if ( GuaranteeDrop )
			dropCount = Math.Max( 1, dropCount );

		// Roll for each drop
		for ( int i = 0; i < dropCount; i++ )
		{
			var entry = RollSingleEntry( luckBonus );
			if ( entry != null && entry.Asset != null && entry.Asset.IsLootable )
			{
				int stackCount = Random.Shared.Int( entry.MinStack, entry.MaxStack + 1 );
				int quantity = Random.Shared.Int( entry.MinQuantity, entry.MaxQuantity + 1 );

				for ( int q = 0; q < quantity; q++ )
				{
					var item = EconomyItem.CreateFromAsset( entry.Asset, stackCount );
					results.Add( item );
				}
			}
		}

		return results;
	}

	/// <summary>
	/// Rolls for a single entry using weighted random selection.
	/// </summary>
	private LootEntry RollSingleEntry( float luckBonus )
	{
		// Filter to lootable items only
		var validEntries = Entries.Where( e => e.Asset?.IsLootable == true ).ToList();
		if ( validEntries.Count == 0 )
			return null;

		// Apply luck bonus to rarity weights
		float totalWeight = 0;
		foreach ( var entry in validEntries )
		{
			float weight = entry.Weight;
			// Luck bonus increases weight of rarer items
			if ( luckBonus > 0 )
			{
				weight *= 1f + (int)entry.Asset.Rarity * luckBonus * 0.25f;
			}
			entry.Weight = weight;
			totalWeight += weight;
		}

		// Weighted random selection
		float roll = Random.Shared.Float( 0, totalWeight );
		float cumulative = 0;

		foreach ( var entry in validEntries )
		{
			cumulative += entry.Weight;
			if ( roll <= cumulative )
				return entry;
		}

		return validEntries.Count > 0 ? validEntries[validEntries.Count - 1] : null;
	}
}

/// <summary>
/// A loot container in the world that players can interact with.
/// </summary>
public class LootContainer
{
	/// <summary>
	/// Unique identifier for this container.
	/// </summary>
	public Guid ContainerId { get; } = Guid.NewGuid();

	/// <summary>
	/// The inventory holding this container's items.
	/// </summary>
	public BaseInventory Inventory { get; private set; }

	/// <summary>
	/// Display name for this container (e.g., "Wooden Chest", "Skeleton Remains").
	/// </summary>
	public string ContainerName { get; set; } = "Loot Container";

	/// <summary>
	/// Grid dimensions for this container's inventory.
	/// </summary>
	public int GridWidth { get; set; } = 6;

	public int GridHeight { get; set; } = 4;

	/// <summary>
	/// The loot table used to generate contents.
	/// </summary>
	public LootTable LootTable { get; set; }

	/// <summary>
	/// Whether this container has been opened/looted.
	/// </summary>
	public bool IsLooted { get; private set; }

	/// <summary>
	/// Time until this container despawns (null = never).
	/// </summary>
	public float? DespawnTime { get; set; } = 300f; // 5 minutes default

	/// <summary>
	/// Time when this container was created. Set by Initialize().
	/// </summary>
	public float SpawnTime { get; private set; }

	/// <summary>
	/// Whether networking is enabled.
	/// </summary>
	public bool EnableNetworking { get; set; } = true;

	/// <summary>
	/// Event fired when container is looted.
	/// </summary>
	public event Action<LootContainer> OnLooted;

	/// <summary>
	/// Event fired when container should despawn.
	/// </summary>
	public event Action<LootContainer> OnDespawn;

	/// <summary>
	/// Creates and initializes the container's inventory.
	/// </summary>
	public void Initialize()
	{
		SpawnTime = Time.Now;
		Inventory = new SimpleInventory( ContainerId, GridWidth, GridHeight );

		if ( EnableNetworking )
			Inventory.Network.Enabled = true;

		// Generate loot if loot table is assigned
		if ( LootTable != null )
		{
			GenerateLoot();
		}
	}

	/// <summary>
	/// Generates loot items and places them in the inventory.
	/// </summary>
	public void GenerateLoot( float luckBonus = 0f )
	{
		if ( LootTable == null || Inventory == null )
			return;

		var items = LootTable.Roll( luckBonus );

		foreach ( var item in items )
		{
			// Try to place item in inventory
			var result = Inventory.TryAdd( item );
			if ( result != InventoryResult.Success )
			{
				Log.Warning( $"Failed to place loot item {item.DisplayName} in container: {result}" );
			}
		}
	}

	/// <summary>
	/// Called when a player opens this container.
	/// </summary>
	/// <param name="playerConnectionId">The player's connection ID.</param>
	public void OnPlayerOpen( Guid playerConnectionId )
	{
		// Subscribe player to inventory updates
		Inventory?.AddSubscriber( playerConnectionId );
	}

	/// <summary>
	/// Called when a player closes this container.
	/// </summary>
	/// <param name="playerConnectionId">The player's connection ID.</param>
	public void OnPlayerClose( Guid playerConnectionId )
	{
		// Unsubscribe player from inventory updates
		Inventory?.RemoveSubscriber( playerConnectionId );

		// Check if container is now empty
		if ( Inventory?.Entries.Count == 0 )
		{
			IsLooted = true;
			OnLooted?.Invoke( this );
		}
	}

	/// <summary>
	/// Updates the container, checking for despawn.
	/// </summary>
	public void Update()
	{
		if ( DespawnTime.HasValue )
		{
			if ( Time.Now - SpawnTime > DespawnTime.Value )
			{
				OnDespawn?.Invoke( this );
			}
		}
	}

	/// <summary>
	/// Cleans up the container.
	/// </summary>
	public void Dispose()
	{
		Inventory?.Dispose();
	}
}

/// <summary>
/// Manages loot containers in the world.
/// </summary>
[Title( "Loot System Manager" )]
[Category( "Inventory" )]
public sealed class LootSystemManager : Component
{
	/// <summary>
	/// All active loot containers.
	/// </summary>
	public List<LootContainer> ActiveContainers { get; } = new();

	/// <summary>
	/// Default loot tables that can be referenced by name.
	/// </summary>
	public Dictionary<string, LootTable> LootTables { get; } = new();

	/// <summary>
	/// Creates a new loot container at a position.
	/// </summary>
	/// <param name="lootTable">The loot table to use.</param>
	/// <param name="containerName">Display name for the container.</param>
	/// <param name="gridWidth">Inventory grid width.</param>
	/// <param name="gridHeight">Inventory grid height.</param>
	/// <param name="despawnTime">Time until despawn (null = never).</param>
	/// <returns>The created LootContainer.</returns>
	public LootContainer CreateContainer( LootTable lootTable, string containerName = "Loot Container",
		int gridWidth = 6, int gridHeight = 4, float? despawnTime = 300f )
	{
		var container = new LootContainer
		{
			ContainerName = containerName,
			GridWidth = gridWidth,
			GridHeight = gridHeight,
			LootTable = lootTable,
			DespawnTime = despawnTime
		};

		container.Initialize();
		container.OnDespawn += OnContainerDespawn;

		ActiveContainers.Add( container );
		return container;
	}

	/// <summary>
	/// Removes a container from the system.
	/// </summary>
	private void OnContainerDespawn( LootContainer container )
	{
		ActiveContainers.Remove( container );
		container.Dispose();
	}

	/// <summary>
	/// Updates all active containers.
	/// </summary>
	private void Update()
	{
		foreach ( var container in ActiveContainers.ToList() )
		{
			container.Update();
		}
	}

	/// <summary>
	/// Creates a standard loot table with weighted rarities.
	/// </summary>
	/// <param name="name">Name for this loot table.</param>
	/// <param name="commonWeight">Weight for common items.</param>
	/// <param name="uncommonWeight">Weight for uncommon items.</param>
	/// <param name="rareWeight">Weight for rare items.</param>
	/// <param name="epicWeight">Weight for epic items.</param>
	/// <param name="legendaryWeight">Weight for legendary items.</param>
	/// <returns>The created LootTable.</returns>
	public LootTable CreateStandardLootTable( string name,
		float commonWeight = 100f,
		float uncommonWeight = 50f,
		float rareWeight = 20f,
		float epicWeight = 5f,
		float legendaryWeight = 1f )
	{
		var table = new LootTable { Name = name };
		LootTables[name] = table;
		return table;
	}
}