using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
//using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using static Mono.Cecil.Cil.OpCodes;

internal class BindableBaseImpl : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;
}

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
		Debug.Log($"CompilationPipeline_assemblyCompilationStarted {assPath}");
	}

	private static void CompilationPipeline_assemblyCompilationFinished(string assPath, CompilerMessage[] arg2)
	{
		// if an assembly has finished compiling and is bindable, need to process

		Debug.Log($"CompilationPipeline_assemblyCompilationFinished {assPath}");

		DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
		resolver.AddSearchDirectory(Directory.GetParent(assPath).FullName);
		Debug.Log(Directory.GetParent(assPath).FullName);

		using (var assDef = AssemblyDefinition.ReadAssembly(assPath, new ReaderParameters { AssemblyResolver = resolver }))
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
			//	Debug.Log($"Not bindable : {assPath}");
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

	public static void PatchAssembly(AssemblyDefinition assDef)
	{
		var bindableModule = ModuleDefinition.ReadModule(typeof(BindableAttribute).Assembly.Location);
		var notifyInterface = bindableModule.ImportReference(typeof(INotifyPropertyChanged));
		var pcev = bindableModule.ImportReference(typeof(PropertyChangedEventHandler));

		var bMod = ModuleDefinition.ReadModule(typeof(BindableBaseImpl).Module.FullyQualifiedName);
		var bindableTemplate = bMod.GetType(typeof(BindableBaseImpl).Name);

		var modDef = assDef.Modules[0];
		foreach (var typeDef in modDef.Types)
		{
			var bindableFields = typeDef.Properties.Where(f => f.CustomAttributes.Any(a => a.AttributeType.Name == nameof(BindableAttribute))).ToArray();

			if (bindableFields.Count() == 0)
			{
				continue;
			}

			var hostType = Type.GetType(typeDef.FullName + ", " + typeDef.Module.Assembly.FullName);

			//if (!typeDef.Interfaces.Any(i => i.InterfaceType.Resolve().FullName == typeof(INotifyPropertyChanged).FullName))
			if (!(typeof(INotifyPropertyChanged).IsAssignableFrom(hostType)))
			{
				Debug.LogError($"{typeDef} cannot use [Bindable] as it doesn't implement INotifyPropertyChanged");
				continue;
			}
			else
			{
				var propertyChangedEventArgs = assDef.MainModule.ImportReference(typeof(PropertyChangedEventArgs));
				var propertyChangedEventArgsCtor = assDef.MainModule.ImportReference(propertyChangedEventArgs.Resolve().Methods.First(m => m.Name == ".ctor"));
				var propertyChangedEventHandler = assDef.MainModule.ImportReference(typeof(PropertyChangedEventHandler));
				var propertyChangedEventHandlerInvoke = assDef.MainModule.ImportReference(propertyChangedEventHandler.Resolve().Methods.First(m => m.Name == "Invoke"));
				var bindable = assDef.MainModule.ImportReference(typeof(Bindable));
				var bindableInvoke = assDef.MainModule.ImportReference(bindable.Resolve().Methods.First(m => m.Name.Equals("NotifyChange")));

				foreach (var property in bindableFields)
				{
					//Debug.Log("Injecting in " + property.FullName);

					var setter = property.SetMethod;
					var backingField = property.DeclaringType.Fields.First(f => f.Name == "<" + property.Name + ">k__BackingField");

					FieldDefinition propChangedEventHandlerField = null;
					MethodDefinition methodDef = null;

					try
					{
						propChangedEventHandlerField = typeDef.Fields.First(f => f.Name == "PropertyChanged");
					}
					catch (InvalidOperationException e)
					{
						Debug.Log($"INotifyPropertyChanged but not prop: {property.FullName}");
						/*var bindableType = typeDef.BaseType.Resolve();
						while (bindableType != null)
						{
							if (bindableType.FullName.Equals(typeof(Bindable).Name)) // puke
							{
								methodDef = bindableType.Methods.First(m => m.Name.Equals("NotifyChange"));
								methodDef = methodDef.Module.ImportReference(methodDef).Resolve();
								break;
							}
							//Debug.Log($"{bindableType.FullName} {bindableType.FullName.Equals(typeof(Bindable).Name)}");
							bindableType = bindableType.BaseType?.Resolve();
						}

						Debug.Log(methodDef);*/
						//continue;
					}

					MethodReference getDefault;
					MethodReference equals;

					TypeDefinition comparer = null;
					//(TypeDefinition)property.CustomAttributes
					//.First(a => a.AttributeType.FullName == typeof(BindableAttribute).FullName)
					//.ConstructorArguments[0].Value;

					if (comparer == null)
					{
						var type = assDef.MainModule.ImportReference(typeof(EqualityComparer<>)).Resolve();
						var typeReference = (TypeReference)type;
						typeReference = typeReference.MakeGenericInstanceType(property.PropertyType);
						getDefault = assDef.MainModule.ImportReference(type.Properties.First(m => m.Name == "Default").GetMethod.MakeHostInstanceGeneric(property.PropertyType));
						equals = assDef.MainModule.ImportReference(type.Methods.First(m => m.Name == "Equals").MakeHostInstanceGeneric(property.PropertyType));
					}
					else
					{
						getDefault = comparer.Methods.First(m => m.Name == "get_Default");
						equals = comparer.Methods.First(m => m.Name == "Equals");
					}

					setter.Body.Instructions.Clear();
					setter.Body.Variables.Add(new VariableDefinition(modDef.TypeSystem.Boolean));
					/*setter.Body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
					setter.Body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
					setter.Body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));*/

					var ilGenerator = setter.Body.GetILProcessor();
					var start = ilGenerator.Create(Nop);
					var ret = ilGenerator.Create(Ret);
					var ldarg_0_18 = ilGenerator.Create(Ldarg_0);
					var ldarg_0_2b = ilGenerator.Create(Ldarg_0);

					setter.Body.Instructions.Add(start);
					ilGenerator.InsertAfter(start, ret);

					ilGenerator.InsertBefore(ret, ilGenerator.Create(Call, getDefault));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldarg_0));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldfld, backingField));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldarg_1));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Callvirt, equals));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Stloc_0));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldloc_0));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Brfalse_S, ldarg_0_18));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Br_S, ret));
					ilGenerator.InsertBefore(ret, ldarg_0_18);
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldarg_1));
					ilGenerator.InsertBefore(ret, ilGenerator.Create(Stfld, backingField));

					if (propChangedEventHandlerField != null)
					{
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldarg_0));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldfld, propChangedEventHandlerField));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Dup));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Brtrue_S, ldarg_0_2b));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Pop));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Br_S, ret));
						ilGenerator.InsertBefore(ret, ldarg_0_2b);
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldstr, property.Name));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Newobj, propertyChangedEventArgsCtor));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Callvirt, propertyChangedEventHandlerInvoke));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Nop));
					}
					else
					{
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldarg_0));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Ldstr, property.Name));
						ilGenerator.InsertBefore(ret, ilGenerator.Create(Call, bindableInvoke));
					}
					
					var attributesToRemove = new List<CustomAttribute>();

					foreach (var attribute in property.CustomAttributes.Where(c => c.AttributeType.FullName == typeof(BindableAttribute).FullName))
						attributesToRemove.Add(attribute);

					foreach (var attribute in attributesToRemove)
						property.CustomAttributes.Remove(attribute);
				}
			}
		}
	}

	//public static void PatchAssembly(AssemblyDefinition assDef)
	//{
	//	var bindableModule = ModuleDefinition.ReadModule(typeof(BindableAttribute).Assembly.Location);
	//	var notifyInterface = bindableModule.ImportReference(typeof(INotifyPropertyChanged));
	//	var pcev = bindableModule.ImportReference(typeof(PropertyChangedEventHandler));
	//
	//	var bMod = ModuleDefinition.ReadModule(typeof(BindableBaseImpl).Module.FullyQualifiedName);
	//	var bindableTemplate = bMod.GetType(typeof(BindableBaseImpl).Name);
	//
	//	var modDef = assDef.Modules[0];
	//	foreach (var typeDef in modDef.Types)
	//	{
	//		var bindableFields = typeDef.Properties.Where(f => f.CustomAttributes.Any(a => a.AttributeType.Name == nameof(BindableAttribute))).ToArray();
	//
	//		if (bindableFields.Count() == 0)
	//		{
	//			continue;
	//		}
	//
	//		var hostType = Type.GetType(typeDef.FullName + ", " + typeDef.Module.Assembly.FullName);
	//
	//		//if (!typeDef.Interfaces.Any(i => i.InterfaceType.Resolve().FullName == typeof(INotifyPropertyChanged).FullName))
	//		if (!(typeof(INotifyPropertyChanged).IsAssignableFrom(hostType)))
	//		{
	//			Debug.LogError($"{typeDef} cannot use [Bindable] as it doesn't implement INotifyPropertyChanged");
	//			continue;
	//		}
	//
	//		{
	//			var propertyChangedEventArgs = assDef.MainModule.ImportReference(typeof(PropertyChangedEventArgs));
	//			var propertyChangedEventArgsCtor = assDef.MainModule.ImportReference(propertyChangedEventArgs.Resolve().Methods.First(m => m.Name == ".ctor"));
	//			var propertyChangedEventHandler = assDef.MainModule.ImportReference(typeof(PropertyChangedEventHandler));
	//			var propertyChangedEventHandlerInvoke = assDef.MainModule.ImportReference(propertyChangedEventHandler.Resolve().Methods.First(m => m.Name == "Invoke"));
	//
	//			foreach (var property in bindableFields)
	//			{
	//				Debug.Log("Injecting in " + property.FullName);
	//
	//				var backingField = property.DeclaringType.Fields.First(f => f.Name == "<" + property.Name + ">k__BackingField");
	//				var propChangedEventHandlerField = property.DeclaringType.Fields.First(f => f.Name == "PropertyChanged");
	//				var setter = property.SetMethod;
	//
	//				MethodReference getDefault;
	//				MethodReference equals;
	//
	//				TypeDefinition comparer = null;
	//					//(TypeDefinition)property.CustomAttributes
	//					//.First(a => a.AttributeType.FullName == typeof(BindableAttribute).FullName)
	//					//.ConstructorArguments[0].Value;
	//
	//				if (comparer == null)
	//				{
	//					var type = assDef.MainModule.ImportReference(typeof(EqualityComparer<>)).Resolve();
	//					var typeReference = (TypeReference)type;
	//					typeReference = typeReference.MakeGenericInstanceType(property.PropertyType);
	//					getDefault = assDef.MainModule.ImportReference(type.Properties.First(m => m.Name == "Default").GetMethod.MakeHostInstanceGeneric(property.PropertyType));
	//					equals = assDef.MainModule.ImportReference(type.Methods.First(m => m.Name == "Equals").MakeHostInstanceGeneric(property.PropertyType));
	//				}
	//				else
	//				{
	//					getDefault = comparer.Methods.First(m => m.Name == "get_Default");
	//					equals = comparer.Methods.First(m => m.Name == "Equals");
	//				}
	//
	//				setter.Body.Instructions.Clear();
	//				setter.Body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
	//				setter.Body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
	//				setter.Body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
	//
	//				var ilGenerator = setter.Body.GetILProcessor();
	//				var start = ilGenerator.Create(OpCodes.Nop);
	//				var ret = ilGenerator.Create(OpCodes.Ret);
	//				var ldarg_0_18 = ilGenerator.Create(OpCodes.Ldarg_0);
	//				var ldarg_0_2b = ilGenerator.Create(OpCodes.Ldarg_0);
	//
	//				setter.Body.Instructions.Add(start);
	//				ilGenerator.InsertAfter(start, ret);
	//
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Call, getDefault));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldarg_0));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldfld, backingField));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldarg_1));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Callvirt, equals));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Stloc_0));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldloc_0));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Brfalse_S, ldarg_0_18));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Br_S, ret));
	//				ilGenerator.InsertBefore(ret, ldarg_0_18);
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldarg_1));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Stfld, backingField));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldarg_0));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldfld, propChangedEventHandlerField));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Dup));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Brtrue_S, ldarg_0_2b));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Pop));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Br_S, ret));
	//				ilGenerator.InsertBefore(ret, ldarg_0_2b);
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Ldstr, property.Name));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Newobj, propertyChangedEventArgsCtor));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Callvirt, propertyChangedEventHandlerInvoke));
	//				ilGenerator.InsertBefore(ret, ilGenerator.Create(OpCodes.Nop));
	//
	//				var attributesToRemove = new List<CustomAttribute>();
	//
	//				foreach (var attribute in property.CustomAttributes.Where(c => c.AttributeType.FullName == typeof(BindableAttribute).FullName))
	//					attributesToRemove.Add(attribute);
	//
	//				foreach (var attribute in attributesToRemove)
	//					property.CustomAttributes.Remove(attribute);
	//			}
	//		}
	//
	//		/*if (!typeDef.Interfaces.Any(i => i.InterfaceType.Resolve().FullName == typeof(INotifyPropertyChanged).FullName))
	//		{
	//			
	//			Debug.Log($"make bindable: {typeDef}");
	//			typeDef.Interfaces.Add(new InterfaceImplementation(notifyInterface));
	//
	//			//typeDef.Events.Add(bindableTemplate.Events[0]);
	//			foreach (var f in bindableTemplate.Fields)
	//			{
	//				typeDef.Fields.Add(f);
	//			}
	//			//foreach (var m in bindableTemplate.Methods)
	//			//{
	//			//	typeDef.Methods.Add(m.Clone());
	//			//}
	//			//typeDef.Events.Add(new EventDefinition("PropertyChanged", EventAttributes.None, pcev));
	//		}
	//		else
	//		{
	//			Debug.Log($"aready bindable: {typeDef}");
	//		}*/
	//	}
	//}

	// https://www.codeproject.com/Articles/671259/Reweaving-IL-code-with-Mono-Cecil
	// https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stloc_0?view=netframework-4.8
	// https://github.com/Fody/PropertyChanging/blob/master/PropertyChanging.Fody/EqualityCheckWeaver.cs
	/*public static void PatchAssembly(AssemblyDefinition assDef)
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
				//prop.CustomAttributes.Remove(prop.CustomAttributes.Single(a => a.AttributeType.Name == nameof(BindableAttribute)));

				var reqBoxing = prop.PropertyType.IsPrimitive;

				var setterName = $"set_{prop.Name}";
				var getterName = $"get_{prop.Name}";
				var setter = typeDef.Methods.Single(m => m.Name == setterName);
				var getter = typeDef.Methods.Single(m => m.Name == getterName);
				il = setter.Body.GetILProcessor();
				var notifyValuesRef = modDef.ImportReference(typeof(BindableBase).GetMethod("_SlotChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));//.Resolve();

				var boxOp = prop.PropertyType.IsPrimitive ? il.Create(OpCodes.Box, modDef.ImportReference(prop.PropertyType)) : il.Create(OpCodes.Nop);

				var ldstr = il.Create(OpCodes.Ldstr, prop.Name);

				// remove the ret instruction to patch the end of the method
				il.Remove(setter.Body.Instructions[setter.Body.Instructions.Count() - 1]);

				var tempVar = new VariableDefinition(prop.PropertyType);
				setter.Body.Variables.Add(tempVar);

				// todo: this is probably flimsy, the stack might get fucked, idk

				// new
				var entryOp = il.Body.Instructions[0];

				il.InsertBefore(entryOp, il.Create(Ldarg_0));
				il.InsertBefore(entryOp, il.Create(Call, getter));
				il.InsertBefore(entryOp, il.Create(Stloc_0));

				il.Emit(Ldarg_0);
				il.Emit(Ldc_I4, fieldIdx);
				il.Emit(Ldloc_0);
				il.Append(boxOp);
				il.Emit(Ldarg_1);
				il.Append(boxOp);
				il.Emit(Call, notifyValuesRef);

				il.Emit(Ret);

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
				nameToIdx.Parameters.Add(new ParameterDefinition("hash", ParameterAttributes.In, modDef.TypeSystem.Int32));
				var vHash = new VariableDefinition(modDef.TypeSystem.Int32);
				nameToIdx.Body.Variables.Add(vHash);
				var vResult = new VariableDefinition(modDef.TypeSystem.Int32);
				nameToIdx.Body.Variables.Add(vResult);

				il = nameToIdx.Body.GetILProcessor();

				var labels = Enumerable.Range(0, fieldIdx + 1).Select(i => il.Create(Ldarg_1)).ToArray();
				var outLabel = labels[fieldIdx] = il.Create(Ldloc, vResult);

				il.Emit(Ldc_I4, -1);
				il.Emit(Stloc, vResult);

				//il.Emit(Ldarg_1);
				//il.Emit(Call, hashRef);
				//il.Emit(Stloc_0);

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
	}*/
}

public static class CecilExtensions
{
	public static MethodDefinition Clone(this MethodDefinition self)
	{
		var d = new MethodDefinition(self.Name, self.Attributes, self.ReturnType);
		return d;
	}

	// https://stackoverflow.com/a/16433452/613130
	public static MethodReference MakeHostInstanceGeneric(this MethodReference self, params TypeReference[] arguments)
	{
		var reference = new MethodReference(self.Name, self.ReturnType, self.DeclaringType.MakeGenericInstanceType(arguments))
		{
			HasThis = self.HasThis,
			ExplicitThis = self.ExplicitThis,
			CallingConvention = self.CallingConvention
		};

		foreach (var parameter in self.Parameters)
			reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

		foreach (var generic_parameter in self.GenericParameters)
			reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

		return reference;
	}
}