using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using LevelEditor;

namespace MapEditorExtension
{

    public class ExtensionUI : MonoBehaviour
    {
        public static ExtensionUI Instance { get; private set; }
        private bool _mShowExtension;
        private Rect extensionMenuRect = new(250f, 100f, 400f, 130f);
        public KeyCode extensionWindowKey1;
        public KeyCode extentionWindowKey2;
        public bool singleExtensionKey;

        private string commandInput = "";
        private bool isMouseMoved = false;
        private Vector3 lastMousePos;
        public static float rotationAngle = 90f;
        public static bool canReverseRotate = true;
        public static bool isHorizonalMirror = false;
        public static bool canStackPlace = false;
        public static bool isStackPlaceByFrame = false;
        public static bool canScale = true;
        public static bool canRotatePointedObject = true;

        private void Start()
        {
            Instance = this;
        }

        private void Awake()
        {
            extensionWindowKey1 = ConfigHandler.GetEntry<KeyboardShortcut>("MapEditorWindowKeybind").MainKey;
            extentionWindowKey2 = ConfigHandler.GetEntry<KeyboardShortcut>("MapEditorWindowKeybind").Modifiers.LastOrDefault();
            if (extentionWindowKey2 == KeyCode.None) singleExtensionKey = true;
        }

        private void Update()
        {
            CheckMouseMove();

            if ((Input.GetKey(extensionWindowKey1) && Input.GetKeyDown(extentionWindowKey2) ||
                 Input.GetKeyDown(extensionWindowKey1) && singleExtensionKey)
                 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LevelEditor")
            {
                _mShowExtension = !_mShowExtension;
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                isHorizonalMirror = !isHorizonalMirror;
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
                if (LevelCreator.Instance.CastRaycastFromMouse(out RaycastHit hit))
                {
                    GameObject hittedObj = hit.collider.transform.root.gameObject;
                    if (!Helper.IsRaycastSatisfied(hittedObj, output: false)) return;
                    var objectName = hittedObj.name.Replace("(Clone)", "");
                    var transform = hittedObj.transform;
                    var stackCount = CountStackedObjects(LevelManager.Instance, objectName, transform.position, transform.rotation, transform.localScale);
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
                    if (levelCreator.CastRaycastFromMouse(out RaycastHit hit))
                    {
                        GameObject hittedObj = hit.collider.transform.root.gameObject;
                        if (!Helper.IsRaycastSatisfied(hittedObj, "rotate")) return;
                        var levelObjects = Traverse.Create(LevelManager.Instance).Field("m_PlacedLevelObjects").GetValue<List<LevelObject>>();
                        foreach (var levelObject in levelObjects)
                        {
                            if (levelObject.VisibleObject == hittedObj.gameObject)
                            {
                                if (canReverseRotate && Input.GetKey(KeyCode.LeftShift))
                                {
                                    rot = -rotationAngle;
                                }
                                levelObject.VisibleObject.transform.Rotate(new Vector3(rot, 0, 0));
                                levelObject.Rotation = levelObject.VisibleObject.transform.rotation.eulerAngles;
                            }
                        }
                    }
                }
            }
        }
        private void OnGUI()
        {
            if (_mShowExtension)
                extensionMenuRect = GUILayout.Window(1001, extensionMenuRect, ExtensionWindow, "Map Editor Extinsion by z7572");
        }

        private void ExtensionWindow(int window)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("旋转角度", GUILayout.Width(70f));
            rotationAngle = Mathf.Round(GUILayout.HorizontalSlider(rotationAngle, 1, 90, GUILayout.Width(260f)));
            GUILayout.FlexibleSpace();
            GUILayout.Label(rotationAngle + "°");
            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            canReverseRotate = GUILayout.Toggle(canReverseRotate, "Shift+R:反向旋转");
            GUILayout.FlexibleSpace();
            canStackPlace = GUILayout.Toggle(canStackPlace, "P:堆叠放置");
            GUILayout.Space(5);
            isStackPlaceByFrame = GUILayout.Toggle(isStackPlaceByFrame, "按帧");
            GUILayout.FlexibleSpace();
            isHorizonalMirror = GUILayout.Toggle(isHorizonalMirror, "水平镜像(H)");
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            canScale = GUILayout.Toggle(canScale, "鼠标中键:设置物体比例");
            GUILayout.FlexibleSpace();
            canRotatePointedObject = GUILayout.Toggle(canRotatePointedObject, "可旋转鼠标指向物体");
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            //Can be removed since we are using chat field to input commands (or text)
            /*
            if (Helper.isQOLModLoaded)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("输入命令:", GUILayout.Width(70f));
                commandInput = GUILayout.TextField(commandInput, GUILayout.Width(260f));
                if (GUILayout.Button("执行", GUILayout.Width(40f)))
                {
                    ExecuteCmd(commandInput);
                }
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            */

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("输出:", GUILayout.Width(70f));
            GUILayout.Label(Helper.currentOutputMsg, GUILayout.Width(300f));
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        public void Stack()
        {
            var levelCreator = LevelCreator.Instance;
            var m_InterfaceManager = Traverse.Create(levelCreator).Field("m_InterfaceManager").GetValue<InterfaceManager>();
            var m_BrushObject = Traverse.Create(levelCreator).Field("m_BrushObject").GetValue<GameObject>();
            var m_MirrorBrushObject = Traverse.Create(levelCreator).Field("m_MirrorBrushObject").GetValue<GameObject>();
            var m_MousePosition = Traverse.Create(levelCreator).Field("m_MousePosition").GetValue<Vector3>();
            var m_LevelManager = Traverse.Create(levelCreator).Field("m_LevelManager").GetValue<LevelManager>();
            var m_SelectedGameObject = Traverse.Create(levelCreator).Field("m_SelectedGameObject").GetValue<GameObject>();
            var m_ResourcesManager = Traverse.Create(levelCreator).Field("m_ResourcesManager").GetValue<ResourcesManager>();
            var m_ToolsHandler = Traverse.Create(levelCreator).Field("m_ToolsHandler").GetValue<LevelToolsHandler>();
            var m_OnPlaceAction = Traverse.Create(levelCreator).Field("m_OnPlaceAction").GetValue<Action>();

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
                if (levelCreator.CastRaycastFromMouse(out RaycastHit hit))
                {
                    GameObject hittedObj = hit.collider.transform.root.gameObject;
                    Debug.Log("hittedObj: " + hittedObj.name);
                    if (!Helper.IsRaycastSatisfied(hittedObj, "stack")) return;
                    if (m_LevelManager.ContainsObject(hittedObj))
                    {
                        position = hittedObj.transform.position;
                        rotation = hittedObj.transform.rotation;
                        scale = hittedObj.transform.localScale;
                        selectedObj = m_ResourcesManager.GetObjectByName(hittedObj.name.Replace("(Clone)", ""));
                    }
                }
            }
            PlaceObject(m_LevelManager, selectedObj, position, rotation, scale);
            if (m_MirrorBrushObject)
            {
                if (m_ToolsHandler.IsMirroring)
                {
                    var mirroredPosition = levelCreator.GetMirroredPosition(position);
                    var mirroredRotation = m_MirrorBrushObject.transform.rotation;
                    if (m_MirrorBrushObject.activeInHierarchy)
                    {
                        PlaceObject(m_LevelManager, selectedObj, mirroredPosition, mirroredRotation, scale);
                    }
                }
            }
            levelCreator.GenerateSnapCornersFaces();
            m_OnPlaceAction?.Invoke();
            var count = CountStackedObjects(m_LevelManager, selectedObj.name, position, rotation, scale);
            Helper.SendModOutput($"Stacked object: {selectedObj.name} of {count}", Helper.LogType.Success);
            isMouseMoved = false;
        }

        private void PlaceObject(LevelManager levelManager, GameObject selectedObj, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject gameObject = Instantiate(selectedObj, position, rotation);
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
                LevelObject levelObject = new LevelObject(gameObject, selectedObj.name, null, int.MinValue);
                EnableAfterFrame component = gameObject.GetComponent<EnableAfterFrame>();
                if (component)
                {
                    component.obj.SetActive(true);
                }
                levelManager.AddNewPlacedLevelObject(levelObject, true);
            }
        }

        private int CountStackedObjects(LevelManager levelManager, string objectName, Vector3 position, Quaternion rotation, Vector3 scale)
        {
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

        /*
        public void ExecuteCmd(string cmd = null)
        {
            if (!Helper.isQOLModLoaded) return;
            cmd ??= commandInput;
            GUI.FocusControl(null);

            var chatManagerPatches = AccessTools.TypeByName("QOL.Patches.ChatManagerPatches");
            var findAndRunCommandMethod = AccessTools.Method(chatManagerPatches, "FindAndRunCommand");
            findAndRunCommandMethod.Invoke(null, [cmd]);
            Helper.currentOutputMsg = AccessTools.TypeByName("QOL.Helper").GetField("currentOutputMsg").GetValue(null) as string;
        }
        */

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
}