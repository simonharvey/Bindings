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
			Binding _owner;
			string _prop;
			INotifyPropertyChanged _target;
			//NativeSlice<int> _crumbs;

			Node _next;

			public INotifyPropertyChanged Target
			{
				get => _target;
				set
				{
					//Debug.Log($"Set Host {this}: {_target} -> {value}");
					if (value != _target)
					{
						if (_target != null)
						{
							_target.PropertyChanged -= OnPropertyChanged;
						}

						if (value != null)
						{
							//Debug.Log($"Bind Host: {value}");
							value.PropertyChanged += OnPropertyChanged;
						}

						object oldValue = Value;
						_target = value;

						if (_next == null)
						{
							// leaf! lets dispatch
							//Debug.Log("LEAF!");
							_owner.Dispatch(oldValue, Value);
						}
						else
						{
							//Debug.Log($"Propagate {Value}");
							_next.Target = Value as INotifyPropertyChanged;
						}

					}
				}
			}

			private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				//Debug.Log($"Changed {_target} -> {_prop} == {e.PropertyName}");
				if (e.PropertyName.Equals(_prop))
				{
					if (_next != null)
					{
						_next.Target = Value as INotifyPropertyChanged;
					}
					else
					{
						// leaf
						_owner.Dispatch(null, Value);
					}
				}
			}

			public Node(Binding owner, INotifyPropertyChanged target, Queue<string> crumbs)
			{
				_owner = owner;
				_prop = crumbs.Dequeue();
				if (crumbs.Count > 0)
				{
					_next = new Node(owner, Value as INotifyPropertyChanged, crumbs);
				}
				Target = target;
			}

			/*public Node(INotifyPropertyChanged value, string prop)
			{
				Host = value;
				_prop = prop;
			}*/

			/*public Node(string prop, Node parent = null)
			{
				_prop = prop;
				if (parent != null)
				{
					parent._next = this;
				}
			}*/

			public object LeafValue
			{
				get
				{
					return _next != null ? _next.Value : Value;
				}
			}

			public object Value
			{
				get
				{
					if (_target != null)
					{
						return _target.GetType().GetProperty(_prop).GetValue(_target);
					}
					return null;
				}
			}
		}

		readonly Node _root;
		readonly Action<string, object> _cb;
		readonly string _path;

		public Binding(object target, string path, Action<string, object> cb)
		{
			_path = path;
			_cb = cb;
			if (target is INotifyPropertyChanged bindableTarget)
			{
				var crumbs = new Queue<string>(path.Split('.')/*.Select(s=>s.GetHashCode())*/);
				var node = _root = new Node(this, bindableTarget, crumbs);
			}
			else
			{
				throw new ArgumentException("target does not implement INotifyPropertyChanged");
			}
		}

		internal void Dispatch(object oldVal, object newVal)
		{
			//Debug.Log("Dispatch!");
			_cb(_path, newVal);
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
