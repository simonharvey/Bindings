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
		//Debug.Log($"CompilationPipeline_assemblyCompilationStarted {assPath}");
	}

	private static void CompilationPipeline_assemblyCompilationFinished(string assPath, CompilerMessage[] arg2)
	{
		// if an assembly has finished compiling and is bindable, need to process

		//Debug.Log($"CompilationPipeline_assemblyCompilationFinished {assPath}");

		DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
		resolver.AddSearchDirectory(Directory.GetParent(assPath).FullName);
		
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
					var setter = property.SetMethod;
					var backingField = property.DeclaringType.Fields.First(f => f.Name == "<" + property.Name + ">k__BackingField");

					FieldDefinition propChangedEventHandlerField = null;
					
					try
					{
						propChangedEventHandlerField = typeDef.Fields.First(f => f.Name == "PropertyChanged");
					}
					catch (InvalidOperationException _)
					{
						//Debug.Log($"INotifyPropertyChanged but not prop: {property.FullName}");
					}

					MethodReference getDefault;
					MethodReference equals;

					TypeDefinition comparer = null;

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