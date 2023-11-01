using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using BepInEx;
using UnityEngine;
using EFTApi;
using JetBrains.Annotations;
using System.Numerics;
using UnityEngine.UIElements;
using static EFT.ScenesPreset;

namespace AI_Distance
{
    internal class AIDistancePatch : MonoBehaviour
    {
        internal enum team
        {
            scav = 0,
            boss = 1,
            followers = 2,
            pmc = 3,
            exusec = 4,
            others = 5
        };

        private void Start()
        {
            AIDistancePatch.initMethods();
            base.InvokeRepeating("Cal", 1f, 2); // 暂设为 2s
            base.InvokeRepeating("initBotList", 1f, 0.01f);
        }

        private void OnGUI()
        {
            int screenHeight = Screen.height, screenWidth = Screen.width;
            //结合透视投影矩阵与 GUI 矩阵，将名字渲染到 bot 脚下
            foreach (Tuple<WildSpawnType, team, UnityEngine.Vector3> info in botInfos)
            {
                UnityEngine.Vector3 screenPos = info.Item3;
                // ----------------------------------------------------------------------------------
                // clamp, 由于投影矩阵需要除以 -z，直接 clamp 会导致背对的 box 朝对角移动
                if(screenPos.x > screenWidth || screenPos.x < 0 ||
                    screenPos.y > screenHeight || screenPos.y < 0)
                {
                    screenPos = clamp(screenPos);
                }
                // ----------------------------------------------------------------------------------
                // 画出 box, 并根据距离线性化 box 的大小
                float distance = screenPos.z;
                float t = distance / Plugin.zoomSilder.Value; // 在指定范围内可以缩放 
                t = Mathf.Clamp01(t);
                int boxSize = (int)Mathf.Lerp(minBoxSize, maxBoxSize, 1 - t);

                // ----------------------------------------------------------------------------------
                // viewport 原点在左下角，GUI 原点在左上角
                // 通过角色身高与 distance 将 box放在合适的位置
                int GUIX = (int)(screenPos.x - boxSize / 2 + Plugin.boxPosX.Value);
                int GUIY = (int)((screenHeight - screenPos.y) + Plugin.boxPosY.Value);
                GUIX = Mathf.Clamp(GUIX, 0, screenWidth);
                GUIY = Mathf.Clamp(GUIY, 0, screenHeight - boxSize);
                GUI.Box(new Rect(GUIX, GUIY, boxSize, boxSize), $"Load");
            }

            string text = " Bots : ";
            if (this.nearestAi == 0)
            {
                text = "-";
            }
            else
            {
                List<string> list = new List<string>();
                if (Plugin.iscount50.Value)
                {
                    list.Add(AIDistancePatch.SetColor(this.num50.ToString(), Color.white));
                }
                if (Plugin.iscount100.Value)
                {
                    list.Add(AIDistancePatch.SetColor(this.num100.ToString(), Color.red));
                }
                list.Add(AIDistancePatch.SetColor(this.numAll.ToString(), Color.blue));
                text += string.Join(" / ", list);

                GUIStyle guistyle = new GUIStyle();
                GUILayout.Label(text, guistyle, Array.Empty<GUILayoutOption>());
            }
        }

        // 屏幕外标记方法，代码来自 https://indienova.com/indie-game-development/unity-off-screen-objective-marker/
        private UnityEngine.Vector3 clamp(UnityEngine.Vector3 newPos)
        {
            UnityEngine.Vector2 center = new(Screen.width / 2, Screen.height / 2);
            float k = (newPos.y - center.y) / (newPos.x - center.x);

            if (newPos.y - center.y > 0)
            {
                newPos.y = Screen.height - AIDistancePatch.offsetUp;
                newPos.x = center.x + (newPos.y - center.y) / k;
            }
            else
            {
                newPos.y = AIDistancePatch.offsetDown;
                newPos.x = center.x + (newPos.y - center.y) / k;
            }

            if (newPos.x > Screen.width - AIDistancePatch.offsetRight)
            {
                newPos.x = Screen.width - AIDistancePatch.offsetRight;
                newPos.y = center.y + (newPos.x - center.x) * k;
            }
            else if (newPos.x < AIDistancePatch.offsetLeft)
            {
                newPos.x = AIDistancePatch.offsetLeft;
                newPos.y = center.y + (newPos.x - center.x) * k;
            }

            return newPos;
        }

