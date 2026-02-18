using BepInEx;
using System;
using System.Reflection;
using HarmonyLib;

namespace EditorExtension;

[BepInPlugin("z7572.EditorExtension", "EditorExtension", "1.6")]
public class EditorExtension : BaseUnityPlugin
{
    public void Awake()
    {
        Logger.LogInfo("EditorExtension is loaded!");
        try
        {
            Logger.LogInfo("Loading configuration options from config file...");
            ConfigHandler.InitConfig(Config);
        }
        catch (Exception e)
        {
            Logger.LogError("Exception on loading configuration: " + e.StackTrace + e.Message + e.Source + e.InnerException);
        }            
        try
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }
}
