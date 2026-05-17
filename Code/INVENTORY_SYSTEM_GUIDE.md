# Drone Forge Inventory System - Complete Setup Guide

## Overview

This guide covers the complete Dark and Darker-style inventory system implementation for S&box, featuring:
- **Grid-based (Tetris-style) inventory** using the Conna.Inventory asset
- **Weight and encumbrance system** affecting movement
- **Economy system** with buy/sell mechanics and credit wallet
- **Shop/trader system** with fit-checking before purchase
- **Loot generation** with weighted rarity tables
- **Container/bank storage** system

## Prerequisites

### Required Assets (Already Installed)
1. **Tetris Inventory** (`conna.inventory`) - Grid-based inventory system
2. **PlayerWallet** (existing in project) - Credit management

### Project Structure
```
Code/
├── EconomyItemAsset.cs          # Item template definitions
├── EconomyItem.cs               # Runtime item instances
├── InventoryWeightCalculator.cs # Weight and capacity utilities
├── ShopSystem.cs                # Shop/trader with fit-checking
├── PlayerInventoryManager.cs    # Player inventory management
├── LootSystem.cs                # Loot tables and containers
├── PlayerWallet.cs              # Credit wallet (existing)
└── INVENTORY_SYSTEM_GUIDE.md    # This file
```

---

## Part 1: Creating Item Assets

### Step 1.1: Create Item Asset Files

In the S&box editor:

1. **Right-click in Assets folder** → **Create** → **Economy Item Asset**
2. Name your item (e.g., "Iron Sword", "Health Potion")
3. Configure the properties in the inspector:

#### Basic Properties
| Property | Description | Example |
|----------|-------------|---------|
| Display Name | Name shown in UI | "Iron Sword" |
| Description | Tooltip description | "A sturdy iron blade" |
| Category | Item type | Weapon, Armor, Consumable, etc. |
| Icon | Sprite for UI | [Select sprite] |

#### Grid Properties
| Property | Description | Example |
|----------|-------------|---------|
| Width | Grid cells wide | 1, 2, 3 |
| Height | Grid cells tall | 2, 3, 4 |
| Max Stack Size | Max items per stack | 1 (unique), 64 (consumables) |

#### Economy Properties
| Property | Description | Default | Example |
|----------|-------------|---------|---------|
| **Weight** | kg per item | 1.0 | 3.5kg for sword |
| **Sell Price** | Credits when sold | 1 | 15 credits |
| **Buy Price** | Credits to purchase | 2 | 30 credits |

#### Behavior Properties
| Property | Description | Default |
|----------|-------------|---------|
| Is Droppable | Can be dropped on death | ✅ |
| Is Sellable | Can be sold to merchants | ✅ |
| Is Buyable | Can be bought from merchants | ✅ |
| Is Lootable | Can appear in loot drops | ✅ |
| Capacity Bonus | Extra carry weight if equipped | 0 |

#### Rarity
| Rarity | Color | Loot Weight |
|--------|-------|-------------|
| Common | White | 100 |
| Uncommon | Green | 50 |
| Rare | Blue | 20 |
| Epic | Purple | 5 |
| Legendary | Orange | 1 |
| Artifact | Red | 0.1 |

### Step 1.2: Example Item Assets to Create

Create these example items to test the system:

#### 1. Iron Sword (Weapon)
```
Display Name: Iron Sword
Category: Weapon
Width: 2, Height: 1
Max Stack: 1
Weight: 3.5
Sell Price: 15
Buy Price: 30
Rarity: Common
```

#### 2. Health Potion (Consumable)
```
Display Name: Health Potion
Category: Consumable
Width: 1, Height: 1
Max Stack: 16
Weight: 0.5
Sell Price: 5
Buy Price: 10
Rarity: Common
```

#### 3. Backpack (Equipment)
```
Display Name: Traveler's Backpack
Category: Accessory
Width: 2, Height: 2
Max Stack: 1
Weight: 2.0
Sell Price: 50
Buy Price: 100
Capacity Bonus: 20 (adds 20kg carry capacity)
Rarity: Uncommon
```

---

## Part 2: Setting Up Player Inventory

### Step 2.1: Add Components to Player

1. **Select your player prefab/root object**
2. **Add Component** → `Player Inventory Manager`
3. **Add Component** → `Player Wallet` (if not already present)

### Step 2.2: Configure Player Inventory Manager

In the inspector:

| Property | Recommended Value | Description |
|----------|-------------------|-------------|
| Base Capacity | 50 | Starting carry weight (kg) |
| Main Inventory Width | 10 | Grid width (cells) |
| Main Inventory Height | 6 | Grid height (cells) |
| Equipment Inventory Width | 4 | Equipment grid width |
| Equipment Inventory Height | 4 | Equipment grid height |
| Enable Networking | ✅ | For multiplayer sync |

