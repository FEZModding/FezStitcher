using System.Reflection;
using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
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
        FieldInfo EndCutscene32HostNoDestroy;
        Hook EndCutscene32HostTryDestroyHook;

        FieldInfo PixelizerLowResRT;
        Hook PixelizerDisposeHook;
        FieldInfo FezGridTetraMesh;
        Hook FezGridUpdateHook;
        FieldInfo FractalOuterShellMesh;
        Hook FractalUpdateHook;

        ILHook PixelizerDrawSetSoundVolumeHook;
        ILHook ZoomOutUpdateSetSoundVolumeHook;

#if DEBUG
        Hook EndCutscene32HostCycleHook;
#endif // DEBUG

        [ServiceDependency]
        public ISoundManager SoundManager { private get; set; }

        [ServiceDependency]
        public ITargetRenderingManager TargetRenderer { private get; set; }

        [ServiceDependency]
        public ILevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameStateManager GameState { private get; set; }

        public void Init()
        {
            Type EndCutscene32Host = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32Host");

            Type Pixelizer = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.Pixelizer");
            Type FezGrid = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.FezGrid");
            Type Fractal = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.Fractal");

            Type ZoomOut = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene64.ZoomOut");

            EndCutscene32HostNoDestroy = EndCutscene32Host.GetField("noDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
            EndCutscene32HostTryDestroyHook = new Hook(EndCutscene32Host.GetMethod("TryDestroy", BindingFlags.NonPublic | BindingFlags.Instance), EndCutscene32HostTryDestroyHooked);

            PixelizerLowResRT = Pixelizer.GetField("LowResRT", BindingFlags.NonPublic | BindingFlags.Instance);
            PixelizerDisposeHook = new Hook(Pixelizer.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance), PixelizerDisposeHooked);
            FezGridTetraMesh = FezGrid.GetField("TetraMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            FezGridUpdateHook = new Hook(FezGrid.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), FezGridUpdateHooked);
            FractalOuterShellMesh = Fractal.GetField("OuterShellMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            FractalUpdateHook = new Hook(Fractal.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), FractalUpdateHooked);

            PixelizerDrawSetSoundVolumeHook = new ILHook(Pixelizer.GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance), GenerateILHookToMultiplyVolume);
            ZoomOutUpdateSetSoundVolumeHook = new ILHook(ZoomOut.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), GenerateILHookToMultiplyVolume);

#if DEBUG
            EndCutscene32HostCycleHook = new Hook(EndCutscene32Host.GetMethod("Cycle", BindingFlags.Public | BindingFlags.Instance), (Action<DrawableGameComponent> original, DrawableGameComponent self) => {
                FezStitcher.Log("EndCutscene32Host.Cycle called");
                original(self);
            });
#endif // DEBUG
        }

        private void EndCutscene32HostTryDestroyHooked(Action<DrawableGameComponent> original, DrawableGameComponent self)
        {
            bool noDestroy = (bool)EndCutscene32HostNoDestroy.GetValue(self);
            if (LevelManager.Name != "DRUM" && !noDestroy)
            {
                FezStitcher.Log("EndCutscene32Host is being destroyed");
                GameState.SkyOpacity = 1f; // Reset this value since otherwise it might remain at 0
            }
            original(self);
        }

        private void PixelizerDisposeHooked(Action<DrawableGameComponent, bool> original, DrawableGameComponent self, bool disposing)
        {
            RenderTarget2D LowResRT = (RenderTarget2D)PixelizerLowResRT.GetValue(self);
            if (LowResRT != null)
                TargetRenderer.UnscheduleHook(LowResRT);

            SoundManager.SoundEffectVolume = SettingsManager.Settings.SoundVolume;

            original(self, disposing);
        }

        private void FezGridUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh TetraMesh = (Mesh)FezGridTetraMesh.GetValue(self);
            if (TetraMesh == null)
            {
                FezStitcher.Log("FezGrid.Update - TetraMesh is null, bailing");
                return;
            }
            original(self, gameTime);
        }

        private void FractalUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh OuterShellMesh = (Mesh)FractalOuterShellMesh.GetValue(self);
            if (OuterShellMesh == null)
            {
                FezStitcher.Log("Fractal.Update - OuterShellMesh is null, bailing");
                return;
            }
            original(self, gameTime);
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
            EndCutscene32HostTryDestroyHook.Dispose();
            PixelizerDisposeHook.Dispose();
            FezGridUpdateHook.Dispose();
            FractalUpdateHook.Dispose();
            PixelizerDrawSetSoundVolumeHook.Dispose();
            ZoomOutUpdateSetSoundVolumeHook.Dispose();
#if DEBUG
            EndCutscene32HostCycleHook.Dispose();
#endif // DEBUG
        }
    }
}
