using System.Collections;
using System.Globalization;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using System.IO;
using RumbleModUI;
using Il2CppRUMBLE.Players.Subsystems;
using static Il2CppRUMBLE.MeshGeneration.PlayerCharacterBaker;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using HarmonyLib;
using Il2CppRUMBLE.CharacterCreation.Interactable;
using Il2CppRUMBLE.Managers;

namespace RumbleDarkMode
{
    public class Main : MelonMod
    {
        public Mod mod = new();

        private ModSetting<bool> gymToggle;
        private ModSetting<bool> parkToggle;
        private ModSetting<bool> ringToggle;
        private ModSetting<bool> pitToggle;

        private ModSetting<string> skyboxName;
        private ModSetting<string> skyboxTint;
        private ModSetting<float> skyboxExposure;
        private ModSetting<string> lightingColor;
        private ModSetting<string> treeColor;
        private ModSetting<string> treeShadowColor;
        private ModSetting<string> playerColor;

        public string currentScene = "Loader";

        public static Dictionary<Renderer, Texture2D> playerTextures = new();

        public override void OnLateInitializeMelon()
        {
            UI.instance.UI_Initialized += OnUIInit;
        }

        public void OnUIInit()
        {
            mod.ModName = "Rumble Dark Mode";
            mod.ModVersion = "2.2.7";
            mod.SetFolder("RumbleDarkMode");
            mod.AddDescription("Description", "", "Adds a dark mode option to Gym, Park, and Ring!", new Tags
            {
                IsSummary = true
            });
            mod.ModSaved += ApplyDarkMode;
            gymToggle = mod.AddToList("Toggle In Gym", true, 0, "Toggles Dark Mode in Gym", new Tags());
            parkToggle = mod.AddToList("Toggle In Parks", true, 0, "Toggles Dark Mode in Parks", new Tags());
            ringToggle = mod.AddToList("Toggle In Ring", true, 0, "Toggles Dark Mode in Ring", new Tags());
            pitToggle = mod.AddToList("Toggle In Pit", false, 0, "Toggles Dark Mode in Pit", new Tags());
            skyboxName = mod.AddToList("Skybox Name", "NightTime", "Name prefix for the skybox texture set. Stored in UserData/RumbleDarkMode/Skybox/", new Tags());
            skyboxTint = mod.AddToList("Skybox Tint", "404052", "The hex value that represents the skybox tint.\nDefault: 404052 (slate blue)", new Tags());
            skyboxExposure = mod.AddToList("Skybox Exposure", 0.5f, "The value that represents the exposure of the skybox.\nDefault: 0.5", new Tags());
            lightingColor = mod.AddToList("Lighting Color", "3d385c", "The hex value that represents what color the scene is lit as.", new Tags());
            treeColor = mod.AddToList("Tree Color", "#3e452d", "The hex value that represents what the tree/moss color is.", new Tags());
            treeShadowColor = mod.AddToList("Tree Shadow Color", "#1f2418", "The hex value that represents the shadow color of the trees/moss.", new Tags());
            playerColor = mod.AddToList("Player Color", "#30387c", "The hex value that represents the color of the players.\n<#F00>May cause lag. Leave textbox empty to disable.", new Tags());
            mod.GetFromFile();
            UI.instance.AddMod(mod);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            currentScene = sceneName;
            MelonCoroutines.Start(DelayedApply());
        }

        private IEnumerator DelayedApply()
        {
            yield return new WaitForSeconds(3f);
            ApplyDarkMode();
        }

        private Texture2D LoadSkyboxFace(string face)
        {
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, "RumbleDarkMode", "Skybox");

            string fileName = $"{(string)skyboxName.SavedValue}_{face}.png";
            string fullPath = Path.Combine(folder, fileName);

            if (!File.Exists(fullPath))
            {
                MelonLogger.Msg($"Skybox face missing: {fullPath}");
                return null;
            }

            byte[] data = File.ReadAllBytes(fullPath);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(data);
            return tex;
        }

