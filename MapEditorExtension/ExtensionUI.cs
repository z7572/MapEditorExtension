using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using LevelEditor;
using static UnityEngine.GUILayout;

namespace EditorExtension;

public class ExtensionUI : MonoBehaviour
{
    public static ExtensionUI Instance { get; private set; }

    private bool _mShowExtension;
    private Rect extensionMenuRect = new(250f, 100f, 400f, 190f);

    public KeyCode extensionWindowKey1;
    public KeyCode extensionWindowKey2;
    public bool singleExtensionKey;

    private bool isMouseMoved = false;
    private Vector3 lastMousePos;

    public static float rotationAngle = 90f;
    public static bool canReverseRotate = true;
    public static bool isHorizonalMirror = false;
    public static bool canStackPlace = false;
    public static bool isStackPlaceByFrame = false;
    public static bool canScale = true;
    public static bool canRotatePointedObject = true;

    public static bool isMirrorDrag = true;

    public static bool IsFixCrateCollision
    {
        get => ConfigHandler.GetEntry<bool>("FixCrateCollision");
        set
        {
            if (IsFixCrateCollision == value) return;
            ConfigHandler.ModifyEntry("FixCrateCollision", value.ToString());
        }
    }

    public float viewportSize = 10f;
    public bool viewportFollowMap = true;

    private void Start()
    {
        Instance = this;       
    }

    private void Awake()
    {
        extensionWindowKey1 = ConfigHandler.GetEntry<KeyboardShortcut>("ExtensionWindowKeybind").MainKey;
        extensionWindowKey2 = ConfigHandler.GetEntry<KeyboardShortcut>("ExtensionWindowKeybind").Modifiers.LastOrDefault();
        if (extensionWindowKey2 == KeyCode.None) singleExtensionKey = true;
    }

