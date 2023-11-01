using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cart : Unit
{
    public class Stop
    {
        public Stock stock;
        public Item.Type itemType;
        public int lastQuantity, totalQuantity;
    }

    public int itemQuantity;
    public Item.Type itemType;
    public Stock destination;
    public bool back;
    public const int frameCount = 8;
    public Stock boss { get { return building as Stock; } }
    readonly GameObject[] frames = new GameObject[frameCount];
    public GameObject cargoSprite;
    public SpriteRenderer onMap;
    public List<Stop> stops = new ();
    public int stop;
    public bool updateNeeded;

    [Obsolete( "Compatibility with old files", true )]
    List<(Stock, Item.Type)> schedule
    {
        set
        {
            foreach ( var stop in value )
                stops.Add( new Stop { stock = stop.Item1, itemType = stop.Item2 } );
        }
    }

    new public static Cart Create()
    {
        return new GameObject().AddComponent<Cart>();
    }

    public void DeliverItems( Stock destination )
    {
        this.destination = destination;

        destination.itemData[(int)itemType].onWay += itemQuantity;
        ScheduleWalkToFlag( destination.flag, true );
        ScheduleWalkToNeighbour( destination.node );

        var task = new DeliverStackTask();
        task.Setup( this, destination );
        ScheduleTask( task );
    }

    public void PickupItems( Item.Type itemType, Stock source, Stock destination )
    {
        int typeIndex = (int)itemType;
        itemQuantity = Math.Min( Constants.Unit.cartCapacity, source.itemData[typeIndex].content );
        int spaceLeft = destination.itemData[typeIndex].spaceLeftForCart;
        if ( itemQuantity > spaceLeft )
            itemQuantity = spaceLeft;
        if ( itemQuantity < 0 )
            itemQuantity = 0;
        source.itemData[typeIndex].content -= itemQuantity;
        if ( stop < stops.Count )
        {
            var currentStop = stops[stop];
            currentStop.lastQuantity = itemQuantity;
            currentStop.totalQuantity += itemQuantity;
        }
        source.contentChange.Trigger();
        this.itemType = itemType;
        ScheduleWalkToNeighbour( source.flag.node );
        DeliverItems( destination );

        SetActive( true );
        UpdateLook();
    }

    public void TransferItems( Item.Type itemType, Stock source, Stock destination )
    {
        Log( $"Cart is going to pick up {itemType} at {source} and deliver to {destination}" );
        if ( node == source.node )
        {
            PickupItems( itemType, source, destination );
            return;
        }
        
        this.itemType = itemType;
        itemQuantity = 0;
        this.destination = destination;

        ScheduleGetToFlag( 1 );
        ScheduleWalkToFlag( source.flag );
        ScheduleWalkToNeighbour( source.node );
        ScheduleCall( source );
    }

    public override void Reset()
    {
        base.Reset();
        itemQuantity = 0;
    }

    new public void Start()
    {
        base.Start();

        for ( int i = 0; i < frameCount; i++ )
        {
            frames[i] = World.FindChildRecursive( body.transform, $"frame{i}" )?.gameObject;
            assert.IsNotNull( frames[i] );
        }

        onMap = new GameObject( "Cart content on map" ).AddComponent<SpriteRenderer>();
        onMap.transform.SetParent( transform, false );
        onMap.transform.localPosition = Vector3.up * 6;
        onMap.transform.localRotation = Quaternion.Euler( 90, 0, 0 );
        onMap.transform.localScale = Vector3.one * 0.3f;
        onMap.material.renderQueue = 4003;
        onMap.gameObject.layer = Constants.World.layerIndexMap;

        UpdateLook();
    }

    public override void GameLogicUpdate( UpdateStage stage )
    {
        if ( itemQuantity > 0 && destination == null )
        {
            destination = null; // Real null, not the unity style fake one
            ResetTasks();
            DeliverItems( boss );
        }
        base.GameLogicUpdate( stage );
    }

    override public void FindTask()
    {
        if ( DoSchedule() )
        {
            SetActive( true );
            return;
        }

        if ( node == boss.node )
        {
            SetActive( false );
            return;
        }

        ScheduleGetToFlag();
        ScheduleWalkToFlag( boss.flag );
        ScheduleWalkToNeighbour( boss.node );
    }

    public void UpdateLook()
    {
        if ( frames[0] == null )	// This is true if start was not yet called (rare case)
            return;

        if ( itemQuantity > 0 )
        {
            for ( int i = 0; i < Math.Min( frameCount, itemQuantity ); i++ )
            {
                var itemBody = Instantiate( Item.looks.GetMediaData( itemType ) );
                itemBody.transform.rotation *= Quaternion.Euler( -Constants.Item.yawAtFlag[(int)itemType], 0, 0 );
                itemBody.transform.SetParent( frames[i].transform, false );
            }
            cargoSprite = new GameObject( "Cargo items" );
            cargoSprite.transform.SetParent( flat.transform, false );
            cargoSprite.transform.localScale = Vector3.one * 0.2f;
            cargoSprite.AddComponent<SpriteRenderer>().Prepare( Item.sprites.GetMediaData( itemType ), Vector3.zero, false, (int)( Constants.Unit.itemsInHandsSpriteVerticalOffset * 100 - 1 ) );
        }
        else
        {
            foreach ( var f in frames )
                if ( f.transform.childCount > 0 )
                    Eradicate( f.transform.GetChild( 0 ).gameObject );
            Eradicate( cargoSprite );
        }
        if ( taskQueue.Count == 0 && walkTo == null )
            SetActive( false );
        if ( itemQuantity > 0 )
            onMap.sprite = Item.sprites.GetMediaData( itemType );
        else
            onMap.sprite = null;
    }

    public override Node LeaveExclusivity()
    {
        var result = base.LeaveExclusivity();
        if ( result )
            road = null;
        return result;
    }

    public bool DoSchedule()
    {
        if ( stops.Count < 2 )
            return false;

        stop %= stops.Count;
        TransferItems( stops[stop].itemType, stops[stop].stock, stops[(stop+1)%stops.Count].stock );
        updateNeeded = true;
        return true;
    }

    public override void Validate( bool chain )
    {
        base.Validate( chain );
        assert.IsTrue( type == Unit.Type.cart || type == Unit.Type.unemployed );
        if ( building )		// Can be null, if the user removed the stock
            assert.IsTrue( building is Stock );
        if ( road && exclusiveMode )
        {
            int index = IndexOnRoad();
            assert.IsTrue( index >= 0 );
            assert.AreEqual( road.haulerAtNodes[index], this );
        }
    }

    new void Update()
    {
        onMap.transform.rotation = Quaternion.Euler( 90, (float)( eye.direction / Math.PI * 180 ), 0 );
        if ( cargoSprite )
            cargoSprite.transform.localPosition = ( facingRight ? Vector3.right : Vector3.left ) * Constants.Unit.itemsInHandsSpriteHorizontalOffset + Vector3.up * Constants.Unit.itemsInHandsSpriteVerticalOffset;
        base.Update();
    }
}

public class DeliverStackTask : Unit.Task
{
    public Stock stock;

    public void Setup( Unit boss, Stock stock )
    {
        base.Setup( boss );
        this.stock = stock;
    }

    public override void Cancel()
    {
        Cart cart = boss as Cart;
        if ( stock )
            stock.itemData[(int)cart.itemType].onWay -= cart.itemQuantity;
        base.Cancel();
    }

    public override bool ExecuteFrame()
    {
        if ( stock == null )
            return ResetBossTasks();

        Cart cart = boss as Cart;
        boss.assert.IsNotNull( cart );
        if ( cart.itemQuantity > 0 )
        {
            cart.itemsDelivered += cart.itemQuantity;
            stock.itemData[(int)cart.itemType].content += cart.itemQuantity;
            stock.contentChange.Trigger();
            stock.itemData[(int)cart.itemType].onWay -= cart.itemQuantity;
            cart.itemQuantity = 0;
            cart.UpdateLook();
        }
        boss.assert.AreEqual( cart.destination, stock );
        cart.destination = null;
        cart.stop++;
        cart.DoSchedule();
        return true;
    }
}

