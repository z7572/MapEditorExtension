using HarmonyLib;
using LevelEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TMPro;
using UnityEngine;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class Patches
{
    [HarmonyPatch(typeof(LevelCreator), "Start")]
    [HarmonyPostfix]
    private static void LevelCreatorStartPostfix()
    {
        LevelCreator.Instance.gameObject.AddComponent<ExtensionUI>();
        LevelCreator.Instance.gameObject.AddComponent<HardScaleUI>();
    }

    [HarmonyPatch(typeof(LevelManager), "GetLevelObject")]
    [HarmonyPostfix]
    public static void GetLevelObjectPostfix(SaveableLevelObject obj, ref LevelObject __result)
    {
        if (__result == null || __result.VisibleObject == null) return;

        float targetZ = obj.ScaleX;
        float targetY = obj.ScaleY;
        Vector3 finalScale = new Vector3(1f, targetY, targetZ);

        __result.VisibleObject.transform.localScale = finalScale;

        __result.Scale = new Vector2(targetZ, targetY);

        // Explosive Barrel
        var codeAnim = __result.VisibleObject.GetComponent<CodeAnimation>();

        if (codeAnim != null && codeAnim.animationType == CodeAnimation.AnimationType.Scale)
        {
            var traverse = Traverse.Create(codeAnim);

            traverse.Field("baseX").SetValue(finalScale.x);
            traverse.Field("baseY").SetValue(finalScale.y);
            traverse.Field("baseZ").SetValue(finalScale.z);
        }
    }

    [HarmonyPatch(typeof(LevelCreator), "RotateObject")]
    [HarmonyPrefix]
    private static bool RotateObjectPrefix(GameObject go, ref Vector3 rotate)
    {
        if (go == null) return false;
        if (rotate.x > 0f && rotate.x <= 90f && rotate.y == 0f && rotate.z == 0f)
        {
            var eulerAngles = new Vector3(ExtensionUI.rotationAngle, 0f, 0f);
            if (ExtensionUI.canReverseRotate && Input.GetKey(KeyCode.LeftShift))
            {
                eulerAngles = -eulerAngles;
            }
            go.transform.Rotate(eulerAngles);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(LevelCreator), "GetMirroredPosition")]
    [HarmonyPrefix]
    private static bool GetMirroredPositionPrefix(LevelCreator __instance, Vector3 pos, ref Vector3 __result,
        MapSpace ___m_MapSpace, GameObject ___m_MirrorBrushObject)
    {
        Vector3 mirroredPosition = ___m_MapSpace.GetMirroredPosition(pos);
        if (___m_MirrorBrushObject)
        {
            if (!ExtensionUI.canStackPlace && Mathf.Abs(___m_MapSpace.GetDistanceToMiddle(mirroredPosition)) < 0.5f)
            {
                ___m_MirrorBrushObject.SetActive(false);
            }
            else
            {
                ___m_MirrorBrushObject.SetActive(true);
            }
        }
        __result = mirroredPosition;
        return false;
    }

    [HarmonyPatch(typeof(LevelCreator), "GetMirroredRotation")]
    [HarmonyPrefix]
    private static bool GetMirroredRotationPrefix(LevelCreator __instance, bool doMirrorRot, ref Quaternion __result,
        GameObject ___m_BrushObject)
    {
        __result = Helper.GetMirroredRotation(doMirrorRot, ___m_BrushObject);
        return false;
    }

    // [重构] 拦截拖动检测
    // 1. 手动实现逻辑以允许 GROUND 被拖动
    // 2. 启动原版主物体拖动协程
    // 3. 启动 ExtensionUI 镜像位置跟随协程
    [HarmonyPatch(typeof(LevelCreator), "DidHitPlaceable")]
    [HarmonyPrefix]
    private static bool DidHitPlaceablePrefix(LevelCreator __instance, ref bool __result)
    {
        var traverse = Traverse.Create(__instance);
        var allowDrag = traverse.Field("m_AllowDragPlacedObjects").GetValue<bool>();
        if (!allowDrag) return true;

        if (Helper.CastRaycastFromMouse(__instance, out var hit))
        {
            var hitObj = hit.collider.gameObject.transform.root.gameObject;
            var objName = hitObj.name.Replace("(Clone)", string.Empty);

            var isPlaceable = ResourcesManager.Instance.GetObjectByName(objName, false) != null;
            if (objName.Contains("Gun") || isPlaceable)
            {
                var isMirroring = LevelToolsHandler.Instance.IsMirroring;
                var isMirrorDrag = ExtensionUI.isMirrorDrag;
                var shouldUnbind = !(isMirroring && isMirrorDrag);

                var lm = LevelManager.Instance;
                var getLO = AccessTools.Method(typeof(LevelManager), "GetLevelObjectFromGameObject");
                var getWO = AccessTools.Method(typeof(LevelManager), "GetLevelWeaponObjectFromGameObject");

                var lo = (LevelObject)getLO.Invoke(lm, [hitObj]);
                var wo = (WeaponObject)getWO.Invoke(lm, [hitObj]);

                if (shouldUnbind)
                {
                    if (lo != null && lo.HasMirrorObject())
                    {
                        var traverseMirror = Traverse.Create(lo).Property("MirrorObject");
                        traverseMirror.Property("MirrorObject").SetValue(null);
                        traverseMirror.SetValue(null);
                        Helper.SendModOutput($"Unbound Mirror Object: {lo.Id}", Helper.LogType.Warning);
                    }
                    else if (wo != null && wo.HasMirrorObject())
                    {
                        var traverseMirror = Traverse.Create(wo).Property("MirrorObject");
                        traverseMirror.Property("MirrorObject").SetValue(null);
                        traverseMirror.SetValue(null);
                        Helper.SendModOutput($"Unbound Mirror Object: {wo.WeaponName}", Helper.LogType.Warning);
                    }
                }

                if (!ExtensionUI.isMirrorDrag) return true;

                bool isGround = objName.Contains("GROUND");
                bool canFlipOrRotate = isPlaceable && !isGround;

                // 反射调用启动原版拖动协程
                // BeginMoveTransform(Transform moveTransform, bool canFlipOrRotate, bool isPlacedObject, bool preventBlockablePositions)
                var beginMoveMethod = AccessTools.Method(typeof(LevelCreator), "BeginMoveTransform");
                __instance.StartCoroutine((IEnumerator)beginMoveMethod.Invoke(__instance, [hitObj.transform, canFlipOrRotate, true, true]));

                var mainTrans = hitObj.transform;
                Transform mirrorTrans = null;

                if (lo != null && lo.HasMirrorObject()) mirrorTrans = lo.MirrorObject.VisibleObject.transform;
                else if (wo != null && wo.HasMirrorObject()) mirrorTrans = wo.MirrorObject.VisibleObject.transform;

                // 如果有镜像，启动跟随协程
                if (mirrorTrans)
                {
                    ExtensionUI.Instance.StartCoroutine(ExtensionUI.Instance.DragMirrorObject(mainTrans, mirrorTrans));
                }

                __result = true;
                return false;
            }
        }
        return true;
    }

    // [新增] 在拖动旋转时同步镜像物体
    [HarmonyPatch(typeof(LevelCreator), "HandleFlipOrRotateDraggedObject")]
    [HarmonyPostfix]
    private static void HandleFlipOrRotateDraggedObjectPostfix(Transform transform)
    {
        if (ExtensionUI.isMirrorDrag)
        {
            var lm = LevelManager.Instance;
            var getLO = AccessTools.Method(typeof(LevelManager), "GetLevelObjectFromGameObject");
            var lo = (LevelObject)getLO.Invoke(lm, [transform.gameObject]);

            // 只有未解绑时，才有 MirrorObject
            if (lo != null && lo.HasMirrorObject())
            {
                var mirrorObj = lo.MirrorObject.VisibleObject;

                bool doMirrorRot = LevelToolsHandler.Instance.IsMirroringRotation;
                mirrorObj.transform.rotation = Helper.GetMirroredRotation(doMirrorRot, null, transform.gameObject);

                LevelManager.Instance.UpdatePlacedObject(mirrorObj);
            }
        }
    }

    [HarmonyPatch(typeof(MapSizeHandler), "Update")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapSizeHandlerUpdateTranspilerA(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_0
                && codes[i + 1].opcode == OpCodes.Ldc_R4 && (float)codes[i + 1].operand == 5f
                && codes[i + 2].opcode == OpCodes.Ldc_R4 && (float)codes[i + 2].operand == 15f)
            {
                var sizeMin = ConfigHandler.GetEntry<float>("MapSizeMin");
                var sizeMax = ConfigHandler.GetEntry<float>("MapSizeMax");
                codes[i + 1].operand = sizeMin;
                codes[i + 2].operand = sizeMax;
                Debug.Log($"[EditorExtension] Changed map size limit to {sizeMin} ~ {sizeMax} !");
                break;
            }
        }
        return codes.AsEnumerable();
    }
}
