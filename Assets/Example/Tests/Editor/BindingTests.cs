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
			var b = new Binding(foo, "BarValue.FloatValue", (path, newVal) => 
			{
				Debug.Log($"{path} changed: {newVal}");
			});
			//foo.PropertyChanged += PropertyChanged;
			//bar.PropertyChanged += PropertyChanged;
			foo.BarValue = bar;
			foo.BarValue.FloatValue = 12345;
			//foo.Unbindable = 666;
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

		private void PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			Debug.Log($"PropertyChanged: {sender} {e.PropertyName}");
		}

		/*private void Foo_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			//Debug.Log($"{sender} {e}");
		}*/

		//[Test]
		//public void TestSimpleBinding()
		//{
		//	var foo = new Foo();
		//	var bar = new Bar();
		//
		//	bool callbackReceived = false;
		//	/*foo.Bind("BarValue", (oldValue, newValue) => 
		//	{
		//		callbackReceived = true;
		//		Debug.Log($"BarValue CALLBACK RECEIVED!! {oldValue} -> {newValue}");
		//	});*/
		//
		//	foo.BarValue = bar;
		//	foo.BarValue.FloatValue = 80085f;
		//
		//	foo.Bind("BarValue.FloatValue", (oldValue, newValue) =>
		//	{
		//		callbackReceived = true;
		//		Debug.Log($"BarValue.FloatValue CALLBACK RECEIVED!! {oldValue} -> {newValue}");
		//	});
		//
		//	foo.BarValue.FloatValue = 1234f;
		//
		//	//foo.IntValue = 666;
		//	//foo.BarValue = new Bar();
		//
		//	Assert.IsTrue(callbackReceived);
		//    // Use the Assert class to test conditions
		//}

		/*[Test]
		public void TestBindingTree()
		{
			var foo = new Foo();
			foo.CreateBinding("BarValue.FloatValue");

			Assert.IsTrue(true);
		}*/

		// A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
		// `yield return null;` to skip a frame.
		/*[UnityTest]
        public IEnumerator BindingTestsWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }*/
	}
}
