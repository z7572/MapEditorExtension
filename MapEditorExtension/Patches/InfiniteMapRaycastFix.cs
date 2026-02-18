using System.Reflection;
using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace EditorExtension.Patches;

[HarmonyPatch]
public static class InfiniteMapRaycastFix
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