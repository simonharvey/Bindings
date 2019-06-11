using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
//using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;

public class Bindings
{

}

public class Patching
{
	[InitializeOnLoadMethod]
	private static void Initialize()
	{
		//Debug.Log("Bindings::Initialize()");
		//AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadEvents_beforeAssemblyReload;
		//AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
	}

	private static void AssemblyReloadEvents_beforeAssemblyReload()
	{
		//throw new System.NotImplementedException();
	}

	private static void AssemblyReloadEvents_afterAssemblyReload()
	{
		var assemblies = CompilationPipeline.GetAssemblies();
		
		foreach (var a in assemblies)
		{
			Debug.Log($"> {a.name}");
			try
			{
				PatchAssembly(a.name);
			}
			catch (Exception e)
			{
				Debug.LogWarning(e);
			}
		}
		//Debug.Log($"assemblies {assemblies.Select(a=>a.name).ToArray().Join(", ")}");
	}

	public static void PatchAssembly(string assName)
	{
		var bindableModule = ModuleDefinition.ReadModule(typeof(BindableAttribute).Assembly.Location); 
		//var bindableModule = ModuleDefinition.ReadModule($"Library/ScriptAssemblies/Bindings.dll");
		var baseBindable = bindableModule.ImportReference(typeof(BindableBase));
		var assPath = $"Library/ScriptAssemblies/{assName}.dll";

		using (var assDef = AssemblyDefinition.ReadAssembly(assPath))
		{
			var modDef = assDef.Modules[0];
			foreach (var t in modDef.Types)
			{
				var tt = t;
				var bindableFields = t.Properties.Where(f => f.CustomAttributes.Any(a => a.AttributeType.Name == nameof(BindableAttribute))).ToArray();

				if (bindableFields.Count() > 0 && t.BaseType.FullName == "System.Object")
				{
					t.BaseType = baseBindable;
				}

				foreach (var prop in bindableFields)
				{
					Debug.Log($"Patch Prop {prop}");
					prop.CustomAttributes.Remove(prop.CustomAttributes.Single(a => a.AttributeType.Name == nameof(BindableAttribute)));

					var setterName = $"set_{prop.Name}";
					var setter = t.Methods.Single(m => m.Name == setterName);
					var il = setter.Body.GetILProcessor();
					var rr = bindableModule.ImportReference(typeof(BindableBase).GetMethod("_NotifyChange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));//.Resolve();

					var ldstr = il.Create(OpCodes.Ldstr, prop.Name);
					var call = il.Create(OpCodes.Call, modDef.ImportReference(rr));
					il.Remove(setter.Body.Instructions[setter.Body.Instructions.Count() - 1]);

					il.Append(il.Create(OpCodes.Ldarg_0));
					il.Append(ldstr);
					il.Append(call);
					il.Append(il.Create(OpCodes.Ret));
				}
			}

			//var assName = $"{assDef.Name.Name}_Patched";
			Debug.Log($"Patched {assName}");
			//assDef.Name.Name = assName;
			//assDef.Write($"Library/ScriptAssemblies/{assName}_Patched.dll");
			assDef.Write($"{assName}.dll");
		}

		File.Delete(assPath);
		File.Move($"{assName}.dll", assPath);
	}

	[MenuItem("Assets/Patch Assembly")]
	private static void PathAssembly()
	{
		Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.Full);
		var ass = (AssemblyDefinitionAsset)Selection.activeObject;
		AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Selection.activeObject));
		//EditorUtility.SetDirty(ass);
		//AssetDatabase.Refresh();
		PatchAssembly(ass.name);
	}

	[MenuItem("Assets/Patch Assembly", true)]
	private static bool PathAssemblyValidation()
	{
		return Selection.activeObject is AssemblyDefinitionAsset;
	}
}