    private void Update()
    {
        CheckMouseMove();

        if (((Input.GetKey(extensionWindowKey1) && Input.GetKeyDown(extensionWindowKey2)) ||
             (Input.GetKeyDown(extensionWindowKey1) && singleExtensionKey))
             && LevelCreator.Instance != null && GameManager.Instance == null)
             //&& UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LevelEditor")
        {
            _mShowExtension = !_mShowExtension;
        }
        if ((canStackPlace && Input.GetKeyDown(KeyCode.P) || canStackPlace && isStackPlaceByFrame && Input.GetKey(KeyCode.P)) &&
            !WorkshopStateHandler.IsPlayTestingMode)
        {
            Stack();
        }
        if (_mShowExtension && canStackPlace && isMouseMoved)
        {
            if (Helper.CastRaycastFromMouse(LevelCreator.Instance, out var hit))
            {
                GameObject hittedObj = hit.collider.transform.root.gameObject;
                if (!Helper.IsRaycastSatisfied(hittedObj, output: false)) return;
                var objectName = hittedObj.name.Replace("(Clone)", "");
                var transform = hittedObj.transform;
                var stackCount = CountStackedObjects(objectName, transform.position, transform.rotation, transform.localScale);
                if (stackCount > 1)
                {
                    Helper.SendModOutput($"Current stacked: {hittedObj.name} of {stackCount}", Helper.LogType.Success);
                }
            }
        }
        if (canRotatePointedObject && !WorkshopStateHandler.IsPlayTestingMode)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.F))
            {
                if (Helper.CastRaycastFromMouse(LevelCreator.Instance, out var hit))
                {
                    GameObject hittedObj = hit.collider.transform.root.gameObject;

                    var isGround = hittedObj.name.StartsWith("GROUND");
                    var isDragging = LevelEditorInputManager.IsHoldingPlace();

                    if (isGround || !isDragging)
                    {
                        RotatePointedObjectLogic(hittedObj);
                    }
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            viewportFollowMap = true;
        }
    }

    private static void RotatePointedObjectLogic(GameObject hittedObj)
    {
        if (hittedObj.name.ToLower().Contains("gun")) return;
        if (!Helper.IsRaycastSatisfied(hittedObj, "rotate", true)) return;

        Vector3 mainDelta = Vector3.zero;
        if (Input.GetKeyDown(KeyCode.R))
        {
            float angle = rotationAngle;
            if (canReverseRotate && Input.GetKey(KeyCode.LeftShift))
            {
                angle = -angle;
            }
            mainDelta = new Vector3(angle, 0f, 0f);
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            if (hittedObj.GetComponent<ProprFlipAroundYIndeadOfX>())
                mainDelta = new Vector3(0f, 180f, 0f);
            else
                mainDelta = new Vector3(180f, 0f, 0f);
        }

        hittedObj.transform.Rotate(mainDelta);
        LevelManager.Instance.UpdatePlacedObject(hittedObj);


        var lm = LevelManager.Instance;
        var getLO = AccessTools.Method(typeof(LevelManager), "GetLevelObjectFromGameObject");
        var getWO = AccessTools.Method(typeof(LevelManager), "GetLevelWeaponObjectFromGameObject");

        LevelObject lo = (LevelObject)getLO.Invoke(lm, [hittedObj]);
        WeaponObject wo = (WeaponObject)getWO.Invoke(lm, [hittedObj]);

        GameObject mirrorObj = null;

        var isMirroring = LevelToolsHandler.Instance.IsMirroring;
        if (lo != null && lo.HasMirrorObject())
        {
            if (isMirroring)
            {
                mirrorObj = lo.MirrorObject.VisibleObject;
            }
            else
            {
                var traverseMirror = Traverse.Create(lo).Property("MirrorObject");
                traverseMirror.Property("MirrorObject").SetValue(null);
                traverseMirror.SetValue(null);
                Helper.SendModOutput($"Unbound Mirror Object: {lo.Id}", Helper.LogType.Warning);
            }
        }
        else if (wo != null && wo.HasMirrorObject()) // Dead code, nvm
        {
            if (isMirroring)
            {
                mirrorObj = wo.MirrorObject.VisibleObject;
            }
            else
            {
                var traverseMirror = Traverse.Create(wo).Property("MirrorObject");
                traverseMirror.Property("MirrorObject").SetValue(null);
                traverseMirror.SetValue(null);
                Helper.SendModOutput($"Unbound Mirror Object: {wo.WeaponName}", Helper.LogType.Warning);
            }
        }

        if (mirrorObj && LevelToolsHandler.Instance.IsMirroring)
        {
            bool doMirrorRot = LevelToolsHandler.Instance.IsMirroringRotation;
            mirrorObj.transform.rotation = Helper.GetMirroredRotation(doMirrorRot, null, hittedObj);
            LevelManager.Instance.UpdatePlacedObject(mirrorObj);
        }
    }

    public void ResetWindowSize()
    {
        extensionMenuRect.height = 0f;
    }

    private void OnGUI()
    {
        if (_mShowExtension)
        {
            extensionMenuRect = Window(1001, extensionMenuRect, ExtensionWindow, "<b>Editor Extension</b> by z7572");
        }
    }

    private void ExtensionWindow(int window)
    {
        BeginVertical();
        Space(5);

        BeginHorizontal();
        Space(10);
        Label("旋转角度:", Width(70f));
        rotationAngle = Mathf.Round(HorizontalSlider(rotationAngle, 1, 90, Width(260f)));
        FlexibleSpace();
        Label(rotationAngle + "°");
        FlexibleSpace();
        Space(10);
        EndHorizontal();

        BeginHorizontal();
        Space(10);
        canReverseRotate = Toggle(canReverseRotate, "Shift+R:反向旋转");
        FlexibleSpace();
        canStackPlace = Toggle(canStackPlace, "P:堆叠放置");
        Space(5);
        isStackPlaceByFrame = Toggle(isStackPlaceByFrame, "按帧");
        FlexibleSpace();
        isHorizonalMirror = Toggle(isHorizonalMirror, "水平镜像");
        Space(10);
        EndHorizontal();

        BeginHorizontal();
        Space(10);
        canScale = Toggle(canScale, "鼠标中键:设置物体比例");
        FlexibleSpace();
        canRotatePointedObject = Toggle(canRotatePointedObject, "可旋转鼠标指向物体");
        FlexibleSpace();
        isMirrorDrag = Toggle(isMirrorDrag, "镜像拖动");
        Space(10);
        EndHorizontal();

        BeginHorizontal();
        Space(10);
        IsFixCrateCollision = Toggle(IsFixCrateCollision, "修复箱子碰撞以及忽略无碰撞警告 (会导致不同步, 详见配置文件)");
        FlexibleSpace();
        Space(10);
        EndHorizontal();

        BeginHorizontal();
        Space(10);
        Label("Shift+鼠标滚轮:调节视口大小");
        Space(10);
        FlexibleSpace();
        Label("鼠标中键拖动:移动视口");
        FlexibleSpace();
        Space(10);
        EndHorizontal();

        BeginHorizontal();
        Space(10);
        Label("地图尺寸:", Width(60f));
        if (float.TryParse(TextField(MapSizeHandler.Instance.mapSize.ToString(), Width(70f)), out var mapSize))
        {
            MapSizeHandler.Instance.mapSize = mapSize;
        }
        Space(10);
        Label("视口尺寸:", Width(60f));
        if (float.TryParse(TextField(this.viewportSize.ToString(), Width(70f)), out var viewportSize))
        {
            this.viewportSize = viewportSize;
        }
        FlexibleSpace();
        if (Button("重置视口(F1)"))
        {
            viewportFollowMap = true;
        }
        Space(10);
        EndHorizontal();

        BeginHorizontal();
        Space(10);
        Label("输出:", Width(70f));

        Label(Helper.currentOutputMsg, Width(300f));
        Space(20);
        EndHorizontal();

        EndVertical();
        GUI.DragWindow();
    }

    // Double drag coroutine for main object and its mirror, handles rotation/flip and mirror rotation
    public IEnumerator DragMirrorObject(Transform mainTrans, Transform mirrorTrans)
    {
        var lc = LevelCreator.Instance;
        var traverseLC = Traverse.Create(lc);

        var brushObj = traverseLC.Field("m_BrushObject").GetValue<GameObject>();
        var mirrorBrushObj = traverseLC.Field("m_MirrorBrushObject").GetValue<GameObject>();
        if (brushObj) Destroy(brushObj);
        if (mirrorBrushObj) Destroy(mirrorBrushObj);

        var mapSpace = traverseLC.Field("m_MapSpace").GetValue<MapSpace>();

        // 给 Main Object 挂上 CheckPreRotation，以便 Helper 计算 Mirror 旋转时能读取到正确的预旋转
        // (Helper 是根据传入的 targetObj(Main) 来查找组件的)
        if (mainTrans.GetComponent<CheckPreRotation>() == null)
        {
            mainTrans.gameObject.AddComponent<CheckPreRotation>();
        }

        while (LevelEditorInputManager.IsHoldingPlace())
        {
            // 位置跟随
            var mirroredPos = mapSpace.GetMirroredPosition(mainTrans.position);
            mirrorTrans.position = mirroredPos;

            // 旋转跟随：交由 Patches.HandleFlipOrRotateDraggedObjectPostfix 处理
            yield return null;
        }

        LevelManager.Instance.UpdatePlacedObject(mainTrans.gameObject);
        LevelManager.Instance.UpdatePlacedObject(mirrorTrans.gameObject);
    }

    public void Stack()
    {
        var levelCreator = LevelCreator.Instance;
        var traverse = Traverse.Create(levelCreator);
        var m_InterfaceManager = traverse.Field("m_InterfaceManager").GetValue<InterfaceManager>();
        var m_BrushObject = traverse.Field("m_BrushObject").GetValue<GameObject>();
        var m_MirrorBrushObject = traverse.Field("m_MirrorBrushObject").GetValue<GameObject>();
        var m_MousePosition = traverse.Field("m_MousePosition").GetValue<Vector3>();
        var m_SelectedGameObject = traverse.Field("m_SelectedGameObject").GetValue<GameObject>();
        var m_ResourcesManager = traverse.Field("m_ResourcesManager").GetValue<ResourcesManager>();
        var m_ToolsHandler = traverse.Field("m_ToolsHandler").GetValue<LevelToolsHandler>();
        var m_OnPlaceAction = traverse.Field("m_OnPlaceAction").GetValue<Action>();

        var selectedObj = m_SelectedGameObject;

        if (m_InterfaceManager.IsOutsideOfEditorArea())
        {
            Helper.SendModOutput("Cannot place object outside of editor area!", Helper.LogType.Warning);
            return;
        }
        var position = new Vector3();
        var rotation = new Quaternion();
        var scale = new Vector3();

        LevelObject existingLevelObj = null;
        WeaponObject existingWeaponObj = null;

        if (m_BrushObject)
        {
            if (m_BrushObject.transform.localScale == Vector3.zero)
            {
                Helper.SendModOutput("Cannot stack GROUND!", Helper.LogType.Warning);
                return;
            }
            position = m_BrushObject.transform.position;
            rotation = m_BrushObject.transform.rotation;
            scale = m_BrushObject.transform.localScale;
        }
        else
        {
            //Debug.Log("m_BrushObject is null");
            GameObject hittedObj = null;
            bool targetFound = false;

            if (Helper.CastRaycastFromMouse(levelCreator, out var hit))
            {
                hittedObj = hit.collider.transform.root.gameObject;
                //Debug.Log("hittedObj: " + hittedObj.name);

                if (Helper.IsRaycastSatisfied(hittedObj, "stack", output: false) && LevelManager.Instance.ContainsObject(hittedObj))
                {
                    position = hittedObj.transform.position;
                    rotation = hittedObj.transform.rotation;
                    scale = hittedObj.transform.localScale;
                    selectedObj = m_ResourcesManager.GetObjectByName(hittedObj.name.Replace("(Clone)", ""));

                    var lm = LevelManager.Instance;
                    var getLO = AccessTools.Method(typeof(LevelManager), "GetLevelObjectFromGameObject");
                    var getWO = AccessTools.Method(typeof(LevelManager), "GetLevelWeaponObjectFromGameObject");

                    existingLevelObj = (LevelObject)getLO.Invoke(lm, [hittedObj]);
                    existingWeaponObj = (WeaponObject)getWO.Invoke(lm, [hittedObj]);

                    targetFound = true;
                }
            }

            if (!targetFound)
            {
                if (HardScaleUI.selectedObject)
                {
                    var scaleObj = HardScaleUI.selectedObject;
                    position = scaleObj.transform.position;
                    rotation = scaleObj.transform.rotation;
                    scale = scaleObj.transform.localScale;
                    selectedObj = m_ResourcesManager.GetObjectByName(scaleObj.name.Replace("(Clone)", ""));
                }
                else
                {
                    if (!Helper.IsRaycastSatisfied(hittedObj, "stack")) return;
                    return;
                }
            }
        }

        var mainPlacedObject = PlaceObject(selectedObj, position, rotation, scale, null);

        var shouldStackMirror = false;
        var mirrorPos = Vector3.zero;
        var mirrorRot = Quaternion.identity;

        // Stack when object have mirror and current mirror state is on.
        if (m_MirrorBrushObject && m_ToolsHandler.IsMirroring)
        {
            shouldStackMirror = true;
            var getMirroredPositionMethod = AccessTools.Method(typeof(LevelCreator), "GetMirroredPosition");
            //var mirroredPosition = levelCreator.GetMirroredPosition(position);
            mirrorPos = (Vector3)getMirroredPositionMethod.Invoke(levelCreator, [position]);
            mirrorRot = m_MirrorBrushObject.transform.rotation;
        }

        // Mirror Drag: Stack mirror when object have its own mirror and being dragged, no matter the current mirror state.
        else if (isMirrorDrag)
        {
            if (existingLevelObj != null && existingLevelObj.HasMirrorObject())
            {
                shouldStackMirror = true;
                var mirVis = existingLevelObj.MirrorObject.VisibleObject.transform;
                mirrorPos = mirVis.position;
                mirrorRot = mirVis.rotation;
            }
            else if (existingWeaponObj != null && existingWeaponObj.HasMirrorObject())
            {
                shouldStackMirror = true;
                var mirVis = existingWeaponObj.MirrorObject.VisibleObject.transform;
                mirrorPos = mirVis.position;
                mirrorRot = mirVis.rotation;
            }
        }

        if (shouldStackMirror)
        {
            PlaceObject(selectedObj, mirrorPos, mirrorRot, scale, mainPlacedObject);
        }
        
        levelCreator.GenerateSnapCornersFaces();
        m_OnPlaceAction?.Invoke();
        var count = CountStackedObjects(selectedObj.name, position, rotation, scale);
        Helper.SendModOutput($"Stacked object: {selectedObj.name} of {count}", Helper.LogType.Success);
        isMouseMoved = false;
    }

    private object PlaceObject(GameObject selectedObj, Vector3 position, Quaternion rotation, Vector3 scale, object mirrorRef)
    {
        var levelManager = LevelManager.Instance;
        var gameObject = Instantiate(selectedObj, position, rotation);
        gameObject.transform.localScale = scale;

        if (selectedObj.name.ToLower().Contains("gun")) // Weapon
        {
            var currentSelectedWeapon = selectedObj.name.ToLower().Replace("gun", "").Replace("(clone)", "");
            levelManager.StripObject(gameObject);

            WeaponObject mirrorWeapon = mirrorRef as WeaponObject;

            WeaponObject weaponObject2 = new WeaponObject(gameObject, int.Parse(currentSelectedWeapon), mirrorWeapon);
            levelManager.AddNewPlacedLevelWeaponObject(weaponObject2);
            return weaponObject2;
        }
        else // Object
        {
            LevelObject mirrorLevelObj = mirrorRef as LevelObject;

            var levelObject = new LevelObject(gameObject, selectedObj.name, mirrorLevelObj, int.MinValue);
            //levelObject.Scale = new Vector2(scale.z, scale.y);
            //levelObject.Rotation = gameObject.transform.rotation.eulerAngles;

            var component = gameObject.GetComponent<EnableAfterFrame>();
            if (component)
            {
                component.obj.SetActive(true);
            }
            levelManager.AddNewPlacedLevelObject(levelObject, true);
            return levelObject;
        }
    }

    private static int CountStackedObjects(string objectName, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var levelManager = LevelManager.Instance;
        var targetPos = new Vector2(position.y, position.z);
        var targetRot = rotation.eulerAngles;
        var targetScale = new Vector2(scale.z, scale.y);
        if (objectName.ToLower().Contains("gun"))
        {
            return levelManager.GetSaveableLevelWeaponObjects.Count(lo => lo.WeaponIndex == int.Parse(objectName.ToLower().Replace("gun", "").Replace("(clone)", ""))
                && lo.PositionX == targetPos.x && lo.PositionY == targetPos.y);
        }
        else
        {
            return levelManager.PlacedLevelObjects.Count(lo => lo.Id == objectName.Replace("(Clone)", "")
                && lo.Position == targetPos && lo.Rotation == targetRot && lo.Scale == targetScale);
        }
    }

    private void CheckMouseMove()
    {
        var currentMousePos = Input.mousePosition;
        if (Vector3.Distance(currentMousePos, lastMousePos) > 0.1f)
        {
            isMouseMoved = true;
        }
        lastMousePos = currentMousePos;
    }
}