### Step 2.3: Configure Player Wallet

| Property | Recommended Value |
|----------|-------------------|
| Starting Credits | 100 |

---

## Part 3: Creating a Shop/Trader

### Step 3.1: Create Shop in Code

Create a new component or add to an existing NPC:

```csharp
using DroneForge;
using Conna.Inventory;

public class BlacksmithShop : Component
{
    public ShopInventory Shop { get; private set; }
    
    protected override void OnStart()
    {
        // Create shop
        Shop = new ShopInventory( "Blacksmith" );
        
        // Add items for sale (requires EconomyItemAsset references)
        // You'll need to assign these in the inspector or load via ResourceLibrary
        
        // Example: Add iron sword
        var ironSwordAsset = ResourceLibrary.Get<EconomyItemAsset>( "iron-sword" );
        if ( ironSwordAsset != null )
        {
            Shop.AddItem( ironSwordAsset, stockCount: 10 );
        }
        
        // Example: Add health potion
        var healthPotionAsset = ResourceLibrary.Get<EconomyItemAsset>( "health-potion" );
        if ( healthPotionAsset != null )
        {
            Shop.AddItem( healthPotionAsset, stockCount: 50, customPrice: 8 ); // Discount!
        }
    }
    
    // Called when player interacts with shop
    public void OnPlayerInteract( PlayerInventoryManager playerInv, PlayerWallet wallet )
    {
        // Try to buy an item (example: first item in shop)
        var shopItem = Shop.ForSale.FirstOrDefault();
        if ( shopItem != null )
        {
            var result = Shop.PurchaseItem( 
                playerInv.MainInventory, 
                wallet, 
                shopItem, 
                quantity: 1, 
                maxCarryCapacity: playerInv.MaxCarryCapacity 
            );
            
            if ( result.IsSuccess )
            {
                Log.Info( $"Purchased {result.PurchasedItem.DisplayName} for {result.CreditsSpent} credits" );
            }
            else
            {
                Log.Info( $"Purchase failed: {result.Message}" );
            }
        }
    }
}
```

### Step 3.2: Key Shop Features

#### Fit-Checking Before Purchase
The `CanPurchase` method checks ALL conditions BEFORE any transaction:
1. ✅ Item is buyable
2. ✅ Item is in stock
3. ✅ Player has enough credits
4. ✅ **Item weight won't exceed carry capacity**
5. ✅ **Item fits in inventory grid**

```csharp
// Check before attempting purchase
if ( Shop.CanPurchase( playerInv.MainInventory, wallet, shopItem, 1, playerInv.MaxCarryCapacity, out string reason ) )
{
    // Safe to purchase
    var result = Shop.PurchaseItem( ... );
}
else
{
    // Show reason to player
    Log.Info( $"Cannot purchase: {reason}" );
}
```

#### Selling Items
```csharp
// Sell item from player inventory
var itemToSell = playerInv.MainInventory.Entries.FirstOrDefault()?.Item as EconomyItem;
if ( itemToSell != null )
{
    var result = Shop.SellItem( playerInv.MainInventory, wallet, itemToSell );
    if ( result.IsSuccess )
    {
        Log.Info( $"Sold {itemToSell.DisplayName} for {result.CreditsSpent} credits" );
    }
}
```

---

## Part 4: Loot System Setup

### Step 4.1: Create Loot Tables

```csharp
using DroneForge;

public class LootSetup : Component
{
    public LootSystemManager LootManager { get; private set; }
    
    protected override void OnStart()
    {
        LootManager = Components.Get<LootSystemManager>( FindMode.EnabledInSelfAndDescendants )
            ?? GameObject.Components.Create<LootSystemManager>();
        
        // Create a standard loot table
        var chestLoot = LootManager.CreateStandardLootTable( "Chest Loot",
            commonWeight: 100,
            uncommonWeight: 40,
            rareWeight: 15,
            epicWeight: 3,
            legendaryWeight: 0.5f
        );
        
        // Add items to the loot table
        // Load assets from ResourceLibrary
        var commonItem = ResourceLibrary.Get<EconomyItemAsset>( "iron-sword" );
        var rareItem = ResourceLibrary.Get<EconomyItemAsset>( "health-potion" );
        
        if ( commonItem != null )
            chestLoot.AddEntry( commonItem, weight: 100, minStack: 1, maxStack: 1 );
            
        if ( rareItem != null )
            chestLoot.AddEntry( rareItem, weight: 50, minStack: 1, maxStack: 5 );
    }
    
    // Create a loot container
    public void SpawnLootChest( Vector3 position )
    {
        var chest = LootManager.CreateContainer(
            lootTable: LootManager.LootTables["Chest Loot"],
            containerName: "Wooden Chest",
            gridWidth: 6,
            gridHeight: 4,
            despawnTime: 300f // 5 minutes
        );
        
        Log.Info( $"Spawned loot chest at {position}" );
    }
}
```

