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

	}
}

public class Bar
{ 
	/*[Bindable]
	public float FloatValue { get; set; }*/
}

public class Baz : BindableBase
{
	[Bindable]
	public double DoubleValue { get; set; }
}

public class Example
{
	[RuntimeInitializeOnLoadMethod]
	public static void Garbage()
	{
		var ctx = new BindingContext();
		var foo = new Foo();
		ctx.Register(foo);
		foo.OnBindableFieldChange += Foo_OnBindableFieldChange;

		foo.IntValue = 666;
		foo.BarValue = new Bar();
	}

	private static void Foo_OnBindableFieldChange(object arg1, string arg2)
	{
		//Debug.Log($"Changed: {arg1} {arg2}"); 
	}
}