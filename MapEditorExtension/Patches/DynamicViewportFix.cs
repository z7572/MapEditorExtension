using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class DynamicViewportFix
{
    [HarmonyPatch(typeof(AspectFix), "Start")]
    [HarmonyPostfix]
    private static void AspectFixStartPostfix(AspectFix __instance, Camera ___cam)
    {
        if (LevelCreator.Instance != null && GameManager.Instance == null)
        {
            if (___cam != Camera.main) return;

            if (__instance.gameObject.GetComponent<DynamicViewportManager>() == null)
            {
                __instance.gameObject.AddComponent<DynamicViewportManager>();
            }
        }
    }

    [HarmonyPatch(typeof(MapSizeHandler), "Update")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapSizeHandlerUpdateTranspilerB(IEnumerable<CodeInstruction> instructions)
    {
        var originalMethod = AccessTools.Method(typeof(Input), "GetAxis", [typeof(string)]);
        var replacementMethod = AccessTools.Method(typeof(DynamicViewportFix), nameof(GetScrollWheelWithoutShift));

        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++)
        {
            //float num = this.mapSize;
            //num -= Input.GetAxis("Mouse ScrollWheel") * 10f;
            if (codes[i].opcode == OpCodes.Call && codes[i].operand == originalMethod &&
                i > 0 && codes[i - 1].opcode == OpCodes.Ldstr && (string)codes[i - 1].operand == "Mouse ScrollWheel")
            {
                // num -= Patches.GetSmartScrollWheel("Mouse ScrollWheel") * 10f;
                codes[i].operand = replacementMethod;
                break;
            }
        }
        return codes;
    }

    public static float GetScrollWheelWithoutShift(string axisName)
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            return 0f;
        }
        return Input.GetAxis(axisName);
    }

    // Raycast: 何异位?
    [HarmonyPatch(typeof(LevelCreator), "Update")]
    [HarmonyPatch(typeof(HardScaleUI), "Update")]
    [HarmonyPrefix]
    private static void CameraUpdatePrefix()
    {
        if (DynamicViewportManager.Instance != null)
        {
            DynamicViewportManager.Instance.TryUpdateCamera();
        }
    }

    [HarmonyPatch(typeof(AspectFix), "UpdateSize")]
    [HarmonyPrefix]
    private static bool AspectFixUpdateSizePrefix()
    {
        if (LevelCreator.Instance != null && !ExtensionUI.Instance.viewportFollowMap && !WorkshopStateHandler.IsPlayTestingMode)
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(LevelCreator), "OnPlayTestStarted")]
    [HarmonyPrefix]
    private static void LevelCreatorOnPlayTestStartedPrefix()
    {
        if (DynamicViewportManager.Instance != null)
        {
            DynamicViewportManager.Instance.OnPlayTestStarted();
        }
    }

    [HarmonyPatch(typeof(LevelCreator), "OnPlayTestEnded")]
    [HarmonyPostfix]
    private static void LevelCreatorStopPlayTestingPostfix()
    {
        if (DynamicViewportManager.Instance != null)
        {
            DynamicViewportManager.Instance.OnPlayTestEnded();
        }
    }

}
