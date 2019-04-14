using System;

public class BindableAttribute : Attribute
{

}

public class BindableBase
{
	public event Action<string> OnBindableFieldChange;

	protected void _NotifyChange(string name)
	{
		OnBindableFieldChange?.Invoke(name);
	}

	/*protected void _TestNotifyChange()
	{

	}*/
}
