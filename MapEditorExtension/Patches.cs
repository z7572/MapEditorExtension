using HarmonyLib;
using LevelEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnityEngine;

namespace MapEditorExtension
{
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
            if (!ExtensionUI.canStackPlace && Mathf.Abs(___m_MapSpace.GetDistanceToMiddle(mirroredPosition)) < 0.5f)
            {
                ___m_MirrorBrushObject?.SetActive(false);
            }
            else
            {
                ___m_MirrorBrushObject?.SetActive(true);
            }
            __result = mirroredPosition;
            return false;
        }

        [HarmonyPatch(typeof(LevelCreator), "GetMirroredRotation")]
        [HarmonyPrefix]
        private static bool GetMirroredRotationPrefix(LevelCreator __instance, bool doMirrorRot, ref Quaternion __result,
            GameObject ___m_BrushObject)
        {
            int num = 0;
            if (doMirrorRot)
            {
                num = 180;
            }
            __result = Quaternion.identity;
            if (___m_BrushObject)
            {
                if (___m_BrushObject.GetComponent<CheckPreRotation>() == null)
                {
                    ___m_BrushObject.AddComponent<CheckPreRotation>();
                }
                var check = ___m_BrushObject.GetComponent<CheckPreRotation>();
                var preRot = check.preRotationAngle;
                if (___m_BrushObject.GetComponent<ProprFlipAroundYIndeadOfX>())
                {
                    Vector3 eulerAngles = ___m_BrushObject.transform.rotation.eulerAngles;
                    eulerAngles = new Vector3(eulerAngles.x, eulerAngles.y + num, eulerAngles.z);
                    if (ExtensionUI.isHorizonalMirror)
                    {
                        eulerAngles = new Vector3(eulerAngles.x, (num - eulerAngles.y - preRot.y) - preRot.y, eulerAngles.z);
                    }
                    __result = Quaternion.Euler(eulerAngles);
                }
                else
                {
                    Vector3 eulerAngles2 = ___m_BrushObject.transform.rotation.eulerAngles;
                    eulerAngles2 = new Vector3(eulerAngles2.x + num, eulerAngles2.y, eulerAngles2.z);
                    if (ExtensionUI.isHorizonalMirror)
                    {
                        eulerAngles2 = new Vector3((num - eulerAngles2.x - preRot.x) - preRot.x, eulerAngles2.y, eulerAngles2.z);
                    }
                    __result = Quaternion.Euler(eulerAngles2);
                }
            }
            else if (___m_BrushObject) // Source code is this shit dead code
            {
                __result = ___m_BrushObject.transform.rotation;
            }
            return false;
        }

        [HarmonyPatch(typeof(MapSizeHandler), "Update")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> MapSizeHandlerUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
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
                    Debug.Log($"[MapEditorExtension] Changed map size limit to {sizeMin} ~ {sizeMax} !");
                    break;
                }
            }
            return codes.AsEnumerable();
        }

        private class CheckPreRotation : MonoBehaviour
        {
            public Vector3 preRotationAngle = Vector3.zero;

            private void Awake()
            {
                if (transform.rotation.x != 0f) // ICE, SMALL ICE
                {
                    preRotationAngle = transform.rotation.eulerAngles;
                }
                else if (transform.GetChild(0).name == "SnakeBarrel_LevelEditor") // SNAKE BARREL
                {
                    preRotationAngle = new Vector3(90f, 0f, 0f);
                }
                else if (transform.GetChild(0).name == "Spike") // SPIKE
                {
                    preRotationAngle = new Vector3(90f, 0f, 0f);
                }
                //else if (transform.GetChild(0).name.Contains("Castle_Platform")) // HINGE STEP
                //{
                //    
                //}
            }
        }


        [HarmonyPatch]
        public static class RotationFixPatches
        {
            /// <summary>
            /// 核心算法：将带有 Z 轴旋转的欧拉角转换为 Z=0 的等效形式
            /// 解决 Unity 欧拉角 Z 轴翻转导致存档丢失数据的问题
            /// </summary>
            private static Vector3 SanitizeEuler(Vector3 inputEuler)
            {
                // 容差，防止浮点误差
                if (Mathf.Abs(inputEuler.z) < 1f) return inputEuler;

                // 数学转换原理: 绕X转x，再绕Y转180，再绕Z转180 
                // 等价于 ==> 绕X转(180-x)，Y转0，Z转0
                // 这样就把 Z 轴消掉了
                float newX = 180f - inputEuler.x;
                float newY = inputEuler.y + 180f;

                // 规范化角度到 0-360
                newX = (newX % 360f + 360f) % 360f;
                newY = (newY % 360f + 360f) % 360f;

                return new Vector3(newX, newY, 0f);
            }

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
                Vector3 cleanRotation = SanitizeEuler(rawRotation);

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
                    Vector3 cleanRotation = SanitizeEuler(rawRotation);

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
                    Vector3 clean = SanitizeEuler(__instance.Rotation);

                    // 修正即将写入存档的数据
                    __result.RotationX = clean.x;
                    __result.RotationY = clean.y;
                }
            }
        }
    }
}
