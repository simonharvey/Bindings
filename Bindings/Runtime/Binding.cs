using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bindings
{
	public class Node
	{
		private BindableBase _value;
		public BindableBase Value
		{
			get => _value;
			set
			{
				if (value != _value)
				{

					_value = value;
				}
			}
		}
		public int SlotName;
		public Node Prev, Next;
	}

	public class Binding
	{
		public Node Root;

		

		/*public class SlotNode
		{

		}*/
	}

	public class SlotData
	{

	}

	public class BindingData
	{
		public SlotData[] Slots;

		public BindingData(int slotCount)
		{

		}

		public void SetSlotValue(int slot, object value)
		{

		}
	}
}

public static class BindingExt
{
	public static Bindings.Binding CreateBinding(this BindableBase self, string path)
	{
		var crumbs = path.Split('.').Select(f => BindableBase.Hash(f)).ToArray();
		Debug.Log($"Bind: [{string.Join(", ", crumbs)}]");

		

		return null;
	}
}
