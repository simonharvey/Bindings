using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEngine;

public class BindableAttribute : Attribute
{

}

public class Bind : Attribute
{
	public readonly string Uri;

	public Bind(string uri)
	{
		Uri = uri;
	}
}

public class Bindable : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	internal void NotifyChange()
	{
		PropertyChanged?.Invoke(null, null);
	}
}