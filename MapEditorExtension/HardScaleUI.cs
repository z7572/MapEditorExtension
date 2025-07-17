using System;
using HarmonyLib;
using UnityEngine;
using LevelEditor;
using System.Collections.Generic;

namespace MapEditorExtension
{

    public class HardScaleUI : MonoBehaviour
    {
        public static HardScaleUI Instance { get; private set; }
        private bool _mShowScaleUI;
        private Rect scaleMenuRect = new(Screen.width - 320 - 100, Screen.height - 170 - 200, 320, 170);

        private GameObject selectedObject;
        private Vector3 currentScale;
        private Vector3 maxSingle;
        private Vector3 minSingle;
        private float lastSnap = 0.05f;
        private string snap = "0.05";
        protected bool lastMouseDown = false;
        protected List<Vector3> originalScales = new();
        protected string[] clipBoard = new string[3] { "1", "1", "1" };

        private void Start()
        {
            Instance = this;
        }

        private void OnGUI()
        {
            if (_mShowScaleUI)
                scaleMenuRect = GUILayout.Window(1002, scaleMenuRect, ScaleWindow, "HardScale");
        }

        private void Update()
        {
            if (ExtensionUI.canScale && !WorkshopStateHandler.IsPlayTestingMode && Input.GetMouseButtonDown(2))
            {
                if (LevelCreator.Instance.CastRaycastFromMouse(out RaycastHit hit))
                {
                    var hittedObject = hit.collider.transform.root.gameObject;
                    if (!Helper.IsRaycastSatisfied(hittedObject, "select", true) || hittedObject.name.ToLower().Contains("gun"))
                    {
                        selectedObject = null;
                        _mShowScaleUI = false;
                    }
                    else if (selectedObject != hittedObject)
                    {
                        selectedObject = hittedObject;
                        currentScale = hittedObject.transform.localScale;
                        maxSingle = currentScale * 3;
                        minSingle = currentScale * 0.1f;
                        _mShowScaleUI = true;
                    }
                }
            }
            if (lastMouseDown != Input.GetMouseButton(0))
            {
                lastMouseDown = Input.GetMouseButton(0);
                if (lastMouseDown) MouseDown();
                else MouseUp();
            }
            if (selectedObject == null) _mShowScaleUI = false;
        }

        private void ScaleWindow(int window)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Selected: " + selectedObject.name);
            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Snap to");
            snap = GUILayout.TextField(snap, GUILayout.Width(100));
            try
            {
                float newSnap = Convert.ToSingle(snap);
                if (newSnap < 0) newSnap = 0;
                lastSnap = newSnap;
            }
            catch (Exception) { }
            GUILayout.Label(lastSnap.ToString(), GUILayout.Width(100));
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            if (!lastMouseDown)
            {
                minSingle = currentScale * 0.1f;
                maxSingle = currentScale * 3;
                for (int i = 0; i < 3; i++) if (maxSingle[i] < 0.1f) maxSingle[i] = 0.1f;
            }

            currentScale[1] = SingleScaleSlider(currentScale[1], minSingle[1], maxSingle[1], "y ");
            currentScale[2] = SingleScaleSlider(currentScale[2], minSingle[2], maxSingle[2], "z ");
            if (UICought && lastSnap > 0.0001f && originalScales.Count == 1)
            {
                for (int i = 1; i < 3; i++)
                {
                    currentScale[i] = originalScales[0][i] + lastSnap * (Mathf.Round((currentScale[i] - originalScales[0][i]) / lastSnap));
                    if (currentScale[i] < 0) currentScale[i] = 0;
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Paste bin: (Ctrl+C/Ctrl+V)");
            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            if (GUILayout.Button("Copy", GUILayout.Width(50)) || Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftControl))
            {
                for (int i = 0; i < 3; i++) clipBoard[i] = currentScale[i].ToString();
            }
            if (GUILayout.Button("Paste", GUILayout.Width(50)) || Input.GetKeyDown(KeyCode.V) && Input.GetKey(KeyCode.LeftControl))
            {
                try
                {
                    LogPreState();
                    for (int i = 0; i < 3; i++) currentScale[i] = Convert.ToSingle(clipBoard[i]);
                    DoSingleScale(currentScale);
                    UpdatePostState();
                }
                catch (Exception) { };
            }
            GUILayout.FlexibleSpace();
            for (int i = 1; i < 3; i++) clipBoard[i] = GUILayout.TextField(clipBoard[i], GUILayout.Width(60));
            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            if (UICought)
                DoSingleScale(currentScale);

            GUI.DragWindow();
        }

        bool UICought = false;
        void MouseDown()//从鼠标按下开始记录
        {
            if (!_mShowScaleUI) return;
            if (!scaleMenuRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))) return;

            UICought = true;
            LogPreState();
        }
        void LogPreState()
        {
            originalScales.Clear();
            originalScales.Add(selectedObject.transform.localScale);
        }
        void MouseUp()
        {
            if (!UICought) return;
            UpdatePostState();
            UICought = false;
        }
        void UpdatePostState()
        {
            originalScales.Clear();
        }

        protected float SingleScaleSlider(float value, float min, float max, string name)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            var ret = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(220));
            GUILayout.Label(name);
            GUILayout.Label(ret.ToString(), GUILayout.Width(60));
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            return ret;
        }

        private void DoSingleScale(Vector3 scale)
        {
            if (selectedObject != null)
            {
                var levelObjects = Traverse.Create(LevelManager.Instance).Field("m_PlacedLevelObjects").GetValue<List<LevelObject>>();
                foreach (var levelObject in levelObjects)
                {
                    if (levelObject.VisibleObject == selectedObject.gameObject)
                    {
                        levelObject.VisibleObject.transform.localScale = scale;
                        levelObject.Scale = new Vector2(scale.z, scale.y);
                    }
                }
            }
        }
    }
}