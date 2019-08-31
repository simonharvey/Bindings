using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

[assembly: Bindable]

public class BaseBindable : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;
}

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

	/*[Bind("IntValue")]
	void OnIntValueChange(int oldValue, int newValue)
	{
		Debug.Log($"OnIntValueChange({oldValue}, {newValue})");
	}

	[Bind("BarValue.FloatValue")]
	public void OnBarPropChanged()
	{
		Debug.Log($"OnBarPropChanged()");
	}*/
}

public class Bar : INotifyPropertyChanged
{
	[Bindable]
	public float FloatValue { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public override string ToString()
	{
		return $"[BAR floatValue:{FloatValue}]";
	}
}

//public class Garbage : BaseBindable
//{
//	void Doit()
//	{
//		PropertyChanged(null, null);
//	}
//}

public class Baz : INotifyPropertyChanged
{
	[Bindable]
	public double DoubleValue { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;
}