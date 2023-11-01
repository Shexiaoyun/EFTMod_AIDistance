using AI_Distance;
using BepInEx;
using BepInEx.Configuration;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AI_Distance
{
    [BepInPlugin("com.chilidog.AI_Distance", "AI_Distance", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> iscount50;
        internal static ConfigEntry<bool> iscount100;
        internal static ConfigEntry<float> zoomSilder;
        internal static ConfigEntry<float> boxPosX;
        internal static ConfigEntry<float> boxPosY;
        private static GameObject Hook = new GameObject("AIDistance");
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo("Plugin AI_Distance is loaded!");

            iscount50 = Config.Bind("Booleans", "50 米", true, "显示 50 米内的敌人数量");
            iscount100 = Config.Bind("Booleans", "100 米", true, "显示 100 米内的敌人数量");
            zoomSilder = Config.Bind("Values", "缩放作用距离", 100f, 
                new ConfigDescription("使标记开启缩放的距离", 
                new AcceptableValueRange<float>(50f, 250f)));
            boxPosX = Config.Bind("Values", "X 偏移", 0f,
                new ConfigDescription("X 偏移的大小",
                new AcceptableValueRange<float>(-50f, 50f)));
            boxPosY = Config.Bind("Values", "Y 偏移", 0f,
                new ConfigDescription("Y 偏移的大小",
                new AcceptableValueRange<float>(-50f, 50f)));
            Hook.AddComponent<AIDistancePatch>();
            DontDestroyOnLoad(Hook);
        }
    }
}