### Step 4.2: Loot Table Configuration

| Property | Description | Example |
|----------|-------------|---------|
| Min Drops | Minimum items per roll | 1 |
| Max Drops | Maximum items per roll | 3 |
| Guarantee Drop | Always drop at least 1 item | ✅ |
| No Drop Chance | Chance (0-1) of no drop | 0.1 (10%) |

---

## Part 5: Weight and Encumbrance

### Step 5.1: Understanding Encumbrance

The system automatically calculates encumbrance based on weight:

| State | Weight % | Speed Multiplier |
|-------|----------|------------------|
| Normal | 0-70% | 100% (no penalty) |
| Encumbered | 70-100% | 75-100% (linear reduction) |
| Overencumbered | 100%+ | 0% (cannot move) |

### Step 5.2: Using Weight System

```csharp
// Get current weight info
var playerInv = Components.Get<PlayerInventoryManager>();

float currentWeight = playerInv.CurrentWeight;
float maxCapacity = playerInv.MaxCarryCapacity;
float remaining = playerInv.RemainingCapacity;
EncumbranceState state = playerInv.EncumbranceState;
float speedMult = playerInv.SpeedMultiplier;

// Check if item can be picked up
var item = /* get EconomyItem */;
if ( playerInv.CanFitItem( item ) )
{
    playerInv.PickupItem( item );
}
else
{
    Log.Info( "Cannot pick up - too heavy or no space" );
}
```

### Step 5.3: Capacity Bonuses

Items with `Capacity Bonus > 0` increase max carry weight when equipped:

```csharp
// Equip a backpack
var backpack = /* get backpack EconomyItem */;
playerInv.EquipItem( backpack );

// Now max capacity increased by backpack.CapacityBonus
float newMaxCapacity = playerInv.MaxCarryCapacity;
```

---

## Part 6: Integration Examples

### Example 1: Complete Shop Interaction

```csharp
public class ShopInteraction : Component, IInteractable
{
    [Property] public ShopInventory Shop { get; set; }
    
    public void OnInteract( SceneObject interactee, IInteractor interactor )
    {
        var player = interactor as Player;
        if ( player == null ) return;
        
        var playerInv = player.Components.Get<PlayerInventoryManager>();
        var wallet = player.Components.Get<PlayerWallet>();
        
        if ( playerInv == null || wallet == null ) return;
        
        // Open shop UI (you'll need to create UI)
        OpenShopUI( Shop, playerInv, wallet );
    }
    
    private void OpenShopUI( ShopInventory shop, PlayerInventoryManager playerInv, PlayerWallet wallet )
    {
        // Display shop items
        foreach ( var item in shop.GetItemsForSale() )
        {
            Log.Info( $"{item.Asset.DisplayName} - {item.BuyPrice} credits (Stock: {item.StockCount})" );
        }
        
        // Example: Buy first item
        var firstItem = shop.ForSale.FirstOrDefault();
        if ( firstItem != null )
        {
            var result = shop.PurchaseItem( playerInv.MainInventory, wallet, firstItem, 1, playerInv.MaxCarryCapacity );
            
            if ( result.IsSuccess )
            {
                Log.Info( $"✅ Purchased {result.PurchasedItem.DisplayName}" );
            }
            else
            {
                Log.Info( $"❌ Failed: {result.Message}" );
            }
        }
    }
}
```

### Example 2: Loot Drop on Enemy Death

```csharp
public class EnemyWithLoot : Component
{
    [Property] public LootTable DeathLootTable { get; set; }
    
    public void OnDeath()
    {
        if ( DeathLootTable == null ) return;
        
        // Roll for loot
        var items = DeathLootTable.Roll();
        
        // Create loot container at enemy position
        var lootManager = Scene.GetAllComponents<LootSystemManager>().FirstOrDefault();
        if ( lootManager != null )
        {
            var container = lootManager.CreateContainer(
                DeathLootTable,
                "Enemy Remains",
                4, 4,
                180f // 3 minutes
            );
            
            // Items are automatically generated and placed in container
            Log.Info( $"Dropped {container.Inventory.Entries.Count} items" );
        }
    }
}
```

