using UnityEngine;
using Verse;

namespace RimTouch
{
    public sealed class RimTouchMod : Mod
    {
        public static RimTouchSettings Settings;

        public RimTouchMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimTouchSettings>();
        }

        public override string SettingsCategory()
        {
            return "RimTouch";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Enable touch mode", ref Settings.enableTouchMode, "Enables RimTouch direct touch gestures and UI tap repair.");
            listing.GapLine();

            listing.Label("Pan inertia strength: " + Settings.panInertiaStrength.ToString("0.00"));
            Settings.panInertiaStrength = listing.Slider(Settings.panInertiaStrength, 0f, 2f);

            listing.Label("Pan inertia damping: " + Settings.panInertiaDamping.ToString("0.00"));
            Settings.panInertiaDamping = listing.Slider(Settings.panInertiaDamping, 1f, 10f);

            listing.Label("Zoom inertia strength: " + Settings.zoomInertiaStrength.ToString("0.00"));
            Settings.zoomInertiaStrength = listing.Slider(Settings.zoomInertiaStrength, 0f, 1f);

            listing.Label("Zoom inertia damping: " + Settings.zoomInertiaDamping.ToString("0.00"));
            Settings.zoomInertiaDamping = listing.Slider(Settings.zoomInertiaDamping, 1f, 14f);

            listing.Gap();
            if (listing.ButtonText("Reset touch tuning"))
            {
                Settings.ResetTouchTuning();
            }

            listing.End();
            Settings.Write();
        }
    }

    public sealed class RimTouchSettings : ModSettings
    {
        public bool enableTouchMode = true;
        public float panInertiaStrength = 1.05f;
        public float panInertiaDamping = 4.8f;
        public float zoomInertiaStrength = 0.22f;
        public float zoomInertiaDamping = 7.5f;

        public void ResetTouchTuning()
        {
            panInertiaStrength = 1.05f;
            panInertiaDamping = 4.8f;
            zoomInertiaStrength = 0.22f;
            zoomInertiaDamping = 7.5f;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableTouchMode, "enableTouchMode", true);
            Scribe_Values.Look(ref panInertiaStrength, "panInertiaStrength", 1.05f);
            Scribe_Values.Look(ref panInertiaDamping, "panInertiaDamping", 4.8f);
            Scribe_Values.Look(ref zoomInertiaStrength, "zoomInertiaStrength", 0.22f);
            Scribe_Values.Look(ref zoomInertiaDamping, "zoomInertiaDamping", 7.5f);
        }
    }
}
