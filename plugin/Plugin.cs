using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SaplingFixes
{
    // 1) Ultrawide: интерфейс игры масштабируется по ширине экрана (CanvasScaler 800x600, match = 0).
    //    На экранах шире 16:9 он становится слишком крупным и вылезает за верх/низ экрана.
    //    Масштаб считается так, будто экран 16:9 той же высоты; лишняя ширина даёт место по бокам.
    // 2) Русификатор: русский текст длиннее английского, поэтому не влезающий в рамку текст
    //    ужимается (best fit), а не обрезается.
    [BepInPlugin("ru.sapling.fixes", "Sapling RU + Ultrawide", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> UiFix;
        internal static ConfigEntry<float> MaxUiAspect;
        internal static ConfigEntry<int> ForceWidth;
        internal static ConfigEntry<int> ForceHeight;
        internal static ConfigEntry<bool> ForceFullscreen;
        internal static ConfigEntry<bool> ShrinkLongText;
        internal static ConfigEntry<float> MinTextScale;

        internal static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SaplingFixes");

        void Awake()
        {
            UiFix = Config.Bind("UI", "Enabled", true, "Не раздувать интерфейс на экранах шире 16:9");
            MaxUiAspect = Config.Bind("UI", "MaxUiAspect", 16f / 9f,
                "Соотношение сторон, под которое масштабируется интерфейс (16:9 = 1.7778)");
            ForceWidth = Config.Bind("Display", "ForceWidth", 0, "Принудительная ширина окна при запуске (0 = как выбрано в окне запуска)");
            ForceHeight = Config.Bind("Display", "ForceHeight", 0, "Принудительная высота окна при запуске (0 = как выбрано в окне запуска)");
            ForceFullscreen = Config.Bind("Display", "ForceFullscreen", true, "Полный экран при принудительном разрешении");
            ShrinkLongText = Config.Bind("Text", "ShrinkLongText", true, "Ужимать русский текст, если он не помещается в рамку");
            MinTextScale = Config.Bind("Text", "MinTextScale", 0.7f, "Минимальный размер ужатого текста относительно исходного");

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            PatchScreenWidthUsers(harmony);
            if (ForceWidth.Value > 0 && ForceHeight.Value > 0)
                Screen.SetResolution(ForceWidth.Value, ForceHeight.Value, ForceFullscreen.Value);
        }

        // ---------- длинный текст ----------

        static bool HasCyrillic(string s)
        {
            if (s == null)
                return false;
            foreach (char c in s)
                if (c >= 'Ѐ' && c <= 'ӿ')
                    return true;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
        static void TextSet(Text __instance, string value)
        {
            // Ужимаем только тексты, которые иначе обрежутся снизу; растущие вниз (Overflow) не трогаем
            if (!ShrinkLongText.Value || __instance.resizeTextForBestFit
                || __instance.verticalOverflow != VerticalWrapMode.Truncate || !HasCyrillic(value))
                return;
            int size = __instance.fontSize;
            __instance.resizeTextMaxSize = size;
            __instance.resizeTextMinSize = Math.Max(1, Mathf.RoundToInt(size * MinTextScale.Value));
            __instance.resizeTextForBestFit = true;
        }

        // ---------- ultrawide ----------

        // Эти компоненты в Start() запоминают Screen.width и потом считают от него расстояния между
        // иконками, сетку 3D-превью и их масштаб — то есть рассчитаны на интерфейс, привязанный к ширине.
        // Им отдаём ширину экрана 16:9 той же высоты, чтобы они совпадали с исправленным интерфейсом.
        static readonly string[] ScreenWidthUsers =
        {
            "AmountVisualizationView", "AmountVisualizationView2", "EnergyVisualizationView",
            "AbstractEditorUIView", "AbstractEditorView", "EpochSuccessfulCladesView",
            "FamilyView", "SpeciesDetailView", "SpeciesListView",
        };

        static void PatchScreenWidthUsers(Harmony harmony)
        {
            var transpiler = new HarmonyMethod(typeof(Plugin), nameof(ReplaceScreenWidth));
            foreach (var name in ScreenWidthUsers)
            {
                var start = AccessTools.Method(AccessTools.TypeByName(name), "Start");
                if (start != null)
                    harmony.Patch(start, transpiler: transpiler);
                else
                    Log.LogWarning($"{name}.Start не найден");
            }
        }

        static readonly MethodInfo ScreenWidthGetter = AccessTools.PropertyGetter(typeof(Screen), nameof(Screen.width));

        static IEnumerable<CodeInstruction> ReplaceScreenWidth(IEnumerable<CodeInstruction> instructions)
        {
            var virtualWidth = AccessTools.Method(typeof(Plugin), nameof(UiScreenWidth));
            foreach (var ins in instructions)
            {
                if (ins.Calls(ScreenWidthGetter))
                    ins.operand = virtualWidth;
                yield return ins;
            }
        }

        public static int UiScreenWidth()
        {
            int capped = Mathf.RoundToInt(Screen.height * MaxUiAspect.Value);
            return UiFix.Value && Screen.width > capped + 2 ? capped : Screen.width;
        }

        static readonly MethodInfo SetScaleFactor = AccessTools.Method(typeof(CanvasScaler), "SetScaleFactor");
        static readonly FieldInfo CanvasField = AccessTools.Field(typeof(CanvasScaler), "m_Canvas");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CanvasScaler), "HandleScaleWithScreenSize")]
        static bool HandleScaleWithScreenSize(CanvasScaler __instance)
        {
            if (!UiFix.Value)
                return true;
            var canvas = (Canvas)CanvasField.GetValue(__instance);
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.targetDisplay < Display.displays.Length)
            {
                var d = Display.displays[canvas.targetDisplay];
                screen = new Vector2(d.renderingWidth, d.renderingHeight);
            }
            float cap = MaxUiAspect.Value;
            if (screen.y <= 0 || screen.x / screen.y <= cap + 0.01f)
                return true;

            screen.x = screen.y * cap;
            Vector2 reference = __instance.referenceResolution;
            float scale;
            switch (__instance.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand:
                    scale = Mathf.Min(screen.x / reference.x, screen.y / reference.y);
                    break;
                case CanvasScaler.ScreenMatchMode.Shrink:
                    scale = Mathf.Max(screen.x / reference.x, screen.y / reference.y);
                    break;
                default:
                    float logW = Mathf.Log(screen.x / reference.x, 2f);
                    float logH = Mathf.Log(screen.y / reference.y, 2f);
                    scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, __instance.matchWidthOrHeight));
                    break;
            }
            SetScaleFactor.Invoke(__instance, new object[] { scale });
            return false;
        }
    }
}
