using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using BepInEx;
using UnityEngine;
using EFTApi;

namespace AI_Distance
{
    internal class AIDistancePatch : MonoBehaviour
    {
        internal enum team
        {

            scav        = 0,
            usec        = 1,
            bear        = 2,
            boss        = 3,
            followers   = 4,
            raiders     = 5,
            exusec      = 6,
            others      = 7
        };

        private void Start()
        {
            AIDistancePatch.initMethods();
            base.InvokeRepeating("Cal", 1f, 2); // 暂设为 2s
            base.InvokeRepeating("initBotList", 1f, 0.01f);
        }

        private void OnGUI()
        {
            // 渲染 GUI
            int screenHeight = Screen.height, screenWidth = Screen.width;
            foreach (Tuple<WildSpawnType, team, EPlayerSide, UnityEngine.Vector3, float> info in botInfos)
            {
                UnityEngine.Vector3 screenPos = info.Item4;
                // ----------------------------------------------------------------------------------
                // clamp, 由于投影矩阵需要除以 -z，直接 clamp 会导致背对的 box 朝对角移动
                if (screenPos.x > screenWidth || screenPos.x < 0 ||
                    screenPos.y > screenHeight || screenPos.y < 0)
                {
                    screenPos = clamp(screenPos);
                }
                // ----------------------------------------------------------------------------------
                // 画出 box, 并根据距离线性化 box 的大小
                float distance = info.Item5;
                float t = distance / Plugin.zoomSilder.Value; // 在指定范围内可以缩放 
                t = Mathf.Clamp01(t);
                int boxSize = (int)Mathf.Lerp(Plugin.minBoxSize.Value, 
                    Plugin.maxBoxSize.Value, 1 - t); // t 或 1 - t，根据需求更改

                // ----------------------------------------------------------------------------------
                // viewport 原点在左下角，GUI 原点在左上角
                // 通过角色身高与 distance 将 box放在合适的位置
                int GUIX = (int)(screenPos.x - boxSize / 2 + Plugin.boxPosX.Value);
                int GUIY = (int)((screenHeight - screenPos.y) + Plugin.boxPosY.Value);
                GUIX = Mathf.Clamp(GUIX, 0, screenWidth);
                GUIY = Mathf.Clamp(GUIY, 0, screenHeight - boxSize);

                // ----------------------------------------------------------------------------------
                // GUI 设置
                int index = ((int)info.Item1), team = ((int)info.Item2);
                EPlayerSide side = info.Item3;
                style.fontSize = boxSize / 6;

                string boxLetter = "Name: ";
                if (side == EPlayerSide.Savage)
                {
                    boxLetter += $"{AIDistancePatch.SetColor(botNames[index], Color.white)}\n";
                }
                else if (side == EPlayerSide.Usec)
                {
                    boxLetter += $"{AIDistancePatch.SetColor(Plugin.usecName.Value, Color.white)}\n";
                }
                else
                {
                    boxLetter += $"{AIDistancePatch.SetColor(Plugin.bearName.Value, Color.white)}\n";
                }

                boxLetter += "Teams: " + teamColors[team] + "\n" +
                    "Dis: " + $"{AIDistancePatch.SetColor(distance.ToString("#0.00"), Color.Lerp(Color.cyan, Color.red, 1 - t))}";

                GUI.Box(new Rect(GUIX, GUIY, boxSize, boxSize), boxLetter, style);
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

        // 代码来自 AiDistance_26282 @jokesun
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

        // 代码来自 AI-Marker @ATMod
        private static team BotRole(WildSpawnType roleType)
        {
            if (roleType == WildSpawnType.assault |
                roleType == WildSpawnType.marksman |
                roleType == WildSpawnType.assaultGroup |
                roleType == WildSpawnType.gifter)
            {
                return team.scav;
            }
            else if (roleType == WildSpawnType.followerBully |
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
            else if (roleType == WildSpawnType.bossBully |
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
            else if (roleType == WildSpawnType.pmcBot)
            {
                return team.raiders;
            }
            else if (roleType == WildSpawnType.exUsec)
            {
                return team.exusec;
            }
            else
            {
                return team.others;
            }

        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private void initBotList()
        {
            botInfos.Clear();
            if (Input.GetKeyDown(Plugin.isEnable.Value))
                isEnable = !isEnable;
            if (!isEnable)
                return;
            if (!EFTApi.EFTGlobal.GameWorld)
                return;

            GameWorld instance = EFTApi.EFTGlobal.GameWorld;
            if (null == instance)
                return;
            if (instance.RegisteredPlayers.Count <= 1)
                return;
            if (null == Camera.main)
                return;

            style.normal.background = MakeTex(1, 1,
                new Color(0.5f, 0.5f, 0.5f, Plugin.alphaTransparent.Value));
            AIDistancePatch.camPos = Camera.main.transform.position;
            foreach (IPlayer bot in instance.RegisteredPlayers)
            {
                if (bot.IsYourPlayer) continue;
                // 将 camera 后面的坐标投影到前面
                // bot.MainParts[BodyPartType.head].Position + bot.Transform.TransformPoint( 
                // bot.Transform.localPosition); // LocalPos 与 worldPos 一致?(奇怪)
                UnityEngine.Vector3 newPos; // the screen space
                UnityEngine.Vector3 bodyPos = bot.MainParts[BodyPartType.body].Position; // world space
                UnityEngine.Vector3 delta =
                    bodyPos - AIDistancePatch.camPos;
                float dot = UnityEngine.Vector3.Dot(Camera.main.transform.forward, delta);
                if (dot < 0)
                {
                    UnityEngine.Vector3 projectedPos = AIDistancePatch.camPos + (delta -
                        Camera.main.transform.forward * dot * 1.01f);
                    newPos = Camera.main.WorldToScreenPoint(projectedPos);
                }
                else
                {
                    newPos = Camera.main.WorldToScreenPoint(bodyPos);
                }
                EPlayerSide side = bot.Side;
                team botRole = (side == EPlayerSide.Savage) ? (BotRole(bot.Profile.Info.Settings.Role)) :
                    (side == EPlayerSide.Usec ? team.usec : team.bear);
                float distance = UnityEngine.Vector3.Distance(bodyPos, camPos);

                botInfos.Add(new Tuple<WildSpawnType, team, EPlayerSide, UnityEngine.Vector3, float>
                    (bot.Profile.Info.Settings.Role, botRole, side, newPos, distance));
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
            this.nearestAi = float.MaxValue;
            foreach (IPlayer bot in instance.RegisteredPlayers)
            {
                if (bot.IsYourPlayer) continue;
                numAll++;
                float distance = UnityEngine.Vector3.Distance(bot.Transform.position, position);
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
            style.richText = true;
            style.wordWrap = true;
        }

        // ------------------------------------------------------------------------------------------------
        public int num50;

        public int num100;

        public int numAll;

        public float nearestAi;

        private bool isEnable = false;

        private const int offsetUp = 0, offsetRight = 50, offsetDown = 50, offsetLeft = 0;

        private static GUIStyle style = new GUIStyle();

        private static UnityEngine.Vector3 camPos;

        private static List<Tuple<WildSpawnType, team, EPlayerSide, UnityEngine.Vector3, float>> botInfos =
            new List<Tuple<WildSpawnType, team, EPlayerSide, UnityEngine.Vector3, float>>();

        private static string[] teamColors = new string[]
        {
            $"{SetColor("Scav", Color.yellow)}",
            $"{SetColor("Usec", Color.cyan)}",
            $"{SetColor("Bear", new Color(129f,   0f, 243f))}",
            $"{SetColor("Boss", Color.red)}",
            $"{SetColor("Followers", Color.blue)}",
            $"{SetColor("Raiders", Color.green)}",
            $"{SetColor("Exusec", Color.black)}",
            $"{SetColor("Others", Color.white)}"

        };

        private static string[] botNames = new string[]
        {
            "Marksman",
            "Assault",
            "BossTest",
            "BossBully",
            "FollowerTest",
            "FollowerBully",
            "BossKilla",
            "BossKojaniy",
            "FollowerKojaniy",
            "PmcBot",
            "CursedAssault",
            "BossGluhar",
            "FollowerGluharAssault",
            "FollowerGluharSecurity",
            "FollowerGluharScout",
            "FollowerGluharSnipe",
            "FollowerSanitar",
            "BossSanitar",
            "Test",
            "AssaultGroup",
            "SectantWarrior",
            "SectantPriest",
            "BossTagilla",
            "FollowerTagilla",
            "ExUsec",
            "Gifter",
            "BossKnight",
            "FollowerBigPipe",
            "FollowerBirdEye",
            "BossZryachiy",
            "FollowerZryachiy",
            "Who?",
            "BossBoar",
            "FollowerBoar",
            "ArenaFighter",
            "ArenaFighterEvent",
            "BossboarSniper",
            "CrazyAssaultEvent"
        };
    }
}
