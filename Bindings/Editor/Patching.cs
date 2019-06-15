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
using static Mono.Cecil.Cil.OpCodes;

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

				Debug.Log($"Patching: {assPath}");
			}
			else
			{
				Debug.Log($"Not bindable : {assPath}");
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

			//Debug.Log($"Patched Bindable Assembly: {patched.Original}");
		}

		_patched.Clear();
	}

	private static void AssemblyReloadEvents_afterAssemblyReload()
	{
		//Debug.Log("AssemblyReloadEvents_afterAssemblyReload");
	}

	// https://www.codeproject.com/Articles/671259/Reweaving-IL-code-with-Mono-Cecil
	// https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stloc_0?view=netframework-4.8
	// https://github.com/Fody/PropertyChanging/blob/master/PropertyChanging.Fody/EqualityCheckWeaver.cs
	public static void PatchAssembly(AssemblyDefinition assDef)
	{
		var bindableModule = ModuleDefinition.ReadModule(typeof(BindableAttribute).Assembly.Location);
		var baseBindable = bindableModule.ImportReference(typeof(BindableBase));

		var modDef = assDef.Modules[0];
		foreach (var typeDef in modDef.Types)
		{
			var bindableFields = typeDef.Properties.Where(f => f.CustomAttributes.Any(a => a.AttributeType.Name == nameof(BindableAttribute))).ToArray();

			if (bindableFields.Count() == 0)
			{
				continue;
			}

			if (typeDef.BaseType.FullName == "System.Object")
			{
				typeDef.BaseType = modDef.ImportReference(baseBindable.GetElementType());
			}

			int fieldIdx = 0;
			string[] fieldNames = new string[bindableFields.Length];

			ILProcessor il;

			foreach (var prop in bindableFields)
			{
				prop.CustomAttributes.Remove(prop.CustomAttributes.Single(a => a.AttributeType.Name == nameof(BindableAttribute)));

				var reqBoxing = prop.PropertyType.IsPrimitive;

				var setterName = $"set_{prop.Name}";
				var getterName = $"get_{prop.Name}";
				var setter = typeDef.Methods.Single(m => m.Name == setterName);
				var getter = typeDef.Methods.Single(m => m.Name == getterName);
				il = setter.Body.GetILProcessor();
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
				il.Append(il.Create(OpCodes.Ldc_I4, fieldIdx));
				//il.Append(ldstr);
				il.Append(il.Create(OpCodes.Ldloc_0));
				il.Append(boxOp);
				il.Append(il.Create(OpCodes.Ldarg_1));
				il.Append(boxOp);
				il.Append(il.Create(OpCodes.Call, notifyValuesRef));

				il.Append(il.Create(OpCodes.Ret));

				fieldNames[fieldIdx] = prop.Name;

				++fieldIdx;
			}

			// create field idx lookup methods
			// https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.switch?view=netframework-4.8

			var hashFn = typeof(BindableBase).GetMethod("Hash", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			var hashRef = modDef.ImportReference(hashFn);

			{
				int[] hashes = new int[fieldIdx];

				for (int i = 0; i < fieldIdx; ++i)
				{
					hashes[i] = (int)hashFn.Invoke(null, new object[] { fieldNames[i] });
				}

				var nameToIdx = new MethodDefinition("GetFieldIndex", MethodAttributes.Public | MethodAttributes.Virtual, modDef.TypeSystem.Int32);
				nameToIdx.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.In, modDef.TypeSystem.String));
				var vHash = new VariableDefinition(modDef.TypeSystem.Int32);
				nameToIdx.Body.Variables.Add(vHash);
				var vResult = new VariableDefinition(modDef.TypeSystem.Int32);
				nameToIdx.Body.Variables.Add(vResult);

				il = nameToIdx.Body.GetILProcessor();

				var labels = Enumerable.Range(0, fieldIdx + 1).Select(i => il.Create(Ldloc_0)).ToArray();
				var outLabel = labels[fieldIdx] = il.Create(Ldloc, vResult);

				il.Emit(Ldc_I4, -1);
				il.Emit(Stloc, vResult);

				il.Emit(Ldarg_1);
				il.Emit(Call, hashRef);
				il.Emit(Stloc_0);

				for (int i = 0; i < fieldIdx; ++i)
				{
					il.Append(labels[i]);
					il.Emit(Ldc_I4, hashes[i]);
					il.Emit(Ceq);
					il.Emit(Brfalse, labels[i + 1]);
					il.Emit(Ldc_I4, i);
					il.Emit(Stloc, vResult);
					il.Emit(Br_S, outLabel);
				}

				il.Append(outLabel);
				il.Emit(Ret);

				typeDef.Methods.Add(nameToIdx);
			}

			{
				var idxToName = new MethodDefinition("GetFieldName", MethodAttributes.Public | MethodAttributes.Virtual, modDef.TypeSystem.String);
				idxToName.Parameters.Add(new ParameterDefinition("idx", ParameterAttributes.In, modDef.TypeSystem.Int32));

				il = idxToName.Body.GetILProcessor();

				var V_0 = new VariableDefinition(modDef.TypeSystem.Int32);
				var V_1 = new VariableDefinition(modDef.TypeSystem.String);

				idxToName.Body.Variables.Add(V_0);
				idxToName.Body.Variables.Add(V_1);

				var outLabel = il.Create(Ldloc_1);
				var defaultLabel = il.Create(Ldnull);

				var switches = new Instruction[fieldIdx];
				il = idxToName.Body.GetILProcessor();
				for (int i = 0; i < fieldIdx; ++i)
				{
					switches[i] = il.Create(OpCodes.Ldstr, fieldNames[i]);
				}

				il.Emit(Ldarg_1);
				il.Emit(Stloc_0);
				il.Emit(Ldloc_0);
				il.Emit(Switch, switches);
				il.Emit(Br_S, defaultLabel);

				for (int i = 0; i < fieldIdx; ++i)
				{
					il.Append(switches[i]);
					il.Emit(Stloc, V_1);
					il.Emit(Br_S, outLabel);
				}

				// default
				il.Append(defaultLabel);
				il.Emit(Stloc_1);
				il.Emit(Br_S, outLabel);

				il.Append(outLabel);
				il.Emit(Ret);
				typeDef.Methods.Add(idxToName);
			}
		}
	}
}