using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditor;

namespace EditorExtension;

public static class Helper
{
    private static readonly MethodInfo _castRaycastMethod;

    static Helper()
    {
        _castRaycastMethod = AccessTools.Method(typeof(LevelCreator), "CastRaycastFromMouse", [typeof(RaycastHit).MakeByRefType()]);
    }

    public static Quaternion GetMirroredRotation(bool doMirrorRot, GameObject brushObject, GameObject dragObj = null)
    {
        var targetObj = brushObject ? brushObject : dragObj;

        if (targetObj && targetObj.name.ToLower().Contains("gun"))
        {
            doMirrorRot = false;
        }

        var result = Quaternion.identity;
        int angle = 0;
        if (doMirrorRot) angle = 180;

        if (targetObj)
        {
            if (targetObj.GetComponent<CheckPreRotation>() == null)
            {
                targetObj.AddComponent<CheckPreRotation>();
            }
            var check = targetObj.GetComponent<CheckPreRotation>();
            var preRot = check.preRotationAngle;

            Vector3 cleanEuler = SanitizeEuler(targetObj.transform.rotation.eulerAngles);

            if (targetObj.GetComponent<ProprFlipAroundYIndeadOfX>())
            {
                Vector3 eulerAngles = cleanEuler;
                eulerAngles = new Vector3(eulerAngles.x, eulerAngles.y + angle, eulerAngles.z);
                if (ExtensionUI.isHorizonalMirror)
                {
                    eulerAngles = new Vector3(eulerAngles.x, (angle - eulerAngles.y - preRot.y) - preRot.y, eulerAngles.z);
                }
                result = Quaternion.Euler(eulerAngles);
            }
            else
            {
                Vector3 eulerAngles2 = cleanEuler;
                eulerAngles2 = new Vector3(eulerAngles2.x + angle, eulerAngles2.y, eulerAngles2.z);
                if (ExtensionUI.isHorizonalMirror)
                {
                    eulerAngles2 = new Vector3((angle - eulerAngles2.x - preRot.x) - preRot.x, eulerAngles2.y, eulerAngles2.z);
                }
                result = Quaternion.Euler(eulerAngles2);
            }
        }
        return result;
    }

    /// <summary>
    /// 清洗欧拉角，强制消除 Z 轴的 180 度翻转，将其标准化为 (X, Y, 0)<br/>
    /// 解决 Unity (x, 0, 0) 变成 (180-x, 180, 180) 导致的计算错误
    /// </summary>
    public static Vector3 SanitizeEuler(Vector3 inputEuler)
    {
        if (Mathf.Abs(inputEuler.z) < 1f) return inputEuler;

        float newX = 180f - inputEuler.x;
        float newY = inputEuler.y + 180f;

        newX = (newX % 360f + 360f) % 360f;
        newY = (newY % 360f + 360f) % 360f;

        return new Vector3(newX, newY, 0f);
    }

    public static bool CastRaycastFromMouse(LevelCreator instance, out RaycastHit hit)
    {
        hit = default;
        if (_castRaycastMethod == null || !instance) return false;

        object[] parameters = [null];
        var result = (bool)_castRaycastMethod.Invoke(instance, parameters);
        hit = (RaycastHit)parameters[0];

        return result;
    }

    // A stupid output method. nvm
    public static bool IsRaycastSatisfied(GameObject hittedObj, string action = "select", bool allowGround = false, bool output = true)
    {
        if (!hittedObj)
        {
            if (output) SendModOutput($"Cannot {action} nothing!", LogType.Warning);
            return false;
        }
        if (hittedObj.name.StartsWith("GROUND") && !allowGround)
        {
            if (output) SendModOutput($"Cannot {action} GROUND!", LogType.Warning);
            return false;
        }
        if (hittedObj.name == "Barriers")
        {
            if (output) SendModOutput($"Cannot {action} empty space!", LogType.Warning);
            return false;
        }
        if (hittedObj.name == "Map")
        {
            if (output) SendModOutput($"Cannot {action} spawn point!", LogType.Warning);
            return false;
        }
        return true;
    }

    public static void LoadAndExecute(string sceneName, Action<GameObject[]> loadedObjsCallback = null)
    {
        CoroutineRunner.Run(LoadCorotine());

        IEnumerator LoadCorotine()
        {
            if (LoadedScene.IsValid() && LoadedScene.isLoaded)
            {
                var unloadAsync = SceneManager.UnloadSceneAsync(LoadedScene);
                yield return new WaitUntil(() => unloadAsync.isDone);
            }

            var loadAsync = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return new WaitUntil(() => loadAsync.isDone);
            var scene = SceneManager.GetSceneByName(sceneName);
            LoadedScene = scene;
            var rootObjs = scene.GetRootGameObjects();
            foreach (var obj in rootObjs)
            {
                obj.GetComponent<MonoBehaviour>().enabled = false;

                obj.gameObject.SetActive(false);
            }
            loadedObjsCallback?.Invoke(rootObjs);
        }
    }

    public static void SendModOutput(string msg, LogType logType = LogType.Info, bool toggleState = true)
    {
        var msgColor = logType switch
        {
            // Enabled => green, disabled => gray
            LogType.Success => toggleState ? "<color=#86C691>" : "<color=#858585>",
            LogType.Warning => "<color=#FF99A4>",
            _ => ""
        };
        var msgColorPostfix = string.IsNullOrEmpty(msgColor) ? string.Empty : "</color>";

        currentOutputMsg = msgColor + msg + msgColorPostfix;
    }
    public enum LogType
    {
        Info,
        Success,
        Warning
    }

    public static string currentOutputMsg
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            ExtensionUI.Instance.ResetWindowSize();
        }
    } = "<color=#999999><i>Not yet evaluated (<color=#569CD6>string</color>)</i></color>";

    public static Scene LoadedScene;
    public static GameObject ChatField;
}