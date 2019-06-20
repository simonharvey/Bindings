using System;
using System.Collections.Generic;
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

/*public class BindingNode
{
	public int TargetSlot;
	public BindableBase Object;
}*/

//  

public delegate void EventDelegate(int kEvent);

public class EventElement
{
	protected event EventDelegate eventdelegate;

	public void Dispatch(int kEvent)
	{
		if (eventdelegate != null)
		{
			eventdelegate(kEvent);
		}
	}

	public static EventElement operator +(EventElement kElement, EventDelegate kDelegate)
	{
		kElement.eventdelegate += kDelegate;
		return kElement;
	}

	public static EventElement operator -(EventElement kElement, EventDelegate kDelegate)
	{
		kElement.eventdelegate -= kDelegate;
		return kElement;
	}
}


//

internal class BindingNode
{
	public BindableBase Object;
	public int NameHash;
	//public BindableBase.BindingChangedDelegate Callback;

	public void SetObject(object value)
	{

	}

	public BindingNode Parent;
	public BindingNode FirstChild;
	public BindingNode PrevSibling, NextSibling;	

	public void Remove()
	{
		if (Parent != null)
		{
			if (Parent.FirstChild == this)
			{
				Parent.FirstChild = this.NextSibling;
			}

			if (PrevSibling != null)
			{
				PrevSibling.NextSibling = NextSibling;
			}

			if (NextSibling != null)
			{
				NextSibling.PrevSibling = PrevSibling;
			}

			NextSibling = PrevSibling = null;

			Parent = null;
		}
	}

	public void AddChild(BindingNode node)
	{
		if (FirstChild == null)
		{
			FirstChild = node;
		}
		else
		{
			var t = FirstChild.NextSibling;
			FirstChild.NextSibling = node;
			node.PrevSibling = FirstChild;
			node.NextSibling = t;
		}

		node.Parent = this;
		//var c = FirstChild;

	}
}

internal class BindingData
{
	public struct Binding
	{
		public NativeArray<int> Path;
		public BindableBase.BindingChangedDelegate Callback;
		public int Depth;

		public int FieldHash => Path[Depth];
		public bool IsLeaf => Path.Length - 1 == Depth;
	}

	public class SlotData
	{

	}

	public struct Source
	{
		public BindableBase Object;
		public int Slot;
	}

	public struct Path
	{
		
	}

	public HashSet<Source>[] Slots;
	public HashSet<Binding> Bindings = new HashSet<Binding>();
	//public NativeMultiHashMap

	internal BindingData(BindableBase host)
	{
		Slots = Enumerable.Range(0, host.FieldCount).Select(i => new HashSet<Source>()).ToArray();
	}

	~BindingData()
	{
		Debug.Log($"~BindingData");
		foreach (var b in Bindings)
		{
			if (b.Depth == 0)
			{
				Debug.Log("Dispose path");
				b.Path.Dispose();
			}
		}
	}
}

/*public class Slot
{
	public HashSet<Binding> Bindings = new HashSet<Binding>();
}*/

/*internal class BindingNod
{
	p
}*/

// Binding -> linked list of names
// maybe keep a flag if the bindable object is actually watched by some binding
// bindings propagate up!
public abstract class BindableBase
{ 
	public delegate void OnChangeDelegate(BindableBase target, string prop, object oldValue, object objectNewValue);
	public delegate void BindingChangedDelegate(object oldValue, object newValue);
	
	//public event OnChangeDelegate OnBindingChange;

	private BindingData _bindingData;
	private bool BindingEnabled => _bindingData != null;
	internal BindingData BindingData
	{
		get
		{
			if (_bindingData == null)
				_bindingData = new BindingData(this);
			return _bindingData;
		}
	}

	private BindingNode[] _bindingNodes;

	public BindableBase()
	{
		//_bindingNodes = new BindingNode[FieldCount];
		// todo: these dont compile to their procedural equivalents.... damn
		_bindingNodes = Enumerable.Range(0, FieldCount).Select(i => new BindingNode()).ToArray();
	}

	~BindableBase()
	{
		Debug.Log("Destructor called");
	}

	private void BubbleChange(int fieldIdx, object oldValue, object newValue)
	{

	}

