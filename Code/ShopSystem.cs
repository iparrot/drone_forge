using System;
using System.Collections.Generic;
using System.Linq;
using Conna.Inventory;
using Sandbox;

namespace DroneForge;

/// <summary>
/// Represents an item available for purchase in a shop.
/// </summary>
public class ShopItem
{
	/// <summary>
	/// The item asset template being sold.
	/// </summary>
	public EconomyItemAsset Asset { get; set; }

	/// <summary>
	/// Current stack count available for sale (0 = out of stock).
	/// </summary>
	public int StockCount { get; set; }

	/// <summary>
	/// Override buy price (null = use asset's default BuyPrice).
	/// Useful for sales, discounts, or premium pricing.
	/// </summary>
	public int? CustomBuyPrice { get; set; }

	/// <summary>
	/// Maximum stock count (0 = unlimited stock).
	/// </summary>
	public int MaxStock { get; set; }

	/// <summary>
	/// Gets the effective buy price for this shop item.
	/// </summary>
	public int BuyPrice => CustomBuyPrice ?? Asset?.BuyPrice ?? 0;

	/// <summary>
	/// Whether this item is currently in stock.
	/// </summary>
	public bool IsInStock => MaxStock == 0 || StockCount > 0;

	/// <summary>
	/// Gets the total value of remaining stock.
	/// </summary>
	public int TotalStockValue => StockCount * BuyPrice;

	public ShopItem( EconomyItemAsset asset, int stockCount = 0, int? customPrice = null, int maxStock = 0 )
	{
		Asset = asset;
		StockCount = stockCount;
		CustomBuyPrice = customPrice;
		MaxStock = maxStock;
	}
}

/// <summary>
/// Result of a shop transaction.
/// </summary>
public enum ShopTransactionResult
{
	Success,
	InsufficientFunds,
	InsufficientSpace,
	InsufficientWeightCapacity,
	ItemNotInStock,
	ItemNotFound,
	InvalidAmount,
	ItemNotBuyable,
	InventoryFull,
	UnknownError
}

/// <summary>
/// Shop transaction result with details.
/// </summary>
public class ShopTransactionResultInfo
{
	public ShopTransactionResult Result { get; set; }
	public string Message { get; set; }
	public int CreditsSpent { get; set; }
	public EconomyItem PurchasedItem { get; set; }

	public bool IsSuccess => Result == ShopTransactionResult.Success;

	public static ShopTransactionResultInfo Success( EconomyItem item, int creditsSpent )
		=> new() { Result = ShopTransactionResult.Success, Message = "Purchase successful", CreditsSpent = creditsSpent, PurchasedItem = item };

	public static ShopTransactionResultInfo Failure( ShopTransactionResult result, string message )
		=> new() { Result = result, Message = message };
}

/// <summary>
/// A shop/trader that players can buy from and sell to.
/// Manages available items, stock levels, and transactions with fit-checking.
/// </summary>
public class ShopInventory
{
	/// <summary>
	/// Unique identifier for this shop.
	/// </summary>
	public Guid ShopId { get; } = Guid.NewGuid();

	/// <summary>
	/// Display name of the shop (e.g., "Blacksmith", "General Store").
	/// </summary>
	public string ShopName { get; set; }

	/// <summary>
	/// List of items available for purchase.
	/// </summary>
	public List<ShopItem> ForSale { get; } = new();

	/// <summary>
	/// Inventory containing items players have sold to this shop.
	/// Players can buy these back (optional feature).
	/// </summary>
	public BaseInventory BuybackInventory { get; }

	/// <summary>
	/// Maximum number of buyback items to keep (oldest are removed).
	/// </summary>
	public int MaxBuybackSlots { get; set; } = 20;

	/// <summary>
	/// Price multiplier for selling (e.g., 0.5 = sell for 50% of item's sell price).
	/// </summary>
	public float SellPriceMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// Event fired when a transaction completes.
	/// </summary>
	public event Action<ShopTransactionResultInfo> OnTransactionComplete;

	/// <summary>
	/// Event fired when shop stock changes.
	/// </summary>
	public event Action OnStockChanged;

	/// <summary>
	/// Creates a new shop inventory.
	/// </summary>
	/// <param name="shopName">Display name for the shop.</param>
	/// <param name="buybackGridWidth">Width of buyback inventory grid.</param>
	/// <param name="buybackGridHeight">Height of buyback inventory grid.</param>
	public ShopInventory( string shopName, int buybackGridWidth = 8, int buybackGridHeight = 4 )
	{
		ShopName = shopName;
		BuybackInventory = new SimpleInventory( ShopId, buybackGridWidth, buybackGridHeight );
	}

