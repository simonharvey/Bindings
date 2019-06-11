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

public class BindableBase
{
	public event Action<object, string> OnBindableFieldChange;

	protected void _NotifyChange(string name)
	{
		OnBindableFieldChange?.Invoke(this, name);
	}
}

public class BindingContext
{
	static Dictionary<Type, int> _typeBindings = new Dictionary<Type, int>();

	struct BindableMember
	{
		public MemberInfo Member;

	}

	public void Register(object obj)
	{
		var type = obj.GetType();
		Debug.Log(type);
		
		foreach (var m in type.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
		{
			var bindAttrs = m.GetCustomAttributes<Bind>(true);
			foreach (var a in bindAttrs)
			{
				Debug.Log($"Bind: {a.Uri}");
				var crumbs = a.Uri.Split('.');
				//crumbs[0];
			}
		}

		//type.GetMembers().Select(m => )
	}
}
