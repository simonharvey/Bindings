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
			
			Node _next;

			public INotifyPropertyChanged Target
			{
				get => _target;
				set
				{
					if (value != _target)
					{
						if (_target != null)
						{
							_target.PropertyChanged -= OnPropertyChanged;
						}

						if (value != null)
						{
							value.PropertyChanged += OnPropertyChanged;
						}

						object oldValue = Value;
						_target = value;

						if (_next == null)
						{
							// leaf! lets dispatch
							_owner.Dispatch(oldValue, Value);
						}
						else
						{
							_next.Target = Value as INotifyPropertyChanged;
						}

					}
				}
			}

			private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
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
			_cb(_path, newVal);
		}

		public void Dispose()
		{
			
		}
	}
}
