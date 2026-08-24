using System.Reflection;
using FezEngine.Tools;
using FezGame;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

/*
 * In the end credits, the game will always pause if the window loses focus, even if Pause On Lost Focus is disabled.
 * This patch fixes that and lets the credits respect the chose Pause On Lost Focus setting.
 */
namespace FezStitcher.Patches
{
    public class CreditsPauseFix : IFezStitch
    {
        ILHook CreditsMenuLevelUpdateHook;

        public void Init()
        {
            Type CreditsMenuLevel = typeof(Fez).Assembly.GetType("FezGame.Structure.CreditsMenuLevel");

            CreditsMenuLevelUpdateHook = new ILHook(CreditsMenuLevel.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance), (il) => {
                ILCursor cursor = new(il);

                cursor.GotoNext(MoveType.After, i => i.MatchCallvirt("Microsoft.Xna.Framework.Game", "get_IsActive"));
                cursor.EmitDelegate(FudgeIsActive);
            });
        }

        private bool FudgeIsActive(bool isActive)
        {
            if (!SettingsManager.Settings.PauseOnLostFocus)
                return true;
            return isActive;
        }

        public void Dispose()
        {
            CreditsMenuLevelUpdateHook.Dispose();
        }
    }
}
