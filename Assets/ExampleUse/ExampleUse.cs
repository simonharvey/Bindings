using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleUse
{
	[RuntimeInitializeOnLoadMethod]
    static void RunExample()
	{
		Foo foo = new Foo();
		Debug.Log(foo.GetFieldName(1));
		//Debug.Log(foo.GetFieldName(0));
		//(foo as BindableBase).
		//foo.
	}
}
