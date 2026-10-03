using System.Reflection;
using FezEngine.Structure;
using FezGame;
using MonoMod.RuntimeDetour;

/*
 * Fixes a crash when loading a new room while an animation relating to Lesser Warp gates is playing
 */
namespace FezStitcher.Patches
{
    public class LesserWarpCrashFix : IFezStitch
    {
        Type LesserWarp;
        Hook LesserWarpActDelegateHook;

        FieldInfo LesserWarpEIdleSpin;

        public void Init()
        {
            LesserWarp = typeof(Fez).Assembly.GetType("FezGame.Components.Actions.LesserWarp");
            MethodInfo LesserWarpActDelegate = FindLesserWarpActDelegate();

            if (LesserWarpActDelegate != null)
                LesserWarpActDelegateHook = new Hook(LesserWarpActDelegate, LesserWarpActDelegateHooked);

            LesserWarpEIdleSpin = LesserWarp.GetField("eIdleSpin", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private MethodInfo FindLesserWarpActDelegate()
        {
            // We need to patch a delegate function inside the LesserGate.Act method. It is the only such delegate
            // method which takes in exactly one float meaning we can scan for it reliably.
            List<MethodInfo> candidates = [];
            foreach (MethodInfo method in LesserWarp.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name.Contains("<Act>"))
                {
                    if (method.ReturnParameter.ParameterType == typeof(void)
                            && method.GetParameters().Length == 1
                            && method.GetParameters()[0].ParameterType == typeof(float))
                    {
                        candidates.Add(method);
                    }
                }
            }
            if (candidates.Count() == 0)
            {
                FezStitcher.LogError("WARNING: Couldn't find LesserWarp.Act delegate method to patch... Please report this!");
                return null;
            }
            if (candidates.Count() > 1)
                FezStitcher.LogError("WARNING: MORE THAN ONE LesserWarp.Act delegate method matched... Patching one but it could be wrong! Please report this!");
            return candidates[0];
        }

        private void LesserWarpActDelegateHooked(Action<object, float> original, object self, float s)
        {
            SoundEmitter eIdleSpin = (SoundEmitter)LesserWarpEIdleSpin.GetValue(self);
            if (eIdleSpin == null)
            {
                FezStitcher.Log("LesserWarp.Act (delegate) - eIdleSpin is null, bailing");
                return;
            }
            original(self, s);
        }

        public void Dispose()
        {
            LesserWarpActDelegateHook?.Dispose();
        }
    }
}
