using System;

public class Foo
{
	[Bindable]
	public int IntValue { get; set; }

	[Bindable]
	public Bar BarValue { get; set; }

	//public event Action<string> OnStringChange;

	public event Action<object> OnChangeTest;
}

public class Bar
{
	[Bindable]
	public float FloatValue { get; set; }
}

public class Baz : BindableBase
{
	[Bindable]
	public double DoubleValue { get; set; }
}