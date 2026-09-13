using System.Reflection;
using FezGame;
using MonoMod.RuntimeDetour;

/*
 * Fixes "bell glitch" that can be encountered by speedrunners. When entering the room, clear out the stacked hits so
 * there are no latent bell hits that can reemerge after reloading a save where the bell is back
 */
namespace FezStitcher.Patches
{
    public class BellGlitchFix : IFezStitch
    {
        Hook TryInitializeHook;

        FieldInfo StackedHitsField;

        public void Init()
        {
            Type BellHost = typeof(Fez).Assembly.GetType("FezGame.Components.BellHost");

            MethodInfo TryInitialize = BellHost.GetMethod("TryInitialize", BindingFlags.NonPublic | BindingFlags.Instance);
            TryInitializeHook = new Hook(TryInitialize, TryInitializeHooked);

            StackedHitsField = BellHost.GetField("stackedHits", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void TryInitializeHooked(Action<object> original, object self)
        {
            StackedHitsField.SetValue(self, 0);
            original(self);
        }

        public void Dispose()
        {
            TryInitializeHook?.Dispose();
        }
    }
}
