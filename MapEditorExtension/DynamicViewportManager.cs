using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using LevelEditor;
using HarmonyLib;

namespace EditorExtension;

public class DynamicViewportManager : MonoBehaviour
{
    public static DynamicViewportManager Instance { get; private set; }

    private Camera _cam;
    private Vector3 _dragOrigin;
    private Vector3 _startMousePos;
    private bool _isDragging = false;
    private const float DragThreshold = 5f;
    private int _lastFrameCount = -1;

    private Vector3 _savedPosition;
    private float _savedViewportSize;
    private bool _savedFollowState;

    // --- Infinite Grid Variables ---
    private Vector3 _lastGridSnapPos = Vector3.zero;
    private Transform _gridParentTransform;
    private bool _hasInitGrid = false;

    // 动态计算出的网格步长
    private float _gridStepY = 1f; // 默认为1，计算后覆盖
    private float _gridStepZ = 1f;

    // 反射缓存
    private object _mapGridInstance;
    private FieldInfo _f_GridPositions;
    private PropertyInfo _p_GridPositions;
    private bool _gridPosIsProperty = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _cam = GetComponent<Camera>();
        if (_cam != Camera.main)
        {
            Destroy(this);
            return;
        }

        if (ExtensionUI.Instance != null && MapSizeHandler.Instance != null)
        {
            ExtensionUI.Instance.viewportSize = MapSizeHandler.Instance.mapSize;
        }

