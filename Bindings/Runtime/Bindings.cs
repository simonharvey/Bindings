using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Bindings
{
	public class Binding : IDisposable
	{
		public class Node
		{
			string _prop;
			INotifyPropertyChanged _value;
			//NativeSlice<int> _crumbs;

			Node _next;

			public INotifyPropertyChanged Value
			{
				get => _value;
				set
				{
					if (value != _value)
					{
						if (_value != null)
						{
							_value.PropertyChanged -= OnPropertyChanged;
						}

						_value = value;

						if (_value != null)
						{
							_value.PropertyChanged += OnPropertyChanged;
						}
					}
				}
			}

			private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName.Equals(_prop))
				{

				}
			}

			public Node(string prop, Node parent = null)
			{
				_prop = prop;
				if (parent != null)
				{
					parent._next = this;
				}
			}
		}

		readonly Node _root;

		public Binding(INotifyPropertyChanged target, string path, Action<object, object> cb)
		{
			var crumbs = path.Split('.')/*.Select(s=>s.GetHashCode())*/.ToArray();
			var node = _root = new Node(crumbs[0]);
			for (int i=1; i<crumbs.Length; ++i)
			{
				node = new Node(crumbs[i], node);
			}
		}

		/*Binding()
		{

		}*/

		public void Dispose()
		{
			//throw new NotImplementedException();
		}
	}

	namespace Example
	{
		public class BindableBase : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;
		}
	}
}
