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
        FieldInfo EndCutscene64HostNoDestroy;
        Hook EndCutscene64HostTryDestroyHook;

        FieldInfo PixelizerLowResRT;
        Hook PixelizerDisposeHook;
        FieldInfo FezGridTetraMesh;
        Hook FezGridUpdateHook;
        FieldInfo FractalOuterShellMesh;
        Hook FractalUpdateHook;
        FieldInfo AxisDnaFatAxisMesh;
        Hook AxisDnaUpdateHook;
        FieldInfo TetraordialOozeTetraMesh;
        Hook TetraordialOozeUpdateHook;
        FieldInfo VibratingMembraneLinesMesh;
        Hook VibratingMembraneUpdateHook;
        FieldInfo DrumSoloStarMesh;
        Hook DrumSoloUpdateHook;

        FieldInfo MulticoloredSpaceCubesMesh;
        Hook MulticoloredSpaceUpdateHook;
        FieldInfo DotsAplentyCloneMesh;
        Hook DotsAplentyUpdateHook;

        ILHook PixelizerDrawSetSoundVolumeHook;
        ILHook ZoomOutUpdateSetSoundVolumeHook;

#if DEBUG
        Hook EndCutscene32HostCycleHook;
        Hook EndCutscene64HostCycleHook;
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
            Type EndCutscene64Host = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene64Host");

            Type Pixelizer = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.Pixelizer");
            Type FezGrid = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.FezGrid");
            Type Fractal = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.Fractal");
            Type AxisDna = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.AxisDna");
            Type TetraordialOoze = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.TetraordialOoze");
            Type VibratingMembrane = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.VibratingMembrane");
            Type DrumSolo = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene32.DrumSolo");

            Type ZoomOut = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene64.ZoomOut");
            Type MulticoloredSpace = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene64.MulticoloredSpace");
            Type DotsAplenty = typeof(Fez).Assembly.GetType("FezGame.Components.EndCutscene64.DotsAplenty");

            EndCutscene32HostNoDestroy = EndCutscene32Host.GetField("noDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
            EndCutscene32HostTryDestroyHook = new Hook(EndCutscene32Host.GetMethod("TryDestroy", BindingFlags.NonPublic | BindingFlags.Instance), EndCutscene32HostTryDestroyHooked);
            EndCutscene64HostNoDestroy = EndCutscene64Host.GetField("noDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
            EndCutscene64HostTryDestroyHook = new Hook(EndCutscene64Host.GetMethod("TryDestroy", BindingFlags.NonPublic | BindingFlags.Instance), EndCutscene64HostTryDestroyHooked);

            PixelizerLowResRT = Pixelizer.GetField("LowResRT", BindingFlags.NonPublic | BindingFlags.Instance);
            PixelizerDisposeHook = new Hook(Pixelizer.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance), PixelizerDisposeHooked);
            FezGridTetraMesh = FezGrid.GetField("TetraMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            FezGridUpdateHook = new Hook(FezGrid.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), FezGridUpdateHooked);
            FractalOuterShellMesh = Fractal.GetField("OuterShellMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            FractalUpdateHook = new Hook(Fractal.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), FractalUpdateHooked);
            AxisDnaFatAxisMesh = AxisDna.GetField("FatAxisMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            AxisDnaUpdateHook = new Hook(AxisDna.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), AxisDnaUpdateHooked);
            TetraordialOozeTetraMesh = TetraordialOoze.GetField("TetraMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            TetraordialOozeUpdateHook = new Hook(TetraordialOoze.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), TetraordialOozeUpdateHooked);
            VibratingMembraneLinesMesh = VibratingMembrane.GetField("LinesMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            VibratingMembraneUpdateHook = new Hook(VibratingMembrane.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), VibratingMembraneUpdateHooked);
            DrumSoloStarMesh = DrumSolo.GetField("StarMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            DrumSoloUpdateHook = new Hook(DrumSolo.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), DrumSoloUpdateHooked);

            MulticoloredSpaceCubesMesh = MulticoloredSpace.GetField("CubesMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            MulticoloredSpaceUpdateHook = new Hook(MulticoloredSpace.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), MulticoloredSpaceUpdateHooked);
            DotsAplentyCloneMesh = DotsAplenty.GetField("CloneMesh", BindingFlags.NonPublic | BindingFlags.Instance);
            DotsAplentyUpdateHook = new Hook(DotsAplenty.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), DotsAplentyUpdateHooked);

            PixelizerDrawSetSoundVolumeHook = new ILHook(Pixelizer.GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance), GenerateILHookToMultiplyVolume);
            ZoomOutUpdateSetSoundVolumeHook = new ILHook(ZoomOut.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), GenerateILHookToMultiplyVolume);

#if DEBUG
            EndCutscene32HostCycleHook = new Hook(EndCutscene32Host.GetMethod("Cycle", BindingFlags.Public | BindingFlags.Instance), (Action<DrawableGameComponent> original, DrawableGameComponent self) => {
                FezStitcher.Log("EndCutscene32Host.Cycle called");
                original(self);
            });
            EndCutscene64HostCycleHook = new Hook(EndCutscene64Host.GetMethod("Cycle", BindingFlags.Public | BindingFlags.Instance), (Action<DrawableGameComponent> original, DrawableGameComponent self) => {
                FezStitcher.Log("EndCutscene64Host.Cycle called");
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

        private void EndCutscene64HostTryDestroyHooked(Action<DrawableGameComponent> original, DrawableGameComponent self)
        {
            bool noDestroy = (bool)EndCutscene64HostNoDestroy.GetValue(self);
            if (!noDestroy)
            {
                FezStitcher.Log("EndCutscene64Host is being destroyed");
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

        private void AxisDnaUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh FatAxisMesh = (Mesh)AxisDnaFatAxisMesh.GetValue(self);
            if (FatAxisMesh == null)
            {
                FezStitcher.Log("AxisDna.Update - FatAxisMesh is null, bailing");
                return;
            }
            original(self, gameTime);
        }

        private void TetraordialOozeUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh TetraMesh = (Mesh)TetraordialOozeTetraMesh.GetValue(self);
            if (TetraMesh == null)
            {
                FezStitcher.Log("TetraordialOoze.Update - TetraMesh is null, bailing");
                return;
            }
            original(self, gameTime);
        }

        private void VibratingMembraneUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh LinesMesh = (Mesh)VibratingMembraneLinesMesh.GetValue(self);
            if (LinesMesh == null)
            {
                FezStitcher.Log("VibratingMembrane.Update - LinesMesh is null, bailing");
                return;
            }
            original(self, gameTime);
        }

        private void DrumSoloUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh StarMesh = (Mesh)DrumSoloStarMesh.GetValue(self);
            if (StarMesh == null)
            {
                FezStitcher.Log("DrumSolo.Update - StarMesh is null, bailing");
                return;
            }
            original(self, gameTime);
        }

        private void MulticoloredSpaceUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh CubesMesh = (Mesh)MulticoloredSpaceCubesMesh.GetValue(self);
            if (CubesMesh == null)
            {
                FezStitcher.Log("MulticoloredSpace.Update - CubesMesh is null, bailing");
                return;
            }
            original(self, gameTime);
        }

        private void DotsAplentyUpdateHooked(Action<DrawableGameComponent, GameTime> original, DrawableGameComponent self, GameTime gameTime)
        {
            Mesh CloneMesh = (Mesh)DotsAplentyCloneMesh.GetValue(self);
            if (CloneMesh == null)
            {
                FezStitcher.Log("DotsAplenty.Update - CloneMesh is null, bailing");
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
            AxisDnaUpdateHook.Dispose();
            TetraordialOozeUpdateHook.Dispose();
            VibratingMembraneUpdateHook.Dispose();
            DrumSoloUpdateHook.Dispose();
            MulticoloredSpaceUpdateHook.Dispose();
            DotsAplentyUpdateHook.Dispose();
#if DEBUG
            EndCutscene32HostCycleHook.Dispose();
            EndCutscene64HostCycleHook.Dispose();
#endif // DEBUG
        }
    }
}
