﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.IO;

public class Flag : MonoBehaviour
{
	public static int maxItems = 8;
	public GroundNode node;
	public Item[] items = new Item[maxItems];
	public Worker user;

	public static bool CreateNew(Ground ground, GroundNode node)
    {
        if ( node.flag )
        {
            Debug.Log( "There is a flag there already" );
            return false;
        }
        if ( node.building )
        {
            Debug.Log( "Cannot create a flag at a building" );
            return false;
        }
		if ( node.road )
		{
			// TODO Make it possible to create a flag at a road
			Debug.Log( "Cannot create a flag at a road" );
			return false;
		}
        bool hasAdjacentFlag = false;
        foreach (var adjacentNode in node.neighbours)
            if (adjacentNode.flag)
                hasAdjacentFlag = true;
        if (hasAdjacentFlag)
        {
            Debug.Log("Another flag is too close");
            return false;
        }
        GameObject flagObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flagObject.name = "Flag "+node.x+", "+node.y;
        flagObject.transform.SetParent(ground.transform);
        flagObject.transform.localPosition = node.Position();
        flagObject.transform.localScale *= 0.3f;
        node.flag = (Flag)flagObject.AddComponent(typeof(Flag));
        node.flag.node = node;
        return true;
    }

	public bool ReleaseItem( Item item )
	{
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == item )
			{
				items[i] = null;
				item.flag = null;
				return true;
			}
		}
		Assert.IsTrue( false );
		return false;
	}

	public bool StoreItem( Item item )
	{
		Assert.IsNull( item.flag );
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == null )
			{
				items[i] = item;
				item.flag = this;
				return true;
			}
		}
		return false;
	}

	public void OnClicked()
	{
		FlagPanel.Open( this );
	}

	public void Validate()
    {
        Assert.AreEqual( this, node.flag );
        for ( int i = 0; i < 6; i++ )
            Assert.IsNull( node.neighbours[i].flag );
		foreach ( var i in items )
		{
			if ( i )
			{
				Assert.AreEqual( i.flag, this );
				i.Validate();
			}
		}
    }
}
 