	/// <summary>
	/// Adds an item to the shop's available stock.
	/// </summary>
	/// <param name="asset">The item asset to sell.</param>
	/// <param name="stockCount">Initial stock (0 = unlimited).</param>
	/// <param name="customPrice">Optional custom price override.</param>
	/// <param name="maxStock">Max stock limit (0 = unlimited).</param>
	/// <returns>The created ShopItem.</returns>
	public ShopItem AddItem( EconomyItemAsset asset, int stockCount = 0, int? customPrice = null, int maxStock = 0 )
	{
		var shopItem = new ShopItem( asset, stockCount, customPrice, maxStock );
		ForSale.Add( shopItem );
		OnStockChanged?.Invoke();
		return shopItem;
	}

	/// <summary>
	/// Removes an item from the shop's available stock.
	/// </summary>
	/// <param name="asset">The item asset to remove.</param>
	public void RemoveItem( EconomyItemAsset asset )
	{
		var item = ForSale.FirstOrDefault( s => s.Asset?.ResourceId == asset?.ResourceId );
		if ( item != null )
		{
			ForSale.Remove( item );
			OnStockChanged?.Invoke();
		}
	}

	/// <summary>
	/// Finds a shop item by asset.
	/// </summary>
	public ShopItem FindShopItem( EconomyItemAsset asset )
	{
		return ForSale.FirstOrDefault( s => s.Asset?.ResourceId == asset?.ResourceId );
	}

	/// <summary>
	/// Checks if a player can purchase an item from this shop.
	/// This performs ALL necessary checks: funds, weight capacity, and grid space.
	/// </summary>
	/// <param name="playerInventory">Player's main inventory.</param>
	/// <param name="playerWallet">Player's wallet component.</param>
	/// <param name="shopItem">The shop item to purchase.</param>
	/// <param name="quantity">Number of items to purchase.</param>
	/// <param name="maxCarryCapacity">Player's maximum carry capacity.</param>
	/// <param name="failureReason">Output: reason for failure if check fails.</param>
	/// <returns>True if purchase is possible.</returns>
	public bool CanPurchase( BaseInventory playerInventory, PlayerWallet playerWallet, ShopItem shopItem, int quantity, float maxCarryCapacity, out string failureReason )
	{
		failureReason = string.Empty;

		// Validate inputs
		if ( shopItem == null || shopItem.Asset == null )
		{
			failureReason = "Item not found in shop.";
			return false;
		}

		if ( quantity <= 0 )
		{
			failureReason = "Invalid quantity.";
			return false;
		}

		// Check if item is buyable
		if ( !shopItem.Asset.IsBuyable )
		{
			failureReason = "This item cannot be purchased.";
			return false;
		}

		// Check stock
		if ( !shopItem.IsInStock )
		{
			failureReason = "Item is out of stock.";
			return false;
		}

		if ( shopItem.MaxStock > 0 && quantity > shopItem.StockCount )
		{
			failureReason = $"Only {shopItem.StockCount} in stock.";
			return false;
		}

		// Check funds
		int totalCost = shopItem.BuyPrice * quantity;
		if ( playerWallet.Credits < totalCost )
		{
			failureReason = $"Insufficient credits. Need {totalCost}, have {playerWallet.Credits}.";
			return false;
		}

		// Check weight capacity BEFORE spending credits
		// This is the key requirement: verify item fits before transaction
		var itemToBuy = EconomyItem.CreateFromAsset( shopItem.Asset, quantity );
		float currentWeight = playerInventory.CalculateTotalWeight();
		float itemWeight = itemToBuy.CurrentWeight;

		if ( currentWeight + itemWeight > maxCarryCapacity )
		{
			failureReason = $"Too heavy. Adding this would exceed carry capacity ({currentWeight + itemWeight:F1}kg / {maxCarryCapacity:F1}kg).";
			return false;
		}

		// Check grid space
		if ( !playerInventory.CanItemFitGridOnly( itemToBuy ) )
		{
			failureReason = "No space in inventory for this item.";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Attempts to purchase an item from the shop.
	/// Performs all checks (funds, weight, space) before completing the transaction.
	/// </summary>
	/// <param name="playerInventory">Player's main inventory.</param>
	/// <param name="playerWallet">Player's wallet component.</param>
	/// <param name="shopItem">The shop item to purchase.</param>
	/// <param name="quantity">Number of items to purchase.</param>
	/// <param name="maxCarryCapacity">Player's maximum carry capacity.</param>
	/// <returns>Transaction result with details.</returns>
	public ShopTransactionResultInfo PurchaseItem( BaseInventory playerInventory, PlayerWallet playerWallet, ShopItem shopItem, int quantity, float maxCarryCapacity )
	{
		// Validate all conditions BEFORE any state changes
		if ( !CanPurchase( playerInventory, playerWallet, shopItem, quantity, maxCarryCapacity, out string failureReason ) )
		{
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.InsufficientFunds, failureReason );
		}

		// Create the item to purchase
		var purchasedItem = EconomyItem.CreateFromAsset( shopItem.Asset, quantity );

		// Calculate cost
		int totalCost = shopItem.BuyPrice * quantity;

		// Deduct credits
		if ( !playerWallet.TrySpend( totalCost ) )
		{
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.InsufficientFunds, "Failed to deduct credits." );
		}

		// Add item to player inventory
		var addResult = playerInventory.TryAdd( purchasedItem );
		if ( addResult != InventoryResult.Success )
		{
			// Refund credits since item couldn't be added
			playerWallet.AddCredits( totalCost );
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.InventoryFull, $"Failed to add item to inventory: {addResult}" );
		}

