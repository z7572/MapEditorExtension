using BepInEx;
using System;
using System.Reflection;
using HarmonyLib;

namespace MapEditorExtension
{
    [BepInPlugin("z7572.MapEditorExtension", "MapEditorExtension", "1.3")]
    public class MapEditorExtension : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogInfo("MapEditorExtension is loaded!");
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
}
