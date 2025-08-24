using System;
using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using LevelEditor;

namespace MapEditorExtension
{
    [HarmonyPatch]
    public static class LevelEditorPatch
    {
        [HarmonyPatch(typeof(LevelCreator), "Start")]
        [HarmonyAfter("monky.plugins.QOL")]
        [HarmonyPostfix]
        public static void StartPostfix()
        {
            LevelCreator.Instance.gameObject.AddComponent<ExtensionUI>();
            LevelCreator.Instance.gameObject.AddComponent<HardScaleUI>();
            try
            {
                var cheatTextManager = AccessTools.TypeByName("QOL.CheatTextManager") ?? throw new NullReferenceException();
                var initModTextMethod = AccessTools.TypeByName("QOL.Plugin").GetMethod("InitModText") ?? throw new NullReferenceException();
                var currentOutputMsg = AccessTools.TypeByName("QOL.Helper").GetField("currentOutputMsg") ?? throw new NotSupportedException();
                _ = AccessTools.TypeByName("QOL.Patches.ChatManagerPatches").GetMethod("FindAndRunCommand") ?? throw new NotSupportedException();
                LevelCreator.Instance.gameObject.AddComponent(cheatTextManager);
                initModTextMethod.Invoke(null, null);
                initModTextMethod.Invoke(null, null);
                Helper.isQOLModLoaded = true;
            }
            catch (NullReferenceException)
            {
                Helper.isQOLModLoaded = false;
                Helper.currentOutputMsg = "已禁用命令输入 -- 启用 QOL-Mod (z7572 fork) 以使用命令输入框!";
                Debug.LogWarning(Helper.currentOutputMsg);
            }
            catch (NotSupportedException)
            {
                Helper.isQOLModLoaded = false;
                Helper.currentOutputMsg = "已禁用命令输入 -- 更新 QOL-Mod 至 z7572 fork 以使用命令输入框!";
                Debug.LogWarning(Helper.currentOutputMsg);
            }
        }

        [HarmonyPatch(typeof(LevelCreator), "RotateObject")]
        [HarmonyPrefix]
        public static bool RotateObjectPrefix(GameObject go, ref Vector3 rotate)
        {
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
        public static bool GetMirroredPositionPrefix(LevelCreator __instance, Vector3 pos, ref Vector3 __result,
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
        public static bool GetMirroredRotationPrefix(LevelCreator __instance, bool doMirrorRot, ref Quaternion __result,
            GameObject ___m_BrushObject)
        {
            int num = 0;
            if (doMirrorRot)
            {
                num = 180;
            }
            if (___m_BrushObject)
            {
                var check = ___m_BrushObject.GetComponent<CheckPreRotation>() ?? ___m_BrushObject.AddComponent<CheckPreRotation>();
                var preRot = check.preRotationAngle;
                if (___m_BrushObject.GetComponent<ProprFlipAroundYIndeadOfX>())
                {
                    Vector3 eulerAngles = ___m_BrushObject.transform.rotation.eulerAngles;
                    eulerAngles = new Vector3(eulerAngles.x, eulerAngles.y + num, eulerAngles.z);
                    if (ExtensionUI.isHorizonalMirror && doMirrorRot)
                    {
                        eulerAngles = new Vector3(eulerAngles.x, (num - eulerAngles.y - preRot.y) - preRot.y, eulerAngles.z);
                    }
                    __result = Quaternion.Euler(eulerAngles);
                }
                else
                {
                    Vector3 eulerAngles2 = ___m_BrushObject.transform.rotation.eulerAngles;
                    eulerAngles2 = new Vector3(eulerAngles2.x + num, eulerAngles2.y, eulerAngles2.z);
                    if (ExtensionUI.isHorizonalMirror && doMirrorRot)
                    {
                        eulerAngles2 = new Vector3((num - eulerAngles2.x - preRot.x) - preRot.x, eulerAngles2.y, eulerAngles2.z);
                    }
                    __result = Quaternion.Euler(eulerAngles2);
                }
            }
            else if (___m_BrushObject)
            {
                __result = ___m_BrushObject.transform.rotation;
            }
            return false;
        }

        [HarmonyPatch(typeof(MapSizeHandler), "Update")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MapSizeHandlerUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
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
                else if (transform.GetChild(0).name == "Castle_Platform1 (1)") // HINGE STEP
                {
                    gameObject.AddComponent<ProprFlipAroundYIndeadOfX>();
                }
            }
        }
    }

}
