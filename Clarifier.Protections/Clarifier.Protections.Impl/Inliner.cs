﻿using Clarifier.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Clarifier.Protection.Impl
{
    /// <summary>
    /// This class manage the "reference proxy" protection.
    /// This protection is based on creating proxy function that should hide
    /// the real function.
    /// Approach here is based on replacing all the calls to these proxy methods
    /// with calls to the real method.
    /// </summary>
    public class Inliner
    {
        BasicStaticProtection staticProtectionsManager = new BasicStaticProtection();

        Dictionary<MethodDef,List<InstructionGroup>> referenceProxyMethods = new Dictionary<MethodDef, List<InstructionGroup>>();

        public void PerformRemoval(ClarifierContext ctx)
        {
            foreach (var method in ctx.CurrentModule.GetMethods())
            {
                if (referenceProxyMethods.Keys.Contains(method))
                    continue;
                foreach (var instruction in method.GetInstructions())
                {
                    if (instruction.OpCode != OpCodes.Call)
                        continue;

                    MethodDef targetMethod = null;
                    if (instruction.Operand is MethodDef)
                    {
                        targetMethod = instruction.Operand as MethodDef;
                    }
                    else if (instruction.Operand is MethodSpec)
                    {
                        MethodSpec tempMethod = instruction.Operand as MethodSpec;
                        targetMethod = tempMethod.Method as MethodDef;
                    }
                    else if(instruction.Operand is MemberRef)
                    {
                        continue;
                    }

                    Debug.Assert(targetMethod != null);

                    List<InstructionGroup> currentInstructionGroup;
                    if (referenceProxyMethods.TryGetValue(targetMethod, out currentInstructionGroup))
                    {
                        int callIndex = currentInstructionGroup.Where(x => x.Name == "Call").First().FoundInstructions[0];
                        instruction.Operand = targetMethod.Body.Instructions[callIndex].Operand;
                    }
                }
            }
        }
        public double PerformIdentification(ClarifierContext ctx)
        {
            foreach (var method in ctx.CurrentModule.GetMethods())
            {
                MacroBodyComparison macroBodyComparison = new MacroBodyComparison()
                {
                    InstructionGroups = new MacroContainer().CallMacro
                };

                if (!method.HasBody)
                    continue;

                if (macroBodyComparison.PerformComparison(method))
                    referenceProxyMethods[method] = macroBodyComparison.InstructionGroups;
            }
            if (referenceProxyMethods.Count != 0)
                return 1.0;
            return 0.0;
        }

        public void Initialize()
        {
        }
    }
}
