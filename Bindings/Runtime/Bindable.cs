using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

/*public class BindingNode
{
	public string Name;
}*/

// bindings propagate up!
public class BindableBase
{ 
	public delegate void OnChangeDelegate(BindableBase target, string prop, object oldValue, object objectNewValue);
	
	private Dictionary<string, BindableBase> _bindings = new Dictionary<string, BindableBase>();

	public event OnChangeDelegate OnBindingChange;
	//public event Action<object, string> OnBindableFieldChange;

	public BindableBase()
	{
		//OnBindingChange +
	}

	protected void _NotifyChange(string name)
	{
		//OnBindableFieldChange?.Invoke(this, name);
	}

	protected void _NotifyChangeValues(string name, object oldValue, object newValue)
	{
		Debug.Log($"_NotifyChangeValues({name}, {oldValue}, {newValue})");
		OnBindingChange?.Invoke(this, name, oldValue, newValue);

		if (oldValue is BindableBase bo)
		{
			var bn = (BindableBase)newValue;
		}
	}
}

public class BindingContext
{
	static Dictionary<Type, int> _typeBindings = new Dictionary<Type, int>();

	struct BindableMember
	{
		public MemberInfo Member;

	}

	public void Register(BindableBase obj)
	{
		var type = obj.GetType();
		
		foreach (var m in type.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
		{
			var bindAttrs = m.GetCustomAttributes<Bind>(true);
			foreach (var a in bindAttrs)
			{
				//Debug.Log($"Bind: {a.Uri}");
				var crumbs = a.Uri.Split('.');
				//obj.OnBindableFieldChange += Obj_OnBindableFieldChange;
				obj.OnBindingChange += Obj_OnBindingChange;
			}
		}
	}

	public void Bind(string uri, Action fn)
	{

	}

	private void Obj_OnBindingChange(BindableBase target, string prop, object oldValue, object objectNewValue)
	{
		
	}

	/*private void Obj_OnBindableFieldChange(object arg1, string arg2)
	{
		Debug.Log($"Obj_OnBindableFieldChange({arg1}, {arg2})");
	}*/
}
