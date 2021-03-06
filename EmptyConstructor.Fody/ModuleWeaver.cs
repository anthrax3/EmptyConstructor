﻿using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

public partial class ModuleWeaver:BaseModuleWeaver
{
    public MethodAttributes Visibility = MethodAttributes.Public;
    public bool MakeExistingEmptyConstructorsVisible;

    public override void Execute()
    {
        ReadConfig();
        ProcessIncludesExcludes();
        var queue = new Queue<TypeDefinition>(ModuleDefinition.GetTypes());

        var processed = new Dictionary<TypeDefinition, MethodReference>();
        var external = new Dictionary<TypeReference, MethodReference>();
        while (queue.Count != 0)
        {
            var type = queue.Dequeue();
            if (!type.IsClass)
            {
                continue;
            }
            if (type.IsValueType)
            {
                continue;
            }
            if (type.IsDelegate())
            {
                continue;
            }
            if (type.IsStaticClass())
            {
                continue;
            }
            if (Visibility == MethodAttributes.Family && type.IsSealed)
            {
                continue;
            }

            var baseType = type.BaseType;
            if (baseType == null)
            {
                continue;
            }

            var typeEmptyConstructor = type.GetEmptyConstructor();

            if (typeEmptyConstructor != null)
            {
                MakeConstructorVisibleIfConfiguredAndNecessary(typeEmptyConstructor);

                processed.Add(type, typeEmptyConstructor);
                continue;
            }

            if (!ShouldIncludeType(type))
            {
                continue;
            }
            MethodReference baseEmptyConstructor;
            if (baseType is TypeDefinition baseTypeDefinition)
            {
                if (!processed.TryGetValue(baseTypeDefinition, out baseEmptyConstructor))
                {
                    queue.Enqueue(type);
                    continue;
                }
            }
            else
            {
                if (!external.TryGetValue(baseType, out baseEmptyConstructor))
                {
                    var emptyConstructor = baseType.Resolve().GetEmptyConstructor();
                    if (emptyConstructor != null)
                    {
                        baseEmptyConstructor = ModuleDefinition.ImportReference(emptyConstructor);
                    }
                    external.Add(baseType, baseEmptyConstructor);
                }
            }

            if (baseEmptyConstructor != null)
            {
                if (baseEmptyConstructor.Resolve().IsPrivate)
                {
                    processed.Add(type, null);
                    LogWarning($"Could not inject empty constructor in {type.FullName} because the base class has a private parameterless constructor");
                    continue;
                }
                var constructor = AddEmptyConstructor(type);
                processed.Add(type, constructor);
            }
            else
            {
                processed.Add(type, null);
            }
        }
    }

    public override bool ShouldCleanReference => true;

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        return Enumerable.Empty<string>();
    }

    MethodDefinition AddEmptyConstructor(TypeDefinition type)
    {
        LogDebug("Processing " + type.FullName);
        var methodAttributes = Visibility | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var method = new MethodDefinition(".ctor", methodAttributes, ModuleDefinition.TypeSystem.Void);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        var methodReference = new MethodReference(".ctor",ModuleDefinition.TypeSystem.Void,type.BaseType){HasThis = true};
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, methodReference));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        type.Methods.Add(method);
        return method;
    }

    void MakeConstructorVisibleIfConfiguredAndNecessary(MethodDefinition typeEmptyConstructor)
    {
        if (!MakeExistingEmptyConstructorsVisible)
        {
            return;
        }
        if (typeEmptyConstructor.IsPublic)
        {
            return;
        }
        if (typeEmptyConstructor.DeclaringType.IsAbstract)
        {
            return;
        }
        if (typeEmptyConstructor.IsFamily)
        {
            if (Visibility == MethodAttributes.Public)
            {
                typeEmptyConstructor.IsFamily = false;
                typeEmptyConstructor.IsPublic = true;
            }
            return;
        }
        if (typeEmptyConstructor.IsPrivate)
        {
            typeEmptyConstructor.IsPrivate = false;
            typeEmptyConstructor.Attributes = typeEmptyConstructor.Attributes | Visibility;
        }
    }
}