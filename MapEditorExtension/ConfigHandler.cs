using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace EditorExtension;

// From Monky's QOL-Mod
public static class ConfigHandler
{
    private static readonly Dictionary<string, ConfigEntryBase> EntriesDict = new(StringComparer.InvariantCultureIgnoreCase);

    private const string MenuSect = "Menu Options";
    private const string MapSect = "Map Options";

    public static void InitConfig(ConfigFile config)
    {
        var extensionWindowKeybindEntry = config.Bind(MenuSect, "ExtensionWindowKeybind", new KeyboardShortcut(KeyCode.F4),
            "GUI快捷键 (可使用双按键组合, 用 \"+\" 连接)");
        var extensionWindowKeybindEntryKey = extensionWindowKeybindEntry.Definition.Key;
        EntriesDict[extensionWindowKeybindEntryKey] = extensionWindowKeybindEntry;

        extensionWindowKeybindEntry.SettingChanged += (_, _) =>
        {
            var shortcut = extensionWindowKeybindEntry.Value;
            var guiManager = ExtensionUI.Instance;

            guiManager.extensionWindowKey1 = shortcut.MainKey;
            guiManager.extensionWindowKey2 = shortcut.Modifiers.LastOrDefault();

            guiManager.singleExtensionKey = guiManager.extensionWindowKey2 == KeyCode.None;
        };

        var mapSizeMinEntry = config.Bind(MapSect, "MapSizeMin", 1f, "地图最小尺寸 (原版: 5)");
        EntriesDict[mapSizeMinEntry.Definition.Key] = mapSizeMinEntry;

        var mapSizeMaxEntry = config.Bind(MapSect, "MapSizeMax", 65535f, "地图最大尺寸 (原版: 15)");
        EntriesDict[mapSizeMaxEntry.Definition.Key] = mapSizeMaxEntry;

        var fixCrateCollisionEntry = config.Bind(MapSect, "FixCrateCollision", false, "修复箱子碰撞\n" +
            "原版硬编码了箱子在y低于-11时就会失去与玩家的碰撞，开启可以使该值随地图尺寸动态变化，但如果其他玩家未开启此功能或未安装模组就会导致不同步\n" +
            "如果开启，则会一并禁用编辑器中箱子y低于-11的警告\n" +
            "（在主游戏里同样也生效，可在编辑器的菜单中调整该项配置）"); 
        EntriesDict[fixCrateCollisionEntry.Definition.Key] = fixCrateCollisionEntry;
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