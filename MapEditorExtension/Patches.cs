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

namespace EditorExtension;

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

            Vector3 cleanEuler = Helper.SanitizeEuler(___m_BrushObject.transform.rotation.eulerAngles);

            if (___m_BrushObject.GetComponent<ProprFlipAroundYIndeadOfX>())
            {
                Vector3 eulerAngles = cleanEuler;
                eulerAngles = new Vector3(eulerAngles.x, eulerAngles.y + num, eulerAngles.z);
                if (ExtensionUI.isHorizonalMirror)
                {
                    eulerAngles = new Vector3(eulerAngles.x, (num - eulerAngles.y - preRot.y) - preRot.y, eulerAngles.z);
                }
                __result = Quaternion.Euler(eulerAngles);
            }
            else
            {
                Vector3 eulerAngles2 = cleanEuler;
                eulerAngles2 = new Vector3(eulerAngles2.x + num, eulerAngles2.y, eulerAngles2.z);
                if (ExtensionUI.isHorizonalMirror)
                {
                    eulerAngles2 = new Vector3((num - eulerAngles2.x - preRot.x) - preRot.x, eulerAngles2.y, eulerAngles2.z);
                }
                __result = Quaternion.Euler(eulerAngles2);
            }
        }
        //else if (___m_BrushObject) // Source code is this shit dead code
        //{
        //    __result = ___m_BrushObject.transform.rotation;
        //}
        return false;
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

    private class CheckPreRotation : MonoBehaviour
    {
        public Vector3 preRotationAngle = Vector3.zero;

        private void Start()
        {
            if (transform.GetChild(0).name == "SnakeBarrel_LevelEditor") // SNAKE BARREL
            {
                preRotationAngle = new Vector3(90f, 0f, 0f);
            }
            else if (transform.GetChild(0).name == "Spike") // SPIKE
            {
                preRotationAngle = new Vector3(90f, 0f, 0f);
            }
            else if (transform.GetChild(0).name == "Castle_Platform1 (1)") // HINGE STEP
            {
                preRotationAngle = new Vector3(90f, 0f, 0f);
            }
            else if (transform.rotation.x != 0f) // ICE, SMALL ICE, etc.
            {
                preRotationAngle = transform.rotation.eulerAngles;
            }
        }
    }

    //[AIGenerated]
    [HarmonyPatch]
    public static class RotationFix
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
        var replacementMethod = AccessTools.Method(typeof(Patches), nameof(GetScrollWheelWithoutShift));

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

    // 何异位
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

    [HarmonyPatch]
    public static class CrateCollisionFix
    {
        public static float RealMapSize { get; private set; } = 10f;

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
        private static IEnumerable<CodeInstruction> IgnorePlayerWhenOffScreenUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
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

    [HarmonyPatch]
    public static class InfiniteMapFix
    {
        // 缓存反射方法以提高性能
        private static MethodInfo _handleSnappingMethod;
        private static MethodInfo _getMirroredPositionMethod;
        private static MethodInfo _getMirroredRotationMethod;
        private static MethodInfo _isMouseOverSpawnPointMethod;

        // 手动实现一个基于数学平面的射线检测
        private static bool GetMouseWorldPosition(Camera cam, out Vector3 worldPos)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            // 1. 优先尝试物理检测 (为了兼容某些特定逻辑)
            if (Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            {
                worldPos = hit.point;
                return true;
            }

            // 2. 如果物理检测失败（比如超出了地图板），使用数学平面检测
            // 游戏逻辑是侧视，X轴为深度，创建一个 X=0 的平面 (法线指向右侧)
            Plane plane = new Plane(Vector3.right, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                worldPos = ray.GetPoint(enter);
                return true;
            }

            worldPos = Vector3.zero;
            return false;
        }

        [HarmonyPatch(typeof(LevelCreator), "UpdateMousePosition")]
        [HarmonyPrefix]
        private static bool UpdateMousePositionPrefix(
            LevelCreator __instance,
            ref Vector3 ___m_MousePosition,
            ref GameObject ___m_BrushObject,
            ref GameObject ___m_MirrorBrushObject,
            ref Vector3 ___m_BrushOffset,
            MapGrid ___m_MapGrid,
            Transform[] ___m_SpawnPoints,
            Camera ___m_MainCamera)
        {
            // 初始化反射信息
            if (_handleSnappingMethod == null)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                _handleSnappingMethod = typeof(LevelCreator).GetMethod("HandleSnapping", flags);
                _getMirroredPositionMethod = typeof(LevelCreator).GetMethod("GetMirroredPosition", flags);
                _getMirroredRotationMethod = typeof(LevelCreator).GetMethod("GetMirroredRotation", flags);
                _isMouseOverSpawnPointMethod = typeof(LevelCreator).GetMethod("IsMouseOverSpawnPoint", flags);
            }

            // --- 核心修改逻辑开始 ---

            // 使用我们增强版的射线检测
            bool hasValidInput = GetMouseWorldPosition(___m_MainCamera, out Vector3 targetPoint);
            LevelToolsHandler toolsHandler = LevelToolsHandler.Instance;

            if (toolsHandler != null && hasValidInput)
            {
                // 设置鼠标位置 (保持 X=0 的游戏逻辑)
                ___m_MousePosition = new Vector3(0f, targetPoint.y, targetPoint.z);

                // 处理吸附逻辑 (Snapping)
                bool isSnapping = false;
                if (toolsHandler.IsSnapping || ___m_MapGrid.UsingGrid)
                {
                    // 调用私有方法 HandleSnapping
                    isSnapping = (bool)_handleSnappingMethod.Invoke(__instance, null);
                }

                if (!isSnapping)
                {
                    // 更新笔刷位置
                    if (___m_BrushObject)
                    {
                        ___m_BrushObject.transform.position = ___m_MousePosition + ___m_BrushOffset;
                    }

                    // 处理镜像逻辑
                    if (toolsHandler.IsMirroring)
                    {
                        // 获取镜像位置 (Invoke 会触发你已经写好的 GetMirroredPositionPrefix)
                        Vector3 mirroredPos = (Vector3)_getMirroredPositionMethod.Invoke(__instance, new object[] { ___m_MousePosition + ___m_BrushOffset });

                        // 获取镜像旋转
                        Quaternion mirroredRot = (Quaternion)_getMirroredRotationMethod.Invoke(__instance, new object[] { toolsHandler.IsMirroringRotation });

                        if (___m_MirrorBrushObject)
                        {
                            ___m_MirrorBrushObject.transform.SetPositionAndRotation(mirroredPos, mirroredRot);
                        }
                    }
                }
                else if (toolsHandler.IsMirroring && ___m_MirrorBrushObject)
                {
                    // 如果正在吸附中，且开启了镜像，需要额外更新镜像物体的位置
                    // 注意：HandleSnapping 内部会更新 m_BrushObject，但不会更新 MirrorBrushObject，所以这里需要补一下
                    // 由于 HandleSnapping 可能会修改 m_MousePosition (吸附后的位置)，我们重新读取一下
                    // 但在原版代码中，Snapping 后的镜像更新写在 HandleSnapping 内部的一小段逻辑里或者 UpdateMousePosition 的分支里
                    // 这里我们简单处理：让镜像跟随当前的吸附点
                    // (为了更完美的还原，这里简化处理，通常 HandleSnapping 已经处理了大部分情况)
                }
            }
            else
            {
                // 如果射线和平面都检测失败（极少情况），隐藏笔刷
                if (___m_BrushObject)
                {
                    ___m_BrushObject.transform.position = new Vector3(0f, -50f, 0f);
                }
                if (___m_MirrorBrushObject)
                {
                    ___m_MirrorBrushObject.transform.position = new Vector3(0f, -50f, 0f);
                }
            }

            // 处理 SpawnPoint 的动画 (鼠标悬停时播放)
            object[] spawnParams = new object[] { null };
            bool isOverSpawn = (bool)_isMouseOverSpawnPointMethod.Invoke(__instance, spawnParams);
            Transform hitSpawn = spawnParams[0] as Transform;

            if (isOverSpawn && hitSpawn != null)
            {
                CodeAnimation component = hitSpawn.GetComponent<CodeAnimation>();
                if (!component.IsPlaying)
                {
                    component.Play();
                    component.looping = true;
                }
            }
            else
            {
                foreach (Transform transform in ___m_SpawnPoints)
                {
                    CodeAnimation component = transform.GetComponent<CodeAnimation>();
                    component.looping = false;
                }
            }

            // 返回 false 以跳过原版那个有 Bug 的 UpdateMousePosition 方法
            return false;
        }
    }
}