        public static string SetColor(string str, Color color)
        {
            return string.Concat(new string[]
            {
                "<color=#",
                ColorUtility.ToHtmlStringRGB(color),
                ">",
                str,
                "</color>"
            });
        }

        internal static team BotRole(WildSpawnType roleType)
        {
            if(roleType == WildSpawnType.assault | 
                roleType == WildSpawnType.marksman | 
                roleType == WildSpawnType.assaultGroup | 
                roleType == WildSpawnType.gifter)
            {
                return team.scav;
            }
            else if(roleType == WildSpawnType.followerBully |
                roleType == WildSpawnType.followerKojaniy |
                roleType == WildSpawnType.followerGluharAssault |
                roleType == WildSpawnType.followerGluharSecurity |
                roleType == WildSpawnType.followerGluharScout |
                roleType == WildSpawnType.followerGluharSnipe |
                roleType == WildSpawnType.followerSanitar |
                roleType == WildSpawnType.followerTagilla |
                roleType == WildSpawnType.followerZryachiy)
            {
                return team.followers;
            }
            else if(roleType == WildSpawnType.bossBully |
                    roleType == WildSpawnType.bossKilla |
                    roleType == WildSpawnType.bossKilla |
                    roleType == WildSpawnType.bossKojaniy |
                    roleType == WildSpawnType.bossGluhar |
                    roleType == WildSpawnType.bossSanitar |
                    roleType == WildSpawnType.bossTagilla |
                    roleType == WildSpawnType.bossKnight |
                    roleType == WildSpawnType.bossZryachiy |
                    roleType == WildSpawnType.followerBirdEye |
                    roleType == WildSpawnType.followerBigPipe)
            {
                return team.boss;
            }
            else if(roleType == WildSpawnType.pmcBot)
            {
                return team.pmc;
            }
            else if(roleType == WildSpawnType.exUsec)
            {
                return team.exusec;
            }
            else
            {
                return team.others;
            }
        }

        private void initBotList()
        {
            botInfos.Clear();
            if (!EFTApi.EFTGlobal.GameWorld)
            {
                return;
            }
            GameWorld instance = EFTApi.EFTGlobal.GameWorld;
            if (null == instance)
            {
                return;
            }
            if (instance.RegisteredPlayers.Count <= 1)
            {
                return;
            }
            if (null == Camera.main)
            {
                return;
            }
            AIDistancePatch.camPos = Camera.main.transform.position;
            foreach (IPlayer bot in instance.RegisteredPlayers)
            {
                if (bot.IsYourPlayer) continue;
                // 将 camera 后面的坐标投影到前面
                // bot.MainParts[BodyPartType.head].Position + bot.Transform.TransformPoint( 
                // bot.Transform.localPosition); // LocalPos，是局部坐标吗？
                UnityEngine.Vector3 newPos;
                UnityEngine.Vector3 oriPos = 
                UnityEngine.Vector3 delta =
                    oriPos - AIDistancePatch.camPos;
                float dot = UnityEngine.Vector3.Dot(Camera.main.transform.forward, delta);
                if(dot < 0)
                {
                    UnityEngine.Vector3 projectedPos = AIDistancePatch.camPos + (delta -
                        Camera.main.transform.forward * dot * 1.01f);
                    newPos = Camera.main.WorldToScreenPoint(projectedPos);
                }
                else
                {
                    newPos = Camera.main.WorldToScreenPoint(oriPos);
                }
                team botRole = BotRole(bot.Profile.Info.Settings.Role);
                botInfos.Add(new Tuple<WildSpawnType, team, UnityEngine.Vector3>
                    (bot.Profile.Info.Settings.Role, botRole, newPos));
            }

        }
            private void Cal()
        {
            this.num50 = 0;
            this.num100 = 0;
            this.numAll = 0;
            this.nearestAi = 0;
            if (!EFTApi.EFTGlobal.GameWorld)
            {
                return;
            }
            GameWorld instance = EFTApi.EFTGlobal.GameWorld;
            if (null == instance)
            {
                return;
            }
            if (instance.RegisteredPlayers.Count <= 1)
            {
                return;
            }
            if (null == Camera.main)
            {
                return;
            }
            UnityEngine.Vector3 position = Camera.main.transform.position;
            this.nearestAi = int.MaxValue;
            foreach (IPlayer bot in instance.RegisteredPlayers)
            {
                if (bot.IsYourPlayer) continue;
                numAll++;
                int distance = this.MySqrt((int)(bot.Transform.position - position).sqrMagnitude);
                if (distance < this.nearestAi)
                {
                    this.nearestAi = distance;
                }
                if (distance < 50)
                {
                    this.num50++;
                }
                else if (distance < 100)
                {
                    this.num100++;
                }
            }
        }

        private static void initMethods()
		{
            
			Type extType = typeof(object);
			Type[] types = typeof(Player).Assembly.GetTypes();
			for (int i = 0; i < types.Length; i++)
			{
				Type classType = types[i];
				MethodBase method;
				if (null != (method = classType.GetMethod("IsFollower", BindingFlags.Static | BindingFlags.Public)))
				{
					ParameterInfo[] parameters = method.GetParameters();
					if (parameters.Length == 1 && parameters[0].Name == "settings")
					{
						extType = classType;
					}
				}
				if (null != (method = classType.GetMethod("GetCorrectedNickname", BindingFlags.Static | BindingFlags.Public)))
				{
					AIDistancePatch.GetCorrectedNickname = ((object obj) => (string)method.Invoke(classType, new object[]
					{
						obj
					}));
				}
			}
			MethodInfo method_IsBoss = extType.GetMethod("IsBoss");
			MethodInfo method_IsFollower = extType.GetMethod("IsFollower");
			AIDistancePatch.IsBoss = ((object obj) => (bool)method_IsBoss.Invoke(extType, new object[]
			{
				obj
			}));
			AIDistancePatch.IsFollower = ((object obj) => (bool)method_IsFollower.Invoke(extType, new object[]
			{
				obj
			}));
		}

        public int MySqrt(int x)
        {
            if (x <= 1)
            {
                return x;
            }
            int i = 1;
            int num = x;
            while (i <= num)
            {
                int num2 = i + (num - i) / 2;
                if (num2 == x / num2)
                {
                    return num2;
                }
                if (num2 < x / num2)
                {
                    i = num2 + 1;
                }
                else
                {
                    num = num2 - 1;
                }
            }
            return num;
        }

        public static object SuperGet(object obj, string name)
        {
            Type type = obj.GetType();
            Func<object, object> func;
            if (!AIDistancePatch.cacheTypeProp.TryGetValue(new Tuple<Type, string>(type, name), out func))
            {
                FieldInfo fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (null != fi)
                {
                    if (fi.IsStatic)
                    {
                        func = ((object _) => fi.GetValue(type));
                    }
                    else
                    {
                        func = new Func<object, object>(fi.GetValue);
                    }
                }
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (null != property)
                {
                    func = new Func<object, object>(property.GetValue);
                }
                AIDistancePatch.cacheTypeProp.Add(new Tuple<Type, string>(type, name), func);
            }
            return func(obj);
        }
        // ------------------------------------------------------------------------------------------------
        public int num50;

        public int num100;

        public int numAll;

        public int nearestAi;

        private const float playerHeight = 1.80f;

        private const int offsetUp = 0, offsetRight = 50, offsetDown = 50, offsetLeft = 0;

        private const int minBoxSize = 50, maxBoxSize = 90;

        private static UnityEngine.Vector3 camPos;

        private static Func<object, bool> IsBoss;

        private static Func<object, bool> IsFollower;

        private static Func<object, string> GetCorrectedNickname;

        private static Dictionary<Tuple<Type, string>, Func<object, object>> cacheTypeProp =
            new Dictionary<Tuple<Type, string>, Func<object, object>>();

        private static List<Tuple<WildSpawnType, team, UnityEngine.Vector3>> botInfos = 
            new List<Tuple<WildSpawnType, team, UnityEngine.Vector3>>();

        private static string[] botNames = new string[37] {
    "marksman",
    "assault",
    "bossTest",
    "bossBully",
    "followerTest",
    "followerBully",
    "bossKilla",
    "bossKojaniy",
    "followerKojaniy",
    "pmcBot",
    "cursedAssault",
    "bossGluhar",
    "followerGluharAssault",
    "followerGluharSecurity",
    "followerGluharScout",
    "followerGluharSnipe",
    "followerSanitar",
    "bossSanitar",
    "test",
    "assaultGroup",
    "sectantWarrior",
    "sectantPriest",
    "bossTagilla",
    "followerTagilla",
    "exUsec",
    "gifter",
    "bossKnight",
    "followerBigPipe",
    "followerBirdEye",
    "bossZryachiy",
    "followerZryachiy",
    "bossBoar",
    "followerBoar",
    "arenaFighter",
    "arenaFighterEvent",
    "bossBoarSniper",
    "crazyAssaultEvent"};
    }
}
