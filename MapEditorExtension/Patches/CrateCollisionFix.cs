using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class CrateCollisionFix
{
    public static float RealMapSize { get; private set; } = 10f;

    [HarmonyPatch(typeof(LevelManager), "AddNewPlacedLevelObject")]
    [HarmonyPrefix]
    private static void AddNewPlacedLevelObjectPrefix(LevelObject newObject, ref bool __state)
    {
        __state = false;

        if (newObject == null || newObject.VisibleObject == null) return;

        if (newObject.VisibleObject.GetComponent<IgnorePlayerWhenOffScreen>() != null)
        {
            __state = true;
        }
    }

    [HarmonyPatch(typeof(LevelManager), "AddNewPlacedLevelObject")]
    [HarmonyPostfix]
    private static void AddNewPlacedLevelObjectPostfix(LevelObject newObject, ref bool __state)
    {
        if (__state)
        {
            if (newObject != null && newObject.VisibleObject != null)
            {
                if (newObject.VisibleObject.GetComponent<LandfallYouDidGreat>() == null)
                {
                    newObject.VisibleObject.AddComponent<LandfallYouDidGreat>();
                }
            }
        }
    }

    [HarmonyPatch(typeof(IgnorePlayerWhenOffScreen), "Start")]
    [HarmonyPostfix]
    private static void IgnorePlayerWhenOffScreenStartPostfix(IgnorePlayerWhenOffScreen __instance)
    {
        if (__instance.GetComponent<LandfallYouDidGreat>() == null)
        {
            __instance.gameObject.AddComponent<LandfallYouDidGreat>();
        }
    }

    [HarmonyPatch(typeof(GameManager), "OnMapSizeChanged")]
    [HarmonyPostfix]
    private static void GameManagerOnMapSizeChangedPostfix(float newSize)
    {
        RealMapSize = newSize;
    }

    public static float GetCrateCollisionThreshold()
    {
        if (!ConfigHandler.GetEntry<bool>("FixCrateCollision")) return -11f;

        var currentSize = MapSizeHandler.Instance != null ? MapSizeHandler.Instance.mapSize : RealMapSize;
        return -11f * (currentSize / 10f);
    }

    [HarmonyPatch(typeof(IgnorePlayerWhenOffScreen), "Update")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> IgnorePlayerWhenOffScreenUpdateTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var replacementMethod = AccessTools.Method(typeof(CrateCollisionFix), nameof(GetCrateCollisionThreshold));

        for (int i = 0; i < codes.Count; i++)
        {
            // if (base.transform.position.y < -11f)
            if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == -11f)
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = replacementMethod;
                Debug.Log("[EditorExtension] Replaced hardcoded threshold in IgnorePlayerWhenOffScreen.");
            }
        }

        return codes;
    }
}