        // 延迟初始化，等待 Grid 生成
        Invoke(nameof(InitGridReference), 1.0f);
    }

    private void InitGridReference()
    {
        if (LevelCreator.Instance == null) return;

        try
        {
            // 1. 查找 GridParent (支持未激活物体)
            var gridObj = GameObject.Find("GridParent");
            if (gridObj == null)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                gridObj = allObjects.FirstOrDefault(g => g.name == "GridParent" && g.scene.isLoaded);
            }
            if (gridObj == null)
            {
                var childLine = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == "GridLine(Clone)" && g.transform.parent != null);
                if (childLine != null) gridObj = childLine.transform.parent.gameObject;
            }

            if (gridObj != null)
            {
                _gridParentTransform = gridObj.transform;
                _lastGridSnapPos = _gridParentTransform.position;
                Debug.Log($"[DynamicViewport] 锁定网格父对象: {_gridParentTransform.name}");
            }

            // 2. 获取逻辑数据
            var traverseLC = Traverse.Create(LevelCreator.Instance);
            _mapGridInstance = traverseLC.Field("m_MapGrid").GetValue();

            if (_mapGridInstance != null)
            {
                var gridType = _mapGridInstance.GetType();
                _f_GridPositions = gridType.GetField("GridPositions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (_f_GridPositions == null)
                {
                    _p_GridPositions = gridType.GetProperty("GridPositions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_p_GridPositions != null) _gridPosIsProperty = true;
                }

                if (_f_GridPositions != null || _p_GridPositions != null)
                {
                    // --- 关键修改：计算网格间距 ---
                    CalculateGridSpacing();

                    _hasInitGrid = true;
                    Debug.Log($"[DynamicViewport] 网格初始化完成。间距 StepZ: {_gridStepZ:F4}, StepY: {_gridStepY:F4}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DynamicViewport] 初始化异常: {e}");
        }
    }

    private void CalculateGridSpacing()
    {
        try
        {
            Vector2[] positions = null;
            if (_gridPosIsProperty)
                positions = _p_GridPositions.GetValue(_mapGridInstance, null) as Vector2[];
            else
                positions = _f_GridPositions.GetValue(_mapGridInstance) as Vector2[];

            if (positions != null && positions.Length > 1)
            {
                // LevelCreator 中 GridPositions 存储的是 Vector2(z, y)
                // 数组生成顺序是：外层循环 X(即Z轴)，内层循环 Y
                // 所以 positions[0] 和 positions[1] 通常是 Y 轴相邻的点
                // 我们需要找到第一个 X(Z) 变化的索引来计算 Z 轴间距

                Vector2 p0 = positions[0];
                Vector2 p1 = positions[1];

                // 1. 计算 Y 轴间距 (假设内层循环是Y，相邻点Y不同)
                if (Mathf.Abs(p1.y - p0.y) > 0.0001f)
                {
                    _gridStepY = Mathf.Abs(p1.y - p0.y);
                }
                else
                {
                    // 如果数组排序很奇怪，遍历寻找最近的Y差异
                    for (int i = 1; i < positions.Length; i++)
                    {
                        if (Mathf.Abs(positions[i].y - p0.y) > 0.0001f)
                        {
                            _gridStepY = Mathf.Abs(positions[i].y - p0.y);
                            break;
                        }
                    }
                }

                // 2. 计算 Z 轴间距 (对应 Vector2.x)
                // 遍历数组，找到第一个 x 值不同的点
                for (int i = 1; i < positions.Length; i++)
                {
                    if (Mathf.Abs(positions[i].x - p0.x) > 0.0001f)
                    {
                        _gridStepZ = Mathf.Abs(positions[i].x - p0.x);
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DynamicViewport] 计算间距失败: {e.Message}");
            _gridStepY = 1f;
            _gridStepZ = 1f;
        }
    }

    private void Update()
    {
        TryUpdateCamera();
    }

    public void TryUpdateCamera()
    {
        // Do only once per frame
        if (Time.frameCount == _lastFrameCount) return;
        _lastFrameCount = Time.frameCount;

        if (LevelCreator.Instance == null || GameManager.Instance != null) return;
        if (ExtensionUI.Instance == null || MapSizeHandler.Instance == null) return;
        if (_cam == null) return;

        if (WorkshopStateHandler.IsPlayTestingMode) return;

        ApplyViewportSize();
        HandleInput();
        HandleFollow();

        if (_hasInitGrid)
        {
            UpdateGridPosition();
        }
    }

    private void UpdateGridPosition()
    {
        Vector3 camPos = transform.position;

        // --- 核心修复：基于真实步长的吸附算法 ---
        // 公式：Round(坐标 / 步长) * 步长
        // 这样可以保证网格移动的距离永远是单元格的整数倍，实现无缝连接

        float targetY = Mathf.Round(camPos.y / _gridStepY) * _gridStepY;
        float targetZ = Mathf.Round(camPos.z / _gridStepZ) * _gridStepZ;

        Vector3 targetPos = new Vector3(0f, targetY, targetZ);

        // 视口过大归位
        if (ExtensionUI.Instance.viewportSize > 30f)
        {
            targetPos = Vector3.zero;
        }

        // 判断是否有位移 (使用极小容差防止浮点抖动)
        if (Vector3.Distance(targetPos, _lastGridSnapPos) > 0.001f)
        {
            Vector3 delta = targetPos - _lastGridSnapPos;
            MoveGrid(targetPos, delta);
            _lastGridSnapPos = targetPos;
        }
    }

    private void MoveGrid(Vector3 newPos, Vector3 delta)
    {
        if (_gridParentTransform != null)
        {
            _gridParentTransform.position = newPos;
        }

        if (_mapGridInstance == null) return;

        // LevelCreator 使用 (Z, Y) 坐标系
        Vector2 delta2D = new Vector2(delta.z, delta.y);

        try
        {
            Vector2[] positions = null;

            if (_gridPosIsProperty)
                positions = _p_GridPositions.GetValue(_mapGridInstance, null) as Vector2[];
            else if (_f_GridPositions != null)
                positions = _f_GridPositions.GetValue(_mapGridInstance) as Vector2[];

            if (positions != null)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] += delta2D;
                }
            }
        }
        catch (Exception e)
        {
            _hasInitGrid = false;
            Debug.LogError($"[DynamicViewport] 更新数据失败: {e.Message}");
        }
    }

    // PlayTest 逻辑保持不变
    public void OnPlayTestStarted()
    {
        _savedPosition = transform.position;
        _savedViewportSize = ExtensionUI.Instance.viewportSize;
        _savedFollowState = ExtensionUI.Instance.viewportFollowMap;

        ExtensionUI.Instance.viewportFollowMap = true;
        ExtensionUI.Instance.viewportSize = 10f;

        var resetPos = transform.position;
        resetPos.y = 0f;
        resetPos.z = 0f;
        transform.position = resetPos;

        if (_hasInitGrid && _lastGridSnapPos != Vector3.zero)
        {
            Vector3 delta = Vector3.zero - _lastGridSnapPos;
            MoveGrid(Vector3.zero, delta);
            _lastGridSnapPos = Vector3.zero;
        }
    }

    public void OnPlayTestEnded()
    {
        ExtensionUI.Instance.viewportSize = _savedViewportSize;
        ExtensionUI.Instance.viewportFollowMap = _savedFollowState;

        transform.position = _savedPosition;
        ApplyViewportSize();

        if (_hasInitGrid)
        {
            // 强制重置以触发位置修正
            _lastGridSnapPos = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            UpdateGridPosition();
        }
    }

    // Input Handling 保持不变
    private void HandleInput()
    {
        // Shift + Scroll
        if (Input.GetKey(KeyCode.LeftShift))
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                var newSize = ExtensionUI.Instance.viewportSize - scroll * 10f;
                newSize = Mathf.Clamp(newSize, 1f, ConfigHandler.GetEntry<float>("MapSizeMax"));
                ExtensionUI.Instance.viewportSize = newSize;
                ExtensionUI.Instance.viewportFollowMap = false;
                ApplyViewportSize();
            }
        }

        // Middle button drag logic
        if (Input.GetMouseButtonDown(2))
        {
            _startMousePos = Input.mousePosition;
            _isDragging = false;
        }

        if (Input.GetMouseButton(2))
        {
            if (!_isDragging && Vector3.Distance(Input.mousePosition, _startMousePos) > DragThreshold)
            {
                _isDragging = true;
                _dragOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
                ExtensionUI.Instance.viewportFollowMap = false;
            }

            if (_isDragging)
            {
                var currentMousePosWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                var difference = _dragOrigin - currentMousePosWorld;
                difference.x = 0f;
                transform.position += difference;
            }
        }

        if (Input.GetMouseButtonUp(2))
        {
            _isDragging = false;
        }
    }

    private void ApplyViewportSize()
    {
        var aspectRatio = _cam.aspect;
        var num2 = Mathf.Max((float)Screen.width / (float)Screen.height, 1.78f);
        var targetOrthoSize = num2 * 10f / aspectRatio * (ExtensionUI.Instance.viewportSize / 10f);
        _cam.orthographicSize = targetOrthoSize;
    }

    private void HandleFollow()
    {
        if (ExtensionUI.Instance.viewportFollowMap)
        {
            ExtensionUI.Instance.viewportSize = MapSizeHandler.Instance.mapSize;
            var targetPos = new Vector3(transform.position.x, 0f, 0f);
            if (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);
            }
            else
            {
                transform.position = targetPos;
            }
        }
    }
}