using UnityEngine;

namespace MapEditorExtension
{

    public class Helper
    {
        public static bool IsRaycastSatisfied(GameObject hittedObj, string action = "select", bool allowGround = false, bool output = true)
        {
            if (hittedObj == null)
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

        public static void SendModOutput(string msg, LogType logType = LogType.Info, bool toggleState = true)
        {
            var msgColor = logType switch
            {
                // Enabled => green, disabled => gray
                LogType.Success => toggleState ? "<color=#006400>" : "<color=#56595C>",
                LogType.Warning => "<color=#CC0000>",
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
        public static bool isQOLModLoaded;
        public static string currentOutputMsg = "<color=#999999><i>Not yet evaluated (<color=#569CD6>string</color>)</i></color>";
    }

}