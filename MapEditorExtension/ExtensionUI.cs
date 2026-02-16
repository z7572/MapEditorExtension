using System;
using System.Linq;
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
        if (canStackPlace && Input.GetKeyDown(KeyCode.P) || canStackPlace && isStackPlaceByFrame && Input.GetKey(KeyCode.P))
        {
            if (WorkshopStateHandler.IsPlayTestingMode)
            {
                Helper.SendModOutput("Cannot stack object in play testing mode!", Helper.LogType.Warning);
                return;
            }
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
        if (canRotatePointedObject && Input.GetKeyDown(KeyCode.R) && !WorkshopStateHandler.IsPlayTestingMode)
        {
            var levelCreator = LevelCreator.Instance;

            var m_BrushObject = Traverse.Create(levelCreator).Field("m_BrushObject").GetValue<GameObject>();
            var rot = rotationAngle;

            if (m_BrushObject == null)
            {
                if (Helper.CastRaycastFromMouse(levelCreator, out var hit))
                {
                    GameObject hittedObj = hit.collider.transform.root.gameObject;
                    if (!Helper.IsRaycastSatisfied(hittedObj, "rotate", true)) return;

                    if (LevelManager.Instance == null) return;

                    var levelObjects = LevelManager.Instance.PlacedLevelObjects;
                    if (levelObjects == null) return;

                    foreach (var levelObject in levelObjects)
                    {
                        if (levelObject == null || levelObject.VisibleObject == null) continue;

                        if (levelObject.VisibleObject == hittedObj.gameObject)
                        {
                            if (canReverseRotate && Input.GetKey(KeyCode.LeftShift))
                            {
                                rot = -rotationAngle;
                            }

                            levelObject.VisibleObject.transform.Rotate(new Vector3(rot, 0, 0));

                            Vector3 currentEuler = levelObject.VisibleObject.transform.rotation.eulerAngles;
                            Vector3 finalSaveRotation = currentEuler;

                            if (Mathf.Abs(currentEuler.z) > 1f)
                            {
                                float newX = 180f - currentEuler.x;
                                float newY = currentEuler.y + 180f;

                                newX = (newX % 360f + 360f) % 360f;
                                newY = (newY % 360f + 360f) % 360f;

                                finalSaveRotation = new Vector3(newX, newY, 0f);
                            }

                            levelObject.Rotation = finalSaveRotation;

                            break;
                        }
                    }
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            viewportFollowMap = true;
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
        if (m_BrushObject != null)
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
            Debug.Log("m_BrushObject is null");
            GameObject hittedObj = null;
            bool targetFound = false;

            if (Helper.CastRaycastFromMouse(levelCreator, out var hit))
            {
                hittedObj = hit.collider.transform.root.gameObject;
                Debug.Log("hittedObj: " + hittedObj.name);

                if (Helper.IsRaycastSatisfied(hittedObj, "stack", output: false) && LevelManager.Instance.ContainsObject(hittedObj))
                {
                    position = hittedObj.transform.position;
                    rotation = hittedObj.transform.rotation;
                    scale = hittedObj.transform.localScale;
                    selectedObj = m_ResourcesManager.GetObjectByName(hittedObj.name.Replace("(Clone)", ""));
                    targetFound = true;
                }
            }

            if (!targetFound)
            {
                if (HardScaleUI.selectedObject != null)
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
        PlaceObject(selectedObj, position, rotation, scale);
        if (m_MirrorBrushObject)
        {
            if (m_ToolsHandler.IsMirroring)
            {
                var getMirroredPositionMethod = AccessTools.Method(typeof(LevelCreator), "GetMirroredPosition");
                //var mirroredPosition = levelCreator.GetMirroredPosition(position);
                var mirroredPosition = (Vector3)getMirroredPositionMethod.Invoke(levelCreator, [position]);
                var mirroredRotation = m_MirrorBrushObject.transform.rotation;
                if (m_MirrorBrushObject.activeInHierarchy)
                {
                    PlaceObject(selectedObj, mirroredPosition, mirroredRotation, scale);
                }
            }
        }
        levelCreator.GenerateSnapCornersFaces();
        m_OnPlaceAction?.Invoke();
        var count = CountStackedObjects(selectedObj.name, position, rotation, scale);
        Helper.SendModOutput($"Stacked object: {selectedObj.name} of {count}", Helper.LogType.Success);
        isMouseMoved = false;
    }

    private void PlaceObject(GameObject selectedObj, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var levelManager = LevelManager.Instance;
        var gameObject = Instantiate(selectedObj, position, rotation);
        gameObject.transform.localScale = scale;
        if (selectedObj.name.ToLower().Contains("gun")) // Weapon
        {
            var currentSelectedWeapon = selectedObj.name.ToLower().Replace("gun", "").Replace("(clone)", "");
            levelManager.StripObject(gameObject);
            WeaponObject weaponObject2 = new WeaponObject(gameObject, int.Parse(currentSelectedWeapon), null);
            levelManager.AddNewPlacedLevelWeaponObject(weaponObject2);
        }
        else // Object
        {
            var levelObject = new LevelObject(gameObject, selectedObj.name, null, int.MinValue);
            //levelObject.Scale = new Vector2(scale.z, scale.y);
            //levelObject.Rotation = gameObject.transform.rotation.eulerAngles;

            var component = gameObject.GetComponent<EnableAfterFrame>();
            if (component)
            {
                component.obj.SetActive(true);
            }
            levelManager.AddNewPlacedLevelObject(levelObject, true);
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