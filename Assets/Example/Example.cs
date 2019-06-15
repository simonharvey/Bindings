using System;
using UnityEngine;

[assembly: Bindable]

public class Foo : BindableBase
{
	[Bindable]
	public int IntValue { get; set; }

	[Bindable]
	public Bar BarValue { get; set; }

	public int Unbindable { get; set; }

	[Bind("IntValue")]
	void OnIntValueChange(int oldValue, int newValue)
	{
		Debug.Log($"OnIntValueChange({oldValue}, {newValue})");
	}

	[Bind("BarValue.FloatValue")]
	public void OnBarPropChanged()
	{
		Debug.Log($"OnBarPropChanged()");
	}

	/*public int ExampleIfs(string n)
	{
		if (n == "hello")
			return 1;
		if (n == "world")
			return 2;
		return 0;
	}

	public int ExampleSwitch(string n)
	{
		switch (n)
		{
			case "a": return 1;
			case "b": return 2;
			case "c": return 3;
			case "d": return 4;
			case "e": return 5;
			case "f": return 6;
			case "g": return 7;
			case "h": return 8;
			default: return 0;
		}
	}*/

	public string ExampleSwitch2(int n)
	{
		switch (n)
		{
			case 1: return "a";
			case 2: return "b";
			case 3: return "c";
			case 4: return "d";
			case 5: return "e";
			case 6: return "f";
			case 7: return "g";
			case 8: return "h";
			default: return "caca";
		}
	}
}

public class Bar
{
	[Bindable]
	public float FloatValue { get; set; }

	public override string ToString()
	{
		return "[BAR]";
	}
}

public class Baz : BindableBase
{
	[Bindable]
	public double DoubleValue { get; set; }
}

public class Example
{
	//[RuntimeInitializeOnLoadMethod]
	//public static void Garbage()
	//{
	//	//var ctx = new BindingContext();
	//	var foo = new Foo();
	//	//ctx.Register(foo);
	//	//foo.OnBindableFieldChange += Foo_OnBindableFieldChange;
	//
	//	/*((BindableBase)foo).Bind("BarValue.FloatValue", (v) =>
	//	{
	//		Debug.Log($"Changed: {v}");
	//	});*/
	//
	//	foo.IntValue = 666;
	//	foo.BarValue = new Bar();
	//	foo.BarValue.FloatValue = 12345;
	//}

	private static void Foo_OnBindableFieldChange(object arg1, string arg2)
	{
		//Debug.Log($"Changed: {arg1} {arg2}"); 
	}
}