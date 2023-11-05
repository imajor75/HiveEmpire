using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class Help
{
	public static int lastRow = 0, lastColumn = 0;
	public static int nextRow = 0, nextColumn = 0;

	public static Interface.Controller AddController( this MonoBehaviour control )
	{
		var controller = control.gameObject.AddComponent<Interface.Controller>();
		control.AddClickHandler( controller.Open, Interface.MouseButton.right );
		return controller;
	} 

	public static void ClosePanel( this MonoBehaviour control )
	{
		for ( var parent = control.transform.parent; parent; parent = parent.parent )
		{
            Interface.Panel panel = null;
			if ( parent.gameObject.TryGetComponent<Interface.Panel>( out panel ) )
			{
				panel.Close();
				return;
			}
		}
	}

	public static Image Image( this Component panel, Sprite picture = null )
	{
		Image i = new GameObject().AddComponent<Image>();
		i.name = "Image";
		i.sprite = picture;
		i.transform.SetParent( panel.transform );
		return i;
	}

	public static Image Image( this Component panel, Interface.Icon icon )
	{
		return panel.Image( Interface.iconTable.GetMediaData( icon ) );
	}

	public static Button CheckBox( this Component panel, string text )
	{
		Button b = new GameObject( "Checkbox" ).AddComponent<Button>();
		b.transform.SetParent( panel.transform );
		var i = new GameObject( "Checkbox Image" ).AddComponent<Image>();
		i.Link( b ).Pin( 0, 0, Interface.iconSize, Interface.iconSize ).AddOutline();
		void UpdateCheckboxLook( bool on )
		{
			i.sprite = Interface.iconTable.GetMediaData( on ? Interface.Icon.yes : Interface.Icon.no );
		}
		b.visualizer = UpdateCheckboxLook;
		b.leftClickHandler = b.Toggle;
		Text( b, text ).Link( b ).Stretch( Interface.iconSize + 5 ).alignment = TextAnchor.MiddleLeft;
		return b;
	}

	public static Text Text( this Component panel, string text = "", int fontSize = 12, bool alignToCenter = false )
	{
		Text t = new GameObject().AddComponent<Text>();
		if ( alignToCenter )
			t.alignment = TextAnchor.MiddleCenter;
		t.name = "Text";
		t.transform.SetParent( panel.transform );
		t.font = Interface.font;
		t.fontSize = (int)( fontSize * Interface.uiScale );
		t.color = Color.black;
		t.text = text;
		return t;
	}

	public static InputField InputField( this Component panel, string text = null )
	{
		var o = GameObject.Instantiate( Resources.Load<GameObject>( "InputField" ) );
		var i = o.GetComponent<InputField>();
		var image = i.GetComponent<Image>();
		i.transform.SetParent( panel.transform );
		i.name = "InputField";
		i.text = text;
		return i;
	}

	public static Dropdown Dropdown( this Component panel )
	{
		var o = GameObject.Instantiate( Resources.Load<GameObject>( "Dropdown" ) );
		var d = o.GetComponent<Dropdown>();
		d.ClearOptions();
		var image = d.GetComponent<Image>();
		d.transform.SetParent( panel.transform );
		d.name = "InputField";
		return d;
	}

	public static Slider Slider( this Component panel )
	{
		var o = GameObject.Instantiate( Resources.Load<GameObject>( "Slider" ) );
		var s = o.GetComponent<Slider>();
		s.transform.SetParent( panel.transform );
		return s;
	}

	public static UIElement Pin<UIElement>( this UIElement g, int x, int y, int xs = Constants.Interface.iconSize, int ys = Constants.Interface.iconSize, float xa = 0, float ya = 1, bool center = false ) where UIElement : Component
	{
		lastColumn = x;
		nextColumn = x + xs / ( center ? 2 : 1 );
		lastRow = y;
		nextRow = y - ys / ( center ? 2 : 1 );

		if ( center )
		{
			x -= xs / 2;
			y += ys / 2;
		}
		if ( g.transform is RectTransform t )
		{
			t.anchorMin = t.anchorMax = new Vector2( xa, ya );
			t.offsetMin = new Vector2( (int)( x * Interface.uiScale ), (int)( ( y - ys ) * Interface.uiScale ) );
			t.offsetMax = new Vector2( (int)( ( x + xs ) * Interface.uiScale ), (int)( y * Interface.uiScale ) );
		}
		else
			Assert.global.Fail( $"Object {g} without RectTransform component cannot be pinned" );
		return g;
	}

	public static UIElement PinCenter<UIElement>( this UIElement g, int x, int y, int xs = Constants.Interface.iconSize, int ys = Constants.Interface.iconSize, float xa = 0, float ya = 1 ) where UIElement : Component
	{
		return g.Pin( x, y, xs, ys, xa, ya, true );
	}

	public static UIElement PinDownwards<UIElement>( this UIElement g, int x = 0, int y = 0, int xs = Constants.Interface.iconSize, int ys = Constants.Interface.iconSize, float xa = 0, float ya = 1, bool center = false ) where UIElement : Component
	{
		return g.Pin( x + lastColumn, y + nextRow - (center ? ys / 2 : 0), xs, ys, xa, ya, center );
	}

	public static UIElement PinSideways<UIElement>( this UIElement g, int x = 0, int y = 0, int xs = Constants.Interface.iconSize, int ys = Constants.Interface.iconSize, float xa = 0, float ya = 1, bool center = false ) where UIElement : Component
	{
		return g.Pin( x + nextColumn + (center ? xs / 2 : 0), y + lastRow, xs, ys, xa, ya, center );
	}

	public static UIElement Stretch<UIElement>( this UIElement g, int x0 = 0, int y0 = 0, int x1 = 0, int y1 = 0 ) where UIElement : Component
	{
		if ( g.transform is RectTransform t )
		{
			t.anchorMin = Vector2.zero;
			t.anchorMax = Vector2.one;
			t.offsetMin = new Vector2( (int)( x0 * Interface.uiScale ), (int)( y0 * Interface.uiScale ) );
			t.offsetMax = new Vector2( (int)( x1 * Interface.uiScale ), (int)( y1 * Interface.uiScale ) );
		}
		return g;
	}

	public static UIElement Rotate<UIElement>( this UIElement g, float angle ) where UIElement : Component
	{
		g.transform.rotation = Quaternion.Euler( 0, 0, angle );
		return g;
	}

	[RequireComponent( typeof( RectTransform ) )]
	public class Button : MonoBehaviour, IPointerClickHandler
	{
		public Action leftClickHandler, rightClickHandler, middleClickHandler;
		public Action<bool> toggleHandler;
		public Action<bool> visualizer;
		public bool toggleState;

        public void OnPointerClick( PointerEventData eventData )
        {
			if ( eventData.button == PointerEventData.InputButton.Left && leftClickHandler != null )
				leftClickHandler();
			if ( eventData.button == PointerEventData.InputButton.Right )
			{
				if ( rightClickHandler != null )
					rightClickHandler();
				else
					this.ClosePanel();
			}
			if ( eventData.button == PointerEventData.InputButton.Middle && middleClickHandler != null )
				middleClickHandler();
        }

		public void Toggle()
		{
			SetToggleState( !toggleState );
			if ( toggleHandler != null )
				toggleHandler( toggleState );
		}

		public void SetToggleState( bool state )
		{
			if ( toggleState == state )
				return;
			toggleState = state;
			if ( visualizer == null )
				visualizer = UpdateLook;
			visualizer( toggleState );
		}

		public void UpdateLook( bool on )
		{
			var i = GetComponent<Image>();
			if ( i )
				i.color = on ? Color.white : Color.grey;
		}
    }

	public static UIElement AddHiveObjectHandler<UIElement>( this UIElement g, HiveObject hiveObject ) where UIElement : Component
	{
		var hoh = g.gameObject.AddComponent<Interface.HiveObjectHandler>();
		hoh.Open( hiveObject );
		return g;
	}

	public static UIElement AddClickHandler<UIElement>( this UIElement g, Action callBack, Interface.MouseButton type = Interface.MouseButton.left ) where UIElement : Component
	{
		var b = g.gameObject.GetComponent<Button>();
		if ( b == null )
			b = g.gameObject.AddComponent<Button>();
		if ( type == Interface.MouseButton.left )
			b.leftClickHandler = callBack;
		if ( type == Interface.MouseButton.right )
			b.rightClickHandler = callBack;
		if ( type == Interface.MouseButton.middle )
			b.middleClickHandler = callBack;
		return g;
	}

	public static UIElement AddToggleHandler<UIElement>( this UIElement g, Action<bool> callBack, bool initialState = false ) where UIElement : Component
	{
		var b = g.gameObject.GetComponent<Button>();
		if ( b == null )
			b = g.gameObject.AddComponent<Button>();

		b.leftClickHandler = b.Toggle;
		b.toggleHandler = callBack;
		b.toggleState = initialState;
		if ( b.visualizer == null )
			b.visualizer = b.UpdateLook;
		b.visualizer( initialState );

		return g;
	}

	public static UIElement SetToggleState<UIElement>( this UIElement g, bool state ) where UIElement : Component
	{
		var b = g.gameObject.GetComponent<Button>();
		b?.SetToggleState( state );
		return g;
	}


	public static UIElement Link<UIElement>( this UIElement g, Component parent ) where UIElement : Component
	{
		g.transform.SetParent( parent.transform, false );
		return g;
	}

	public static UIElement AddOutline<UIElement>( this UIElement g, Color? color = null, float distance = 1 ) where UIElement : Component
	{
		Outline o = g.gameObject.AddComponent<Outline>();
		if ( o != null )
		{
			o.effectColor = color ?? Color.black;
			o.effectDistance = Vector2.one * distance;
		}
		return g;
	}

	public static Color Light( this Color color )
	{
		return Color.Lerp( color, Color.white, 0.5f );
	}

	public static Color Dark( this Color color )
	{
		return Color.Lerp( color, Color.black, 0.5f );
	}

	public static Color Wash( this Color color )
	{
		return Color.Lerp( color, Color.grey, 0.5f );
	}

	[Serializable]
	public class Rectangle
	{
		public float xMin, xMax;
		public float yMin, yMax;
	
		public Rectangle()
		{
			Clear();
		}

		public Rectangle( Vector2 min, Vector2 max )
		{
			xMin = min.x;
			yMin = min.y;
			xMax = max.x;
			yMax = max.y;
		}

		public Rectangle Extend( Vector2 point )
		{
			if ( xMin > point.x )
				xMin = point.x;
			if ( xMax < point.x )
				xMax = point.x;
			if ( yMin > point.y )
				yMin = point.y;
			if ( yMax < point.y )
				yMax = point.y;
			return this;
		}

		public Rectangle Clear()
		{
			xMin = yMin = float.MaxValue;
			xMax = yMax = float.MinValue;
			return this;
		}

		public bool Contains( Vector2 point )
		{
			return xMin <= point.x && yMin <= point.y && xMax >= point.x && yMax >= point.y;
		}

		public Rectangle Grow( float value )
		{
			xMin -= value;
			yMin -= value;
			xMax += value;
			yMax += value;
			return this;
		}
	}

	public static bool Contains<UIElement>( this UIElement g, Vector2 position ) where UIElement : Component
	{
		if ( g.transform is RectTransform t )
		{
			Vector3[] corners = new Vector3[4];
			t.GetWorldCorners( corners );
			var rect = new Rect( corners[0], corners[2] );
			return rect.Contains( position );
		}
		return false;
	}
	
	public static UIElement SetTooltip<UIElement>( this UIElement g, Func<string> textGenerator, Sprite image = null, string additionalText = "", Action<bool> onShow = null, int width = 300 ) where UIElement : Component
	{
		Assert.global.IsTrue( textGenerator != null || onShow != null );
		var s = g.gameObject.GetComponent<Interface.TooltipSource>();
		if ( s == null )
			s = g.gameObject.AddComponent<Interface.TooltipSource>();
		s.SetData( textGenerator, image, additionalText, onShow, width );
		foreach ( Transform t in g.transform )
			t.SetTooltip( textGenerator, image, additionalText, onShow );
		return g;
	}

	public static UIElement SetTooltip<UIElement>( this UIElement g, string text, Sprite image = null, string additionalText = "", Action<bool> onShow = null ) where UIElement : Component
	{
		return SetTooltip( g, text == null ? null as Func<string> : () => text, image, additionalText, onShow );
	}

	public static UIElement SetTooltip<UIElement>( this UIElement g, Action<bool> onShow, Func<string> textGenerator = null ) where UIElement : Component
	{
		return SetTooltip( g, textGenerator, null, null, onShow );
	}

	public static UIElement RemoveTooltip<UIElement>( this UIElement g ) where UIElement : Component
	{
		var s = g.gameObject.GetComponent<Interface.TooltipSource>();
		if ( s )
			HiveCommon.Eradicate( s );
		foreach ( Transform t in g.transform )
			t.RemoveTooltip();
		return g;
	}

	public static UIElement AddHotkey<UIElement>( this UIElement g, string name, KeyCode key, bool ctrl = false, bool alt = false, bool shift = false ) where UIElement : Component
	{
		var h = g.gameObject.AddComponent<Interface.HotkeyControl>();
		h.Open( name, key, ctrl, alt, shift );
		g.name = name;
		return g;
	}

	public static Interface.Hotkey GetHotkey( this Component g )
	{
		return g.gameObject.GetComponent<Interface.HotkeyControl>()?.hotkey;
	}

	public static string GetPrettyName( this string name, bool capitalize = true )
	{
		bool beginWord = true;
		string result = "";
		foreach ( char c in name )
		{
			if ( Char.IsUpper( c ) )
			{
				beginWord = true;
				result += " ";
			}
			if ( beginWord && capitalize )
				result += Char.ToUpper( c );
			else
				result += Char.ToLower( c );

			beginWord = false;
		}
		return result;
	}

	public static ScrollRect Clear( this ScrollRect s )
	{
		foreach ( Transform child in s.content )
			HiveCommon.Eradicate( child.gameObject );

		return s;
	}

	public static ScrollRect SetContentSize( this ScrollRect scroll, int x = -1, int y = -1 )
	{
		var t = scroll.content.transform as RectTransform;
		var m = t.offsetMax;
		if ( y != -1 )
			m.y = (int)( Interface.uiScale * y );
		if ( x != -1 )
			m.x = (int)( Interface.uiScale * x );
		t.offsetMax = m;
		t.offsetMin = Vector2.zero;
		scroll.verticalNormalizedPosition = 1;
		return scroll;
	}

	public static ScrollRect ShowChild( this ScrollRect scroll, Component child, bool horizontal = false, bool vertical = true )
	{
		if ( child.transform is RectTransform rect )
		{
			var c = new Vector3[4];
			var v = new Vector3[4];
			rect.GetWorldCorners( c );
			scroll.viewport.GetWorldCorners( v );
			var contentPosition = scroll.content.localPosition;
			if ( vertical )
			{
				if ( c[0].y < v[0].y )
					contentPosition.y += v[0].y - c[0].y;
				if ( c[2].y > v[2].y )
					contentPosition.y -= c[2].y - v[2].y;
			}
			if ( horizontal )
			{
				if ( c[0].x < v[0].x )
					contentPosition.x += v[0].x - c[0].x;
				if ( c[2].x > v[2].x )
					contentPosition.x -= c[2].x - v[2].x;
			}
			scroll.content.localPosition = contentPosition;
		}
		return scroll;
	}

	public static string TimeToString( int time, bool text = false, bool ignoreSeconds = false, char separator = ':' )
	{
		string result = "";
		bool hasHours = false, hasDays = false;
		int days = time/24/60/60/Constants.Game.normalSpeedPerSecond;
		if ( days > 0 )
		{
			result = $"{days}";
			if ( text )
			{
				if ( days > 1 )
					result += " days and ";
				else
					result += " day and ";
			}
			else
				result += separator;
			hasDays = true;
		}
		int hours = time/Constants.Game.normalSpeedPerSecond/60/60;
		if ( hours > 0 )
		{
			result += $"{(hours%24).ToString( hasDays ? "d2" : "d1" )}";
			if ( text )
			{
				if ( hours % 24 > 1 )
					result += " hours and ";
				else
					result += " hour and ";
			}
			else
				result += separator;
			hasHours = true;
		}
		var minutes = time/Constants.Game.normalSpeedPerSecond/60;
		result += $"{(minutes%60).ToString( hasHours ? "d2" : "d1" )}";
		if ( text )
		{
			result += " minute";
			if ( minutes % 60 > 1 )
				result += "s";
		}
		if ( !hasDays &&!ignoreSeconds )
		{
			if ( text )
				result += " and ";
			else
				result += separator;
			result += $"{((time/Constants.Game.normalSpeedPerSecond)%60).ToString( "d2" )}";
		}
		return result;
	}

	public static bool SelectOption( this Dropdown d, string text )
	{
		for ( int i = 0; i < d.options.Count; i++ )
		{
			if ( d.options[i].text == text )
			{
				d.value = i;
				return true;
			}
		}
		return false;
	}

	public static List<byte> Add( this List<byte> packet, int value )
	{
		var valueBytes = BitConverter.GetBytes( value );
		foreach ( var b in valueBytes )
			packet.Add( b );
		return packet;
	}

	public static List<byte> Extract( this List<byte> packet, ref int value )
	{
		var size = BitConverter.GetBytes( value ).Length;
		value = BitConverter.ToInt32( packet.GetRange( 0, size ).ToArray(), 0 );
		packet.RemoveRange( 0, size );
		return packet;
	}

	public static List<byte> Add( this List<byte> packet, bool value )
	{
		var valueBytes = BitConverter.GetBytes( value );
		foreach ( var b in valueBytes )
			packet.Add( b );
		return packet;
	}

	public static List<byte> Extract( this List<byte> packet, ref bool value )
	{
		var size = BitConverter.GetBytes( value ).Length;
		value = BitConverter.ToBoolean( packet.GetRange( 0, size ).ToArray(), 0 );
		packet.RemoveRange( 0, size );
		return packet;
	}

	public static List<byte> Add<enumType>( this List<byte> packet, enumType value )
	{
		return packet.Add( (int)(object)value );
	}

	public static List<byte> Extract<enumType>( this List<byte> packet, ref enumType value ) where enumType : System.Enum
	{
		int data = 0;
		packet.Extract( ref data );
			value = (enumType)(object)data;
		return packet;
	}

	public static List<byte> Add( this List<byte> packet, List<int> array )
	{
		packet.Add( array.Count );
		foreach ( var value in array )
			packet.Add( value );
		return packet;
	}

	public static List<byte> Extract( this List<byte> packet, ref List<int> array )
	{
		array.Clear();
		int size = 0;
		packet.Extract( ref size );
		for ( int i = 0; i < size; i++ )
		{
			int value = 0;
			packet.Extract( ref value );
			array.Add( value );
		}
		return packet;
	}

	public static Type Random<Type>( this Type[] array )
	{
		return array[new System.Random().Next( array.Length )];
	}

    public static void Prepare( this SpriteRenderer renderer, Sprite sprite, Vector3 position, bool functional = false, int sortOffset = int.MaxValue )
    {
        renderer.sprite = sprite;
        renderer.material.shader = Interface.spriteShader;
        renderer.gameObject.layer = functional ? Constants.World.layerIndexMap : Constants.World.layerIndexSprites;
		if ( sortOffset != int.MaxValue )
			renderer.gameObject.AddComponent<VisibleHiveObject.SpriteController>().sortOffset = sortOffset;
		else
    		renderer.sortingOrder = (int)( -position.x * 100 );
		renderer.gameObject.AddComponent<BoxCollider>();
	}

	public static void Play( this AudioSource source, AudioClip clip, float volume = 1.0f, bool loop = false )
	{
		if ( !HiveCommon.game.inProgress )
			return;

		source.clip = clip;
		source.volume = volume;
		source.loop = loop;
		source.Play();
	}	
	
	static public string Prettify( this string raw )
	{
		string nice = "";
		bool capitalize = true;
		foreach ( var c in raw )
		{
			char current = c;
			if ( Char.IsUpper( c ) )
				nice += " ";
			if ( capitalize )
			{
				current = Char.ToUpper( c );
				capitalize = false;
			}
			nice += current;
		}
		return nice;
	}


	public class RectTransformDebugger : MonoBehaviour
	{
		public Vector2 anchorMin, anchorMax, offsetMin, offsetMax;
		public bool write;

		void Update()
		{
			RectTransform t = gameObject.GetComponent<RectTransform>();
			if ( write )
			{
				t.anchorMin = anchorMin;
				t.anchorMax = anchorMax;
				t.offsetMin = offsetMin;
				t.offsetMax = offsetMax;

			}
			else
			{
				anchorMin = t.anchorMin;
				anchorMax = t.anchorMax;
				offsetMin = t.offsetMin;
				offsetMax = t.offsetMax;
			}
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(Game.Timer))]
	public class TimerDrawer : PropertyDrawer
	{
		// Draw the property inside the given rect
		public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
		{
			var timer = property.boxedValue as Game.Timer;
			// Using BeginProperty / EndProperty on the parent property means that
			// prefab override logic works on the entire property.
			EditorGUI.BeginProperty( position, label, property );

			// Draw label
			position = EditorGUI.PrefixLabel( position, GUIUtility.GetControlID( FocusType.Passive ), label );

			// Don't make child fields be indented
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// Calculate rects
			var labelRect = new Rect( position.x, position.y, 100, position.height );
			var referenceRect = new Rect( position.x + 110, position.y, 100, position.height );

			// Draw fields - pass GUIContent.none to each so they are drawn without labels
			EditorGUI.LabelField( labelRect, ( timer.age < 0 ? "-" : "" ) + TimeToString( Math.Abs( timer.age ) ) );
			EditorGUI.PropertyField( referenceRect, property.FindPropertyRelative( "reference" ), GUIContent.none );

			// Set indent back to what it was
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}
	}
#endif
}



