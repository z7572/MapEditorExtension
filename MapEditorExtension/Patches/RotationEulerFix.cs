using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class RotationEulerFix
{
    // ========================================================================
    // Patch 1: 修复新放置物体的旋转 (LevelObject 初始化时)
    // 拦截目标: LevelObject.InitPosRotScale
    // ========================================================================
    [HarmonyPatch(typeof(LevelObject), "InitPosRotScale")]
    [HarmonyPostfix]
    public static void InitPosRotScale_Postfix(LevelObject __instance)
    {
        if (__instance == null || __instance.VisibleObject == null) return;

        // 【关键点】不要信任 __instance.Rotation，因为它可能已经被原游戏的硬编码逻辑改错了(-180)
        // 我们直接从 Unity 的 Transform 组件重新读取最原始的欧拉角
        Vector3 rawRotation = __instance.VisibleObject.transform.rotation.eulerAngles;

        // 使用我们的算法清洗 Z 轴
        Vector3 cleanRotation = Helper.SanitizeEuler(rawRotation);

        // 强制覆盖 LevelObject 中的数据
        // 这样原游戏那句 if (z==180 && y==180) 造成的错误就会被我们修正
        __instance.Rotation = cleanRotation;
    }

    // ========================================================================
    // Patch 2: 修复移动/旋转工具操作后的物体 (LevelManager 更新时)
    // 拦截目标: LevelManager.UpdatePlacedObject
    // ========================================================================
    [HarmonyPatch(typeof(LevelManager), "UpdatePlacedObject")]
    [HarmonyPostfix]
    public static void UpdatePlacedObject_Postfix(LevelManager __instance, GameObject objectToEdit, bool __result)
    {
        if (!__result || objectToEdit == null) return;

        // 获取对应的 LevelObject
        var getLevelObjectFromGameObjectMethod = AccessTools.Method(typeof(LevelManager), "GetLevelObjectFromGameObject");
        LevelObject levelObj = (LevelObject)getLevelObjectFromGameObjectMethod.Invoke(__instance, [objectToEdit]);
        if (levelObj != null)
        {
            // 同样，直接从 Transform 读取，绕过 LevelManager 里的错误逻辑
            Vector3 rawRotation = objectToEdit.transform.rotation.eulerAngles;
            Vector3 cleanRotation = Helper.SanitizeEuler(rawRotation);

            // 强制修正
            levelObj.Rotation = cleanRotation;
        }
    }

    // ========================================================================
    // Patch 3: 存档时的最后一道防线
    // 拦截目标: LevelObject.GetSaveableObject
    // ========================================================================
    [HarmonyPatch(typeof(LevelObject), "GetSaveableObject")]
    [HarmonyPostfix]
    public static void GetSaveableObject_Postfix(LevelObject __instance, ref SaveableLevelObject __result)
    {
        if (__result == null) return;

        // 检查当前内存中的旋转数据是否依然带有 Z 轴 (作为双重保险)
        if (Mathf.Abs(__instance.Rotation.z) > 1f)
        {
            Vector3 clean = Helper.SanitizeEuler(__instance.Rotation);

            // 修正即将写入存档的数据
            __result.RotationX = clean.x;
            __result.RotationY = clean.y;
        }
    }
}
