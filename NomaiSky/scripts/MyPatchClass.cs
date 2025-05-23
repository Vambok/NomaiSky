using HarmonyLib;
using NomaiSky;
using System;
using System.Collections.Generic;

[HarmonyPatch]
public static class MyPatchClass
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ReferenceFrame), nameof(ReferenceFrame.GetHUDDisplayName))]
    static bool GetHUDDisplayName_Prefix(ReferenceFrame __instance, ref string __result)
    {
        MVBGalacticMap mapParameters = __instance._attachedOWRigidbody.GetComponent<MVBGalacticMap>();
        if (mapParameters != null)
        {
            __result = mapParameters.mapName;
            return false;
        }
        return true;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ReferenceFrameTracker), nameof(ReferenceFrameTracker.FindReferenceFrameInMapView))]
    static IEnumerable<CodeInstruction> FindReferenceFrameInMapView_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(i => i.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && Convert.ToInt32(i.operand) == 100000)
        ).Repeat(match => match.SetOperandAndAdvance(7000000f)).InstructionEnumeration();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MapController), nameof(MapController.LateUpdate))]
    static IEnumerable<CodeInstruction> LateUpdate_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(i => i.opcode == System.Reflection.Emit.OpCodes.Ldfld && ((System.Reflection.FieldInfo)i.operand).Name == "_zoomSpeed")
        ).Advance(1).InsertAndAdvance(
            new CodeInstruction(System.Reflection.Emit.OpCodes.Mul),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ldc_R4, 4f),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Mul),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0),
            CodeInstruction.LoadField(typeof(MapController), "_zoom"),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0),
            CodeInstruction.LoadField(typeof(MapController), "_maxZoomDistance"),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Div)
        ).InstructionEnumeration();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.IsTimeFlowing))]
    static bool IsTimeFlowing_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.Start))]
    static void Start_Postfix()
    {
        TimeLoop._isTimeFlowing = false;
    }
}