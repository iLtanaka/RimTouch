using System;
using System.Collections.ObjectModel;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimTouch
{
    internal static class SimpleCameraSettingCompatibility
    {
        private const string PackageId = "ray1203.SimpleCameraSetting";
        private const string HarmonyId = "ray1203.SimpleCameraSetting";

        private static bool loadedChecked;
        private static bool loaded;
        private static bool compatibilityFinished;
        private static int lastCompatibilityCheckFrame = -1000;

        public static bool IsLoaded
        {
            get
            {
                if (!loadedChecked)
                {
                    loaded = IsPackageLoaded();
                    loadedChecked = true;
                }
                return loaded;
            }
        }

        public static void Apply(Harmony harmony)
        {
            Apply(harmony, false);
        }

        public static void Apply(Harmony harmony, bool force)
        {
            if (compatibilityFinished || harmony == null)
            {
                return;
            }

            if (!force && Time.frameCount - lastCompatibilityCheckFrame < 120)
            {
                return;
            }
            lastCompatibilityCheckFrame = Time.frameCount;

            if (!IsLoaded)
            {
                compatibilityFinished = true;
                return;
            }

            MethodInfo original = AccessTools.Method(typeof(CameraMapConfig), "ConfigFixedUpdate_60");
            if (original == null)
            {
                compatibilityFinished = true;
                return;
            }

            Patches patchInfo = Harmony.GetPatchInfo(original);
            if (patchInfo == null)
            {
                return;
            }

            if (!HasOwner(patchInfo.Prefixes, HarmonyId))
            {
                compatibilityFinished = true;
                return;
            }

            harmony.Unpatch(original, HarmonyPatchType.Prefix, HarmonyId);
            compatibilityFinished = true;
            Log.Message("[RimTouch] SimpleCameraSetting compatibility enabled: disabled its CameraMapConfig.ConfigFixedUpdate_60 prefix while leaving zoom settings active.");
        }

        private static bool HasOwner(ReadOnlyCollection<Patch> patches, string owner)
        {
            if (patches == null)
            {
                return false;
            }

            for (int i = 0; i < patches.Count; i++)
            {
                Patch patch = patches[i];
                if (patch != null && patch.owner == owner)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPackageLoaded()
        {
            if (LoadedModManager.RunningModsListForReading == null)
            {
                return false;
            }

            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod != null && string.Equals(mod.PackageId, PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
