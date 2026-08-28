using System.Reflection;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;

/*
 * There are a few buggy behaviors with secret passages. If you're looking at a secret passage door, then rotate and
 * quickly enter another door (see NUZU_ABANDONED_B), the game will open the secret door while you go through the normal
 * door at the same time. Depending on various factors involving timing and if there are any unlocked secret passages in
 * the next room, Gomez might just freeze for a few seconds, but worst case the game can fully crash.
 *
 * To prevent getting in the situation in the first place, this patch adds a check to make sure we're not already going
 * through a door before it opens the secret passage door.
 *
 * Even with that, it's still possible to crash if we quickly enter a door while the rumble sound effect from a secret
 * passage is still playing. The offending crashing function is inside a delegate function which means it has a compiler
 * generated name which can vary between different compilers. We have to do some jank to find it - it's possible that
 * there are situations where this doesn't work, but I believe the constraints on the name and function signature should
 * be enough that we always find it. Simply patch that delegate function to have a null check.
 */
namespace FezStitcher.Patches
{
    public class SecretPassagesFixes : IFezStitch
    {
        Type SecretPassagesHost;
        FieldInfo SecretPassagesERumble;
        Hook SecretPassagesOpenHook;
        Hook SecretPassagesUpdateDelegateHook;

        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        public void Init()
        {
            SecretPassagesHost = typeof(Fez).Assembly.GetType("FezGame.Components.SecretPassagesHost");
            SecretPassagesERumble = SecretPassagesHost.GetField("eRumble", BindingFlags.NonPublic | BindingFlags.Instance);

            SecretPassagesOpenHook = new Hook(SecretPassagesHost.GetMethod("Open", BindingFlags.NonPublic | BindingFlags.Instance), SecretPassagesOpenHooked);

            MethodInfo Delegate = FindSecretPassagesHostUpdateDelegate();
            if (Delegate != null)
                SecretPassagesUpdateDelegateHook = new Hook(Delegate, SecretPassagesUpdateDelegateHooked);
        }

        private MethodInfo FindSecretPassagesHostUpdateDelegate()
        {
            List<MethodInfo> candidates = [];
            foreach (MethodInfo method in SecretPassagesHost.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name.Contains("<Update>"))
                {
                    if (method.ReturnParameter.ParameterType == typeof(void)
                            && method.GetParameters().Length == 0)
                    {
                        candidates.Add(method);
                    }
                }
            }
            if (candidates.Count() == 0)
            {
                FezStitcher.LogError("WARNING: Couldn't find SecretPassagesHost.Update delegate method to patch... Secret passage door crashes are still possible! Please report this!");
                return null;
            }
            if (candidates.Count() > 1)
                FezStitcher.LogError("WARNING: MORE THAN ONE SecretPassagesHost.Update delegate method matched... Patching one but it could be wrong! Please report this!");
            return candidates[0];
        }

        private void SecretPassagesOpenHooked(Action<GameComponent> original, GameComponent self)
        {
#if DEBUG
            FezStitcher.Log("SecretPassagesHost.Open Action: " + PlayerManager.Action);
#endif // DEBUG
            if (PlayerManager.Action.IsEnteringDoor())
            {
                FezStitcher.Log("SecretPassagesHost.Open - tried to open while in " + PlayerManager.Action + ", bailing");
                return;
            }
            original(self);
        }

        private void SecretPassagesUpdateDelegateHooked(Action<GameComponent> original, GameComponent self)
        {
            SoundEmitter eRumble = (SoundEmitter)SecretPassagesERumble.GetValue(self);
            if (eRumble == null)
            {
                FezStitcher.Log("SecretPassagesHost.Update delegate - eRumble is null, bailing");
                return;
            }
            original(self);
        }

        public void Dispose()
        {
            SecretPassagesOpenHook.Dispose();
            SecretPassagesUpdateDelegateHook?.Dispose();
        }
    }
}
