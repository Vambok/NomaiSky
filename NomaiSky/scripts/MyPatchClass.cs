using HarmonyLib;//
using NewHorizons.OtherMods.CustomShipLogModes;//
using System;//
using System.Collections.Generic;//

namespace NomaiSky;

[HarmonyPatch]
public static class MyPatchClass {
    //NMS version
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.FadeInTitleLogo))]
    public static void FadeInTitleLogo(TitleScreenManager __instance) {
        __instance._gameVersionTextDisplay.text += $"{Environment.NewLine}Nomai's Sky : {NomaiSky.version}";
    }
    //Ship warp drive
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomShipLogModesHandler), nameof(CustomShipLogModesHandler.AddInterstellarMode))]
    static bool AddInterstellarMode_Prefix() {//That's not the correct ship warp drive! (map view is)
        return false;
    }
    //No supernova
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.IsTimeFlowing))]
    static bool IsTimeFlowing_Prefix(ref bool __result) {
        __result = true;
        return false;
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.Start))]
    static void Start_Postfix() {
        TimeLoop._isTimeFlowing = false;
    }
    //Display star name on map
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ReferenceFrame), nameof(ReferenceFrame.GetHUDDisplayName))]
    static bool GetHUDDisplayName_Prefix(ReferenceFrame __instance, ref string __result) {
        MVBGalacticMap mapParameters = __instance._attachedOWRigidbody.GetComponent<MVBGalacticMap>();
        if (mapParameters != null) {
            __result = mapParameters.mapName;
            return false;
        }
        return true;
    }
    //Increase max star system size
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ReferenceFrameTracker), nameof(ReferenceFrameTracker.FindReferenceFrameInMapView))]
    static IEnumerable<CodeInstruction> FindReferenceFrameInMapView_Transpiler(IEnumerable<CodeInstruction> instructions) {
        return new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(i => i.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && Convert.ToInt32(i.operand) == 100000)
        ).Repeat(match => match.SetOperandAndAdvance(7000000f)).InstructionEnumeration();
    }
    //Tweak zoom speed (zoom dependant)
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MapController), nameof(MapController.LateUpdate))]
    static IEnumerable<CodeInstruction> LateUpdate_Transpiler(IEnumerable<CodeInstruction> instructions) {
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
    //No zoom on star click
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapController), nameof(MapController.OnTargetReferenceFrame))]
    static void OnTargetReferenceFrame_Postfix(MapController __instance) {
        if(__instance._targetTransform != null && __instance._targetTransform.GetComponent<MVBGalacticMap>() != null) __instance._lockedToTargetTransform = false;
    }
}