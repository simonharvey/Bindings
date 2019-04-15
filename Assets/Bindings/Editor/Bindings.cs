using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
//using System.Reflection.Emit;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class Bindings
{
	[MenuItem("Assets/Patch Assembly")]
	private static void PathAssembly()
	{
		Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.Full);
		var ass = (AssemblyDefinitionAsset)Selection.activeObject;
		//EditorUtility.SetDirty(ass);
		//AssetDatabase.Refresh();
		var bindableModule = ModuleDefinition.ReadModule($"Library/ScriptAssemblies/Bindings.dll");
		var baseBindable = bindableModule.ImportReference(typeof(BindableBase));

		using (var assDef = AssemblyDefinition.ReadAssembly($"Library/ScriptAssemblies/{ass.name}.dll"))
		{
			var modDef = assDef.Modules[0];
			//using (var modDef = ModuleDefinition.ReadModule($"Library/ScriptAssemblies/{ass.name}.dll"))
			//{
			Debug.Log(modDef);
			foreach (var t in modDef.Types)
			{
				var tt = t;
				var bindableFields = t.Properties.Where(f => f.CustomAttributes.Any(a => a.AttributeType.Name == nameof(BindableAttribute))).ToArray();

				if (bindableFields.Count() > 0 && t.BaseType.FullName == "System.Object")
				{
					t.BaseType = baseBindable;
					/*t.BaseType = new TypeDefinition("", "BindableBase", TypeAttributes.Public | TypeAttributes.BeforeFieldInit).Resolve();
					tt = t.Resolve();
					Debug.Log(tt.BaseType);*/
				}

				//var changedFnDef = t.Resolve().Methods.Single(m => m.Name == "_NotifyChange").Resolve();
				//Debug.Log(changedFnDef);

				foreach (var prop in bindableFields)
				{
					//Debug.Log(prop);
					prop.CustomAttributes.Remove(prop.CustomAttributes.Single(a => a.AttributeType.Name == nameof(BindableAttribute)));

					var setterName = $"set_{prop.Name}";
					var setter = t.Methods.Single(m => m.Name == setterName);
					//Debug.Log(setter);

					//var eventDef = new EventDefinition($"On{prop.Name}Changed", EventAttributes.None, new TypeDefinition("System", "Action", TypeAttributes.Import));
					//eventDef.Resolve();
					//t.Events.Add(eventDef);

					var il = setter.Body.GetILProcessor();
					var rr = bindableModule.ImportReference(typeof(BindableBase).GetMethod("_NotifyChange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));//.Resolve();
					
					var ldstr = il.Create(OpCodes.Ldstr, prop.Name);
					var call = il.Create(OpCodes.Call, modDef.ImportReference(rr));
					il.Remove(setter.Body.Instructions[setter.Body.Instructions.Count() - 1]);

					il.Append(il.Create(OpCodes.Ldarg_0));
					il.Append(ldstr);
					il.Append(call);
					il.Append(il.Create(OpCodes.Ret));

					//il.InsertBefore(setter.Body.Instructions[setter.Body.Instructions.Count()-1], ldstr);
					//il.InsertAfter(ldstr, call);

					//var testNotify = baseBindable.Resolve().Methods.Single(m => m.Name == "_TestNotifyChange");
					//var testNotify = bindableModule.ImportReference.Methods.Single(m => m.Name == "_TestNotifyChange");

					//var instruction = proc.Create(OpCodes.Call, testNotify);
					//proc.InsertAfter(setter.Body.Instructions[setter.Body.Instructions.Count - 1], instruction);
				}
			}
			//}

			var assName = $"{assDef.Name.Name}_Patched";
			Debug.Log(assName);
			assDef.Name.Name = assName;
			//assDef.Name = new AssemblyNameDefinition(assName, assDef.Name.Version);
			assDef.Write($"Library/ScriptAssemblies/{assName}.dll");
		}
	}

	/*private EventDefinition GetOrCreateEventDefinition(TypeDefinition owner, string name, TypeDefinition fieldType)
	{

	}*/

	public static void PatchAssemble(string srcPath, string dstPath)
	{

	}

	[MenuItem("Assets/Patch Assembly", true)]
	private static bool PathAssemblyValidation()
	{
		return Selection.activeObject is AssemblyDefinitionAsset;
	}
}
