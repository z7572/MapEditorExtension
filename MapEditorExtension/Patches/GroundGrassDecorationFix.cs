using HarmonyLib;
using UnityEngine;
using LevelEditor;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class GroundGrassDecorationFix
{
    private static void RotateToIdentity(GameObject obj, out Quaternion originalRot)
    {
        originalRot = Quaternion.identity;
        if (obj != null)
        {
            originalRot = obj.transform.rotation;
            obj.transform.rotation = Quaternion.identity;
        }
    }

    private static void RestoreRotation(GameObject obj, Quaternion originalRot)
    {
        if (obj != null)
        {
            obj.transform.rotation = originalRot;
        }
    }

    // --- Editor 模式修复 (针对 LevelObject) ---

    [HarmonyPatch(typeof(LevelObject), "InitGround")]
    [HarmonyPrefix]
    public static void InitGroundPrefix(LevelObject __instance, out Quaternion __state)
    {
        RotateToIdentity(__instance?.VisibleObject, out __state);
    }

    [HarmonyPatch(typeof(LevelObject), "InitGround")]
    [HarmonyPostfix]
    public static void InitGroundPostfix(LevelObject __instance, Quaternion __state)
    {
        RestoreRotation(__instance?.VisibleObject, __state);
    }

    [HarmonyPatch(typeof(LevelObject), "UpdateGround")]
    [HarmonyPrefix]
    public static void UpdateGroundPrefix(LevelObject __instance, out Quaternion __state)
    {
        RotateToIdentity(__instance?.VisibleObject, out __state);
    }

    [HarmonyPatch(typeof(LevelObject), "UpdateGround")]
    [HarmonyPostfix]
    public static void UpdateGroundPostfix(LevelObject __instance, Quaternion __state)
    {
        RestoreRotation(__instance?.VisibleObject, __state);
    }

    // --- Playtest / 主游戏修复 (针对 ResourcesManager) ---
    // WorkshopLevelManager 在加载关卡时会调用这些方法。
    // 如果不重置旋转，生成的装饰物会基于旋转后的包围盒偏移。

    [HarmonyPatch(typeof(ResourcesManager), "AddNewGround")]
    [HarmonyPrefix]
    public static void AddNewGroundPrefix(GameObject newBlock, out Quaternion __state)
    {
        RotateToIdentity(newBlock, out __state);
    }

    [HarmonyPatch(typeof(ResourcesManager), "AddNewGround")]
    [HarmonyPostfix]
    public static void AddNewGroundPostfix(GameObject newBlock, Quaternion __state)
    {
        RestoreRotation(newBlock, __state);
    }

    [HarmonyPatch(typeof(ResourcesManager), "GenerateRandomThemeProps")]
    [HarmonyPrefix]
    public static void GenerateRandomThemePropsPrefix(GameObject newBlock, out Quaternion __state)
    {
        RotateToIdentity(newBlock, out __state);
    }

    [HarmonyPatch(typeof(ResourcesManager), "GenerateRandomThemeProps")]
    [HarmonyPostfix]
    public static void GenerateRandomThemePropsPostfix(GameObject newBlock, Quaternion __state)
    {
        RestoreRotation(newBlock, __state);
    }
}