        private void SetCustomSkybox()
        {
            Texture2D px = LoadSkyboxFace("Right");
            Texture2D nx = LoadSkyboxFace("Left");
            Texture2D py = LoadSkyboxFace("Top");
            Texture2D ny = LoadSkyboxFace("Bottom");
            Texture2D pz = LoadSkyboxFace("Front");
            Texture2D nz = LoadSkyboxFace("Back");

            if (px == null || nx == null || py == null || ny == null || pz == null || nz == null)
            {
                MelonLogger.Msg("One or more skybox faces missing.");
                return;
            }

            int size = px.width;
            var cubemap = new Cubemap(size, TextureFormat.RGBA32, false);

            Graphics.CopyTexture(px, 0, 0, cubemap, (int)CubemapFace.PositiveX, 0);
            Graphics.CopyTexture(nx, 0, 0, cubemap, (int)CubemapFace.NegativeX, 0);
            Graphics.CopyTexture(py, 0, 0, cubemap, (int)CubemapFace.PositiveY, 0);
            Graphics.CopyTexture(ny, 0, 0, cubemap, (int)CubemapFace.NegativeY, 0);
            Graphics.CopyTexture(pz, 0, 0, cubemap, (int)CubemapFace.PositiveZ, 0);
            Graphics.CopyTexture(nz, 0, 0, cubemap, (int)CubemapFace.NegativeZ, 0);

            var shader = Shader.Find("Skybox/Cubemap");

            if (shader == null)
            {
                MelonLogger.Msg("Skybox/Cubemap shader not found.");
                return;
            }

            var skyMat = new Material(shader);
            skyMat.SetTexture("_Tex", cubemap);
            skyMat.SetColor("_Tint", HexToColor((string)skyboxTint.SavedValue));
            skyMat.SetFloat("_Exposure", (float)skyboxExposure.SavedValue);

            RenderSettings.skybox = skyMat;
        }

        private void ApplyDarkMode()
        {
            if (currentScene == "Loader")
                return;

            if ((currentScene == "Gym" && !(bool)gymToggle.SavedValue) ||
                (currentScene == "Park" && !(bool)parkToggle.SavedValue) ||
                (currentScene == "Map0" && !(bool)ringToggle.SavedValue) ||
                (currentScene == "Map1" && !(bool)pitToggle.SavedValue))
                return;

            // Skybox
            SetCustomSkybox();

            // Lights
            foreach (var light in GameObject.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.color = HexToColor((string)lightingColor.SavedValue);
                    light.intensity = 0.5f;
                }

                light.cullingMask = -1;
            }

            // Renderers
            foreach (var renderer in GameObject.FindObjectsOfType<Renderer>())
            {
                var mat = renderer.sharedMaterial;
                if (mat == null) continue;

                if (mat.shader.name == "Shader Graphs/MobileEnvironmentUV0")
                {
                    var tex = mat.GetTexture("_TEXTURE");

                    var newMat = new Material(Shader.Find("Shader Graphs/RUMBLE_Move_Toon"));

                    newMat.SetTexture("Texture2D_2058E65A", tex);
                    newMat.SetTexture("Texture2D_3812B1EC", tex);
                    newMat.SetColor("Color_D943764B", Color.white);

                    renderer.material = newMat;
                }

                if (mat.shader.name == "Shader Graphs/Leaves")
                {
                    mat.SetColor("_Main_color", HexToColor((string)treeColor.SavedValue));
                    mat.SetColor("_Shadow_color", HexToColor((string)treeShadowColor.SavedValue));
                }

                renderer.lightmapIndex = -1;
                renderer.lightmapScaleOffset = Vector4.zero;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            // Players
            if ((string)playerColor.Value == "") return;
            foreach (var playerTexture in playerTextures)
            {
                if (playerTexture.Key == null || playerTexture.Value == null) continue;

                Texture2D tintedTexture = TintTexture(playerTexture.Value, HexToColor((string)playerColor.SavedValue));
                playerTexture.Key.material.SetTexture("_ColorAtlas", tintedTexture);

                if (currentScene == "Gym" && playerTexture.Key == PlayerManager.Instance.LocalPlayer.Controller.PlayerVisuals.renderer)
                {
                    Renderer mannequinRenderer = RumbleModdingAPI.RMAPI.GameObjects.Gym.INTERACTABLES.DressingRoom.PreviewPlayerController.Visuals.Renderer.GetGameObject().GetComponent<Renderer>();
                    mannequinRenderer.material.SetTexture("_ColorAtlas", tintedTexture);
                }
            }
        }

        private static Color HexToColor(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return Color.black;

            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);

            return new Color32(r, g, b, 255);
        }

        private static Texture2D TintTexture(Texture2D texture, Color tint)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(
                texture.width, texture.height, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D readableTex = new Texture2D(texture.width, texture.height);
            readableTex.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            Color[] pixels = readableTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r *= tint.r;
                pixels[i].g *= tint.g;
                pixels[i].b *= tint.b;
            }
            readableTex.SetPixels(pixels);
            readableTex.Apply();

            return readableTex;
        }

        [HarmonyPatch(typeof(PlayerVisuals), nameof(PlayerVisuals.ApplyPlayerVisuals), new Type[] { typeof(GeneratedPlayerVisuals) })]
        public static class PlayerVisuals_ApplyPlayerVisuals_Patch
        {
            private static void Postfix(ref PlayerVisuals __instance, ref GeneratedPlayerVisuals generatedVisuals)
            {
                Renderer r = __instance.renderer;
                Texture2D atlas = generatedVisuals.GeneratedTexture;
                atlas.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                Main.playerTextures[r] = atlas;
            }
        }
    }
}