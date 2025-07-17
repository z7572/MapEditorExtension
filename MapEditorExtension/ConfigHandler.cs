using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace MapEditorExtension
{

    public static class ConfigHandler
    {
        private static readonly Dictionary<string, ConfigEntryBase> EntriesDict = new(StringComparer.InvariantCultureIgnoreCase);

        private const string MenuSect = "Menu Options";
        private const string MapSect = "Map Options";

        public static void InitConfig(ConfigFile config)
        {
            var mapEditorWindowKeybindEntry = config.Bind(MenuSect, "MapEditorWindowKeybind", new KeyboardShortcut(KeyCode.F1),
                "GUI快捷键 (可使用双按键组合, 用 \"+\" 连接)");
            var mapEditorWindowKeybindEntryKey = mapEditorWindowKeybindEntry.Definition.Key;
            EntriesDict[mapEditorWindowKeybindEntryKey] = mapEditorWindowKeybindEntry;

            mapEditorWindowKeybindEntry.SettingChanged += (_, _) =>
            {
                var shortcut = mapEditorWindowKeybindEntry.Value;
                var guiManager = ExtensionUI.Instance;

                guiManager.extensionWindowKey1 = shortcut.MainKey;
                guiManager.extentionWindowKey2 = shortcut.Modifiers.LastOrDefault();

                guiManager.singleExtensionKey = guiManager.extentionWindowKey2 == KeyCode.None;
            };

            var mapSizeMinEntry = config.Bind(MapSect, "MapSizeMin", 1f, "地图最小尺寸 (原版: 5)");
            EntriesDict[mapSizeMinEntry.Definition.Key] = mapSizeMinEntry;

            var mapSizeMaxEntry = config.Bind(MapSect, "MapSizeMax", 30f, "地图最大尺寸 (原版: 15)");
            EntriesDict[mapSizeMaxEntry.Definition.Key] = mapSizeMaxEntry;
        }

        public static T GetEntry<T>(string entryKey, bool defaultValue = false)
            => defaultValue ? (T)EntriesDict[entryKey].DefaultValue : (T)EntriesDict[entryKey].BoxedValue;

        public static void ModifyEntry(string entryKey, string value)
            => EntriesDict[entryKey].SetSerializedValue(value);

        public static void ResetEntry(string entryKey)
        {
            var configEntry = EntriesDict[entryKey];
            configEntry.BoxedValue = configEntry.DefaultValue;
        }

        public static bool EntryExists(string entryKey)
            => EntriesDict.ContainsKey(entryKey);

        public static string[] GetConfigKeys() => EntriesDict.Keys.ToArray();

    }
}