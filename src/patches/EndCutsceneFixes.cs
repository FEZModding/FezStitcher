using System.Reflection;
using FezEngine.Services;
using FezEngine.Tools;
using FezGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

/*
 * These patches fix a few issues regarding the 32 and 64 cube ending cutscenes.
 *
 * If you do anything to end the 32 cutscene early (load a different or new save file, restart the run in speedrun mode)
 * while the pixelization effect is active, the game will crash due to Render Target shenanigans. Fix this by clearing
 * that out properly during disposal.
 *
 * In both cutscenes, the sound effects fade out but the initial starting volume does not respect your chosen sfx volume
 * and will always start at full volume. Fix this by using an ILHook to multiply the sound volume with the setting.
 */
namespace FezStitcher.Patches
{
    public class EndCutsceneFixes : IFezStitch
    {
        FieldInfo PixelizerLowResRT;

        Hook PixelizerDisposeHook;
        ILHook PixelizerDrawSetSoundVolumeHook;

        ILHook ZoomOutUpdateSetSoundVolumeHook;

        [ServiceDependency]
        public ISoundManager SoundManager { private get; set; }

        [ServiceDependency]
        public ITargetRenderingManager TargetRenderer { private get; set; }

        public void Init()
        {
            Type Pixelizer = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.Pixelizer");
            Type ZoomOut = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene64.ZoomOut");

            PixelizerLowResRT = Pixelizer.GetField("LowResRT", BindingFlags.NonPublic | BindingFlags.Instance);

            PixelizerDisposeHook = new Hook(Pixelizer.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance), PixelizerDisposeHooked);
            PixelizerDrawSetSoundVolumeHook = new ILHook(Pixelizer.GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance), GenerateILHookToMultiplyVolume);
            ZoomOutUpdateSetSoundVolumeHook = new ILHook(ZoomOut.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), GenerateILHookToMultiplyVolume);
        }

        private void PixelizerDisposeHooked(Action<DrawableGameComponent, bool> original, DrawableGameComponent self, bool disposing)
        {
            RenderTarget2D LowResRT = (RenderTarget2D)PixelizerLowResRT.GetValue(self);
            if (LowResRT != null)
                TargetRenderer.UnscheduleHook(LowResRT);

            SoundManager.SoundEffectVolume = SettingsManager.Settings.SoundVolume;

            original(self, disposing);
        }

        private void GenerateILHookToMultiplyVolume(ILContext il)
        {
            ILCursor cursor = new(il);

            cursor.GotoNext(MoveType.Before, i => i.MatchCallvirt("FezEngine.Services.ISoundManager", "set_SoundEffectVolume")); // SoundManager.SoundEffectVolume = ...; (after it does the calculation but right before it gets set)
            cursor.EmitDelegate(MultiplyVolume); // Pass that value into our delegate which will multiply it by the sound volume setting to respect it
        }

        private float MultiplyVolume(float volume)
        {
            return volume * SettingsManager.Settings.SoundVolume;
        }

        public void Dispose()
        {
            PixelizerDisposeHook.Dispose();
            PixelizerDrawSetSoundVolumeHook.Dispose();
            ZoomOutUpdateSetSoundVolumeHook.Dispose();
        }
    }
}