### Example 3: Weight-Based Movement

```csharp
public class PlayerMovement : Component
{
    [Property] public PlayerInventoryManager Inventory { get; set; }
    [Property] public float BaseSpeed { get; set; } = 300f;
    
    protected override void Update()
    {
        if ( Inventory == null ) return;
        
        // Get speed multiplier based on encumbrance
        float speedMult = Inventory.SpeedMultiplier;
        float currentSpeed = BaseSpeed * speedMult;
        
        // Apply to movement
        ApplyMovement( currentSpeed );
        
        // Show encumbrance UI
        UpdateEncumbranceUI( Inventory.EncumbranceState, Inventory.CurrentWeight, Inventory.MaxCarryCapacity );
    }
}
```

---

## Part 7: Networking (Multiplayer)

### Step 7.1: Enable Networking

The system supports host-authoritative networking out of the box:

```csharp
// In PlayerInventoryManager, set:
EnableNetworking = true;

// Inventories will automatically sync to subscribed clients
inventory.Network.Enabled = true;
inventory.AddSubscriber( playerConnectionId );
```

### Step 7.2: Client-Server Operations

```csharp
// CLIENT: Request to purchase item (async)
if ( !shop.HasAuthority )
{
    var result = await shop.Network.PurchaseItem( ... );
}

// HOST: Direct operation
if ( shop.HasAuthority )
{
    var result = shop.PurchaseItem( ... );
}
```

---

## Part 8: Saving and Loading (Network Storage)

### Step 8.1: Setup Network Storage

1. Install **Network Storage by sboxcool** from Asset Browser
2. Configure API keys in `Editor/Network Storage/.env`
3. Create collections on sboxcool.com dashboard

### Step 8.2: Save Player Data

```csharp
using Sandbox;

public class PlayerDataSaver : Component
{
    public async Task SavePlayerData()
    {
        var inventory = Components.Get<PlayerInventoryManager>();
        var wallet = Components.Get<PlayerWallet>();
        
        // Serialize inventory
        var inventoryData = SerializeInventory( inventory.MainInventory );
        
        // Save to cloud
        await NetworkStorage.SaveDocument( "player-inventories", Game.SteamId.ToString(), new
        {
            credits = wallet.Credits,
            inventory = inventoryData,
            lastSaved = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        } );
    }
    
    private object SerializeInventory( BaseInventory inventory )
    {
        var data = new
        {
            width = inventory.Width,
            height = inventory.Height,
            items = inventory.Entries.Select( e => new
            {
                resourceId = (e.Item as EconomyItem)?.Resource?.ResourceId ?? 0,
                x = e.Slot.X,
                y = e.Slot.Y,
                stackCount = e.Item.StackCount
            }).ToList()
        };
        
        return data;
    }
}
```

---

## Troubleshooting

### Issue: "Item cannot fit" when it should
- **Check**: Weight capacity (`MaxCarryCapacity` vs `CurrentWeight`)
- **Check**: Grid space (item dimensions vs empty cells)
- **Solution**: Increase inventory size or reduce item weight

### Issue: Shop purchase fails silently
- **Check**: `CanPurchase` method returns detailed failure reason
- **Check**: Item has `IsBuyable = true`
- **Check**: Player has sufficient credits
- **Solution**: Use the `out string failureReason` parameter

### Issue: Items not appearing in loot
- **Check**: Item has `IsLootable = true`
- **Check**: Loot table has entries added
- **Check**: Loot table `Roll()` is being called
- **Solution**: Debug log the rolled items count

### Issue: Weight not calculating correctly
- **Check**: Items are `EconomyItem` type (not base `InventoryItem`)
- **Check**: `CalculateTotalWeight()` extension is being used
- **Solution**: Ensure all items inherit from `EconomyItem`

---

## Next Steps

1. **Create Item Assets**: Use the S&box editor to create Economy Item Assets
2. **Set Up Player**: Add `PlayerInventoryManager` and `PlayerWallet` components
3. **Build Shops**: Create shop interactions using `ShopInventory`
4. **Add Loot**: Set up loot tables and containers with `LootSystemManager`
5. **Design UI**: Create inventory and shop UI panels (using the Tetris Inventory UI examples)
6. **Test**: Spawn items, test purchases, verify weight system
7. **Deploy**: Enable networking for multiplayer, set up cloud saves

---

## Additional Resources

- **Tetris Inventory Documentation**: `Libraries/conna.inventory/README.md`
- **Network Storage Guide**: `Libraries/sboxcool.network-storage/README.md`
- **S&box Component API**: https://sbox.game/api

For questions or issues, refer to the inline XML documentation in each code file.