using System.Collections;
using System.Collections.Generic;
using Bindings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
	namespace PropChanged
	{

	}

	public class BindingTests
    {
		

		[Test]
		public void TestPropertyChange()
		{
			var foo = new Foo();
			var bar = new Bar();
			using (var b = new Binding(foo, "BarValue.FloatValue", BindingChanged))
			{
				foo.BarValue = bar;
				foo.BarValue.FloatValue = 12345;
			}
		}

		[Test]
		public void TestSubPropertyChange()
		{
			var foo = new Foo();
			var bar = new Bar
			{
				FloatValue = 1234
			};
			var bar2 = new Bar
			{
				FloatValue = 2345
			};
			using (var b = new Binding(foo, "BarValue.FloatValue", BindingChanged))
			{
				foo.BarValue = bar;
				foo.BarValue = bar2;
			}
		}

		private void BindingChanged(string path, object newVal)
		{
			Debug.Log($"{path} changed: {newVal}");
		}
	}
}