	// this is called when one of our fields changes.
	protected void _SlotChanged(int fieldIdx, object oldValue, object newValue)
	{
		_bindingNodes[fieldIdx].SetObject(newValue);

		////if (BindingEnabled)
		//{
		//	//Debug.Log($"{this}._NotifyChangeValues({fieldIdx}, {oldValue}, {newValue})");
		//	var binding = BindingData;
		//	foreach (var b in binding.Bindings)
		//	{
		//		// if leaf, dispatch
		//		if (b.IsLeaf)
		//		{
		//			b.Callback(oldValue, newValue);
		//		}
		//
		//		// if branch, rewire. update towards leafs
		//		else
		//		{
		//			var node = _bindingNodes[fieldIdx];
		//		}
		//	}
		//	/*foreach (var i in binding.Slots[fieldIdx])
		//	{
		//		i.Object._NotifyChangeValues(i.Slot, oldValue, newValue);
		//		if (oldValue is BindableBase bo)
		//		{
		//			var bn = (BindableBase)newValue;
		//		}
		//	}*/
		//}
	}

	public int[] GetPath(string path)
	{
		var crumbs = path.Split('.');//.Select(f => Hash(f)).ToArray();
		int[] indices = new int[crumbs.Count()];
		var type = GetType();
		for (var i=0; i<indices.Length; ++i)
		{
			var f = type.GetRuntimeProperty(crumbs[i]);
			type = f.PropertyType;
		}
		return null;
	}

	public void Bind(string path, BindingChangedDelegate callback)
	{
		var crumbs = path.Split('.').Select(f=>Hash(f)).ToArray();
		Debug.Log($"Bind: [{string.Join(", ", crumbs)}]");

		var b = this.BindingData;
		var node = _bindingNodes[GetFieldIndex(crumbs[0])];

		for (int i=0; i<crumbs.Length; ++i)
		{

		}
		
		//var binding = new BindingData.Binding();
		//binding.Path = new NativeArray<int>(crumbs, Allocator.Persistent); ;
		//binding.Callback = callback;
		//b.Bindings.Add(binding);
	}

	//internal void Bind()

	public static int Hash(string str) => str.GetHashCode();

	// generated overrides
	public virtual int FieldCount
	{
		get
		{
			var fields = GetType().GetRuntimeProperties();
			var bindables = fields.Where(f => f.CustomAttributes.Any(c => c.AttributeType == typeof(BindableAttribute)));
			return bindables.Count();
		}
	}
	public virtual Type GetFieldType(int idx)
	{
		var fields = GetType().GetRuntimeProperties();
		var bindables = fields.Where(f => f.CustomAttributes.Any(c => c.AttributeType == typeof(BindableAttribute)));
		return bindables.ElementAt(idx).PropertyType;
	}
	public virtual int GetFieldIndex(string name) { return -1; }
	public virtual string GetFieldName(int idx) { return null; }
	public virtual int GetFieldIndex(int hash) { return -1; }
}

//public class BindingContext
//{
//	static Dictionary<Type, int> _typeBindings = new Dictionary<Type, int>();
//
//	struct BindableMember
//	{
//		public MemberInfo Member;
//
//	}
//
//	public void Register(BindableBase obj)
//	{
//		var type = obj.GetType();
//		
//		foreach (var m in type.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
//		{
//			var bindAttrs = m.GetCustomAttributes<Bind>(true);
//			foreach (var a in bindAttrs)
//			{
//				//Debug.Log($"Bind: {a.Uri}");
//				var crumbs = a.Uri.Split('.');
//				//obj.OnBindableFieldChange += Obj_OnBindableFieldChange;
//				obj.OnBindingChange += Obj_OnBindingChange;
//			}
//		}
//	}
//
//	public void Bind(string uri, Action fn)
//	{
//
//	}
//
//	private void Obj_OnBindingChange(BindableBase target, string prop, object oldValue, object objectNewValue)
//	{
//		
//	}
//
//	/*private void Obj_OnBindableFieldChange(object arg1, string arg2)
//	{
//		Debug.Log($"Obj_OnBindableFieldChange({arg1}, {arg2})");
//	}*/
//}
