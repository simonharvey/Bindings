using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;


public class Patching
{
	struct PatchedEntry
	{
		public string Original;
		public string Patched;
	}

	private static List<PatchedEntry> _patched = new List<PatchedEntry>();
	private const string TempPath = "Library/PatchedAssemblyTemp";

	[InitializeOnLoadMethod]
	private static void Initialize()
	{
		//Debug.Log("Bindings::Initialize()");
		CompilationPipeline.assemblyCompilationStarted += CompilationPipeline_assemblyCompilationStarted;
		CompilationPipeline.assemblyCompilationFinished += CompilationPipeline_assemblyCompilationFinished;
		AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadEvents_beforeAssemblyReload;
		AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
	}

	private static void CompilationPipeline_assemblyCompilationStarted(string assPath)
	{
		//Debug.Log($"CompilationPipeline_assemblyCompilationStarted {assPath}");
	}

	private static void CompilationPipeline_assemblyCompilationFinished(string assPath, CompilerMessage[] arg2)
	{
		// if an assembly has finished compiling and is bindable, need to process

		//Debug.Log($"CompilationPipeline_assemblyCompilationFinished {assPath}");

		using (var assDef = AssemblyDefinition.ReadAssembly(assPath))
		{
			var count = assDef.CustomAttributes.Count;

			var otherAttrs = assDef.CustomAttributes.Where((a) => a.AttributeType.Name != nameof(BindableAttribute)).ToList();
			if (otherAttrs.Count != assDef.CustomAttributes.Count)
			{
				assDef.CustomAttributes.Clear();
				foreach (var attr in otherAttrs)
				{
					assDef.CustomAttributes.Add(attr);
				}

				PatchAssembly(assDef);

				var outputPath = $"{Application.temporaryCachePath}/{assDef.Name.Name}.dll";// "Library/PatchedAssemblyTemp";
				assDef.Write(outputPath);

				_patched.Add(new PatchedEntry
				{
					Original = assPath,
					Patched = outputPath,
				});
			}
		}
	}

	private static void AssemblyReloadEvents_beforeAssemblyReload()
	{
		//Debug.Log("AssemblyReloadEvents_beforeAssemblyReload");

		// overwrite all patched assemblies
		foreach (var patched in _patched)
		{
			File.Delete(patched.Original);
			File.Move(patched.Patched, patched.Original);

			Debug.Log($"Patched Bindable Assembly: {patched.Original}");
		}

		_patched.Clear();
	}

	private static void AssemblyReloadEvents_afterAssemblyReload()
	{
		//Debug.Log("AssemblyReloadEvents_afterAssemblyReload");
	}

	// https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stloc_0?view=netframework-4.8
	// https://github.com/Fody/PropertyChanging/blob/master/PropertyChanging.Fody/EqualityCheckWeaver.cs
	public static void PatchAssembly(AssemblyDefinition assDef)
	{
		var bindableModule = ModuleDefinition.ReadModule(typeof(BindableAttribute).Assembly.Location); 
		var baseBindable = bindableModule.ImportReference(typeof(BindableBase));
		
		var modDef = assDef.Modules[0];
		foreach (var t in modDef.Types)
		{
			var tt = t;
			var bindableFields = t.Properties.Where(f => f.CustomAttributes.Any(a => a.AttributeType.Name == nameof(BindableAttribute))).ToArray();

			if (bindableFields.Count() > 0 && t.BaseType.FullName == "System.Object")
			{
				t.BaseType = modDef.ImportReference(baseBindable.GetElementType());
			}

			foreach (var prop in bindableFields)
			{
				prop.CustomAttributes.Remove(prop.CustomAttributes.Single(a => a.AttributeType.Name == nameof(BindableAttribute)));

				var reqBoxing = prop.PropertyType.IsPrimitive;
				
				var setterName = $"set_{prop.Name}";
				var getterName = $"get_{prop.Name}";
				var setter = t.Methods.Single(m => m.Name == setterName);
				var getter = t.Methods.Single(m => m.Name == getterName);
				var il = setter.Body.GetILProcessor();
				var notifyRef = modDef.ImportReference(typeof(BindableBase).GetMethod("_NotifyChange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));//.Resolve();
				var notifyValuesRef = modDef.ImportReference(typeof(BindableBase).GetMethod("_NotifyChangeValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));//.Resolve();

				var boxOp = prop.PropertyType.IsPrimitive ? il.Create(OpCodes.Box, modDef.ImportReference(prop.PropertyType)) : il.Create(OpCodes.Nop);

				var ldstr = il.Create(OpCodes.Ldstr, prop.Name);

				// remove the ret instruction to patch the end of the method
				il.Remove(setter.Body.Instructions[setter.Body.Instructions.Count() - 1]);

				var tempVar = new VariableDefinition(prop.PropertyType);
				setter.Body.Variables.Add(tempVar);

				// todo: this is probably flimsy, the stack might get fucked, idk

				// new
				var entryOp = il.Body.Instructions[0];

				il.InsertBefore(entryOp, il.Create(OpCodes.Ldarg_0));
				il.InsertBefore(entryOp, il.Create(OpCodes.Call, getter));
				il.InsertBefore(entryOp, il.Create(OpCodes.Stloc_0));

				il.Append(il.Create(OpCodes.Ldarg_0));
				il.Append(ldstr);
				il.Append(il.Create(OpCodes.Ldloc_0));
				il.Append(boxOp);
				il.Append(il.Create(OpCodes.Ldarg_1));
				il.Append(boxOp);
				il.Append(il.Create(OpCodes.Call, notifyValuesRef));

				il.Append(il.Create(OpCodes.Ret));
			}
		}
	}

	[MenuItem("Assets/Patch Assembly")]
	private static void PathAssemblyDef()
	{
		/*Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.Full);
		var ass = (AssemblyDefinitionAsset)Selection.activeObject;
		AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Selection.activeObject));
		//EditorUtility.SetDirty(ass);
		//AssetDatabase.Refresh();
		PatchAssembly($"Library/ScriptAssemblies/{ass.name}.dll");*/
	}

	[MenuItem("Assets/Patch Assembly", true)]
	private static bool PathAssemblyValidation()
	{
		return Selection.activeObject is AssemblyDefinitionAsset;
	}
}