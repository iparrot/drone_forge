using System.Collections.Generic;
using System.Linq;
using Conna.Inventory;
using Sandbox;

namespace DroneForge;

/// <summary>
/// Place on a scene GameObject (workbench, NPC) to make it a shop the player can interact with.
/// Press the "use" action (E by default) within InteractRange to toggle the shop UI.
/// </summary>
[Title( "Shop Interactor" )]
[Category( "Inventory" )]
public sealed class ShopInteractor : Component
{
	[Property] public string ShopName { get; set; } = "Workbench";

	[Property] public List<EconomyItemAsset> ForSale { get; set; } = new();

	[Property, Range( 1, 999 )] public int StockPerItem { get; set; } = 10;

	[Property, Range( 16, 1024 )] public float InteractRange { get; set; } = 120f;

	public ShopInventory Shop { get; private set; }

	private ShopUI _ui;
	private PlayerInventoryManager _player;
	private PlayerWallet _wallet;

	protected override void OnStart()
	{
		Shop = new ShopInventory( ShopName );

		foreach ( var asset in ForSale )
		{
			if ( asset == null ) continue;
			Shop.AddItem( asset, stockCount: StockPerItem, maxStock: StockPerItem );
		}

		_ui = Components.Get<ShopUI>() ?? Components.Create<ShopUI>();
	}

	protected override void OnUpdate()
	{
		if ( _player == null || !_player.IsValid() )
		{
			_player = Scene.GetAllComponents<PlayerInventoryManager>().FirstOrDefault();
			if ( _player == null ) return;
			_wallet = _player.Components.Get<PlayerWallet>();
		}

		var inRange = Vector3.DistanceBetween( _player.WorldPosition, WorldPosition ) < InteractRange;

		if ( inRange && Input.Pressed( "use" ) )
		{
			_ui.Toggle( Shop, _player, _wallet );
		}
	}
}
