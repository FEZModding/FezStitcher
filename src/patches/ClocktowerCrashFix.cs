using System.Reflection;
using FezGame;
using MonoMod.RuntimeDetour;

/*
 * Prevents the game from crashing when entering the Clock Tower after leaving the room when a cube was spawned
 */
namespace FezStitcher.Patches
{
    public class ClocktowerCrashFix : IFezStitch
    {
        Hook TryInitializeHook;

        FieldInfo RedSecretField;
        FieldInfo BlueSecretField;
        FieldInfo GreenSecretField;
        FieldInfo WhiteSecretField;

        public void Init()
        {
            Type ClockTowerHost = typeof(Fez).Assembly.GetType("FezGame.Components.ClockTowerHost");

            MethodInfo TryInitialize = ClockTowerHost.GetMethod("TryInitialize", BindingFlags.NonPublic | BindingFlags.Instance);
            TryInitializeHook = new Hook(TryInitialize, TryInitializeHooked);

            RedSecretField = ClockTowerHost.GetField("RedSecret", BindingFlags.NonPublic | BindingFlags.Instance);
            BlueSecretField = ClockTowerHost.GetField("BlueSecret", BindingFlags.NonPublic | BindingFlags.Instance);
            GreenSecretField = ClockTowerHost.GetField("GreenSecret", BindingFlags.NonPublic | BindingFlags.Instance);
            WhiteSecretField = ClockTowerHost.GetField("WhiteSecret", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void TryInitializeHooked(Action<object> original, object self)
        {
            RedSecretField.SetValue(self, null);
            BlueSecretField.SetValue(self, null);
            GreenSecretField.SetValue(self, null);
            WhiteSecretField.SetValue(self, null);
            original(self);
        }

        public void Dispose()
        {
            TryInitializeHook?.Dispose();
        }
    }
}