		// Update stock
		if ( shopItem.MaxStock > 0 )
		{
			shopItem.StockCount = Math.Max( 0, shopItem.StockCount - quantity );
			OnStockChanged?.Invoke();
		}

		var result = ShopTransactionResultInfo.Success( purchasedItem, totalCost );
		OnTransactionComplete?.Invoke( result );
		return result;
	}

	/// <summary>
	/// Attempts to sell an item from player's inventory to the shop.
	/// </summary>
	/// <param name="playerInventory">Player's main inventory.</param>
	/// <param name="playerWallet">Player's wallet component.</param>
	/// <param name="item">The item to sell.</param>
	/// <returns>Transaction result with details.</returns>
	public ShopTransactionResultInfo SellItem( BaseInventory playerInventory, PlayerWallet playerWallet, EconomyItem item )
	{
		if ( item == null )
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.ItemNotFound, "No item to sell." );

		if ( !item.IsSellable )
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.ItemNotBuyable, "This item cannot be sold." );

		if ( !playerInventory.Contains( item ) )
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.ItemNotInStock, "Item not in your inventory." );

		// Calculate sell value
		int sellValue = (int)(item.CurrentSellPrice * SellPriceMultiplier);

		// Remove item from player inventory
		var removeResult = playerInventory.TryRemove( item );
		if ( removeResult != InventoryResult.Success )
		{
			return ShopTransactionResultInfo.Failure( ShopTransactionResult.UnknownError, $"Failed to remove item: {removeResult}" );
		}

		// Add credits to player
		playerWallet.AddCredits( sellValue );

		// Add to buyback inventory (optional - for buyback feature)
		AddToBuyback( item );

		var result = ShopTransactionResultInfo.Success( item, sellValue );
		OnTransactionComplete?.Invoke( result );
		return result;
	}

	/// <summary>
	/// Adds an item to the buyback inventory.
	/// </summary>
	private void AddToBuyback( EconomyItem item )
	{
		// Try to add to buyback inventory
		var result = BuybackInventory.TryAdd( item );
		if ( result != InventoryResult.Success )
		{
			// If buyback is full, remove oldest item
			if ( BuybackInventory.Entries.Count >= MaxBuybackSlots )
			{
				var oldest = BuybackInventory.Entries.FirstOrDefault();
				if ( oldest.Item is EconomyItem oldItem )
				{
					BuybackInventory.TryRemove( oldItem );
				}
			}

			// Try again
			BuybackInventory.TryAdd( item );
		}
	}

	/// <summary>
	/// Gets all items for sale, optionally filtered by category.
	/// </summary>
	public IEnumerable<ShopItem> GetItemsForSale( ItemCategory? category = null )
	{
		var items = ForSale.AsEnumerable();
		if ( category.HasValue )
		{
			items = items.Where( i => i.Asset.ItemCategory == category.Value );
		}
		return items;
	}

	/// <summary>
	/// Clears all stock from the shop.
	/// </summary>
	public void ClearStock()
	{
		ForSale.Clear();
		OnStockChanged?.Invoke();
	}
}

/// <summary>
/// Simple inventory implementation for shops and containers.
/// </summary>
public class SimpleInventory : BaseInventory
{
	public SimpleInventory( Guid id, int width, int height ) : base( id, width, height ) { }
}