using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

[assembly: Bindable]

public class Foo : INotifyPropertyChanged
{
	[Bindable]
	public int IntValue { get; set; }

	[Bindable]
	public Bar BarValue { get; set; }

	private int _unbindable;
	public int Unbindable
	{
		get => _unbindable;
		set
		{
			if (EqualityComparer<int>.Default.Equals(_unbindable, value))
				return;
			_unbindable = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Unbindable"));
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
}

public class Bar : Bindable
{ 
	[Bindable]
	public float FloatValue { get; set; }

	public override string ToString()
	{
		return $"[BAR floatValue:{FloatValue}]";
	}
}

public class Baz : Bindable
{
	[Bindable]
	public double DoubleValue { get; set; }
}