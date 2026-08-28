using System.Reflection;
using FezEngine.Tools;
using Microsoft.Xna.Framework;

namespace FezStitcher
{
    public interface IFezStitch
    {
        public void Init();
        public void Dispose();
    }

	public class FezStitcher(Game game) : GameComponent(game)
	{
        private readonly List<IFezStitch> Patches = [];

        public static void Log(object message)
        {
            Console.WriteLine("[FezStitcher] " + message);
        }

        public static void LogError(object message)
        {
            Console.Error.WriteLine("[FezStitcher]" + message);
        }

        public override void Initialize()
        {
            base.Initialize();

            Log("Patching...");

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()
                    .Where(t => t.IsClass && typeof(IFezStitch).IsAssignableFrom(t)))
            {
                IFezStitch patch = (IFezStitch)Activator.CreateInstance(type);
                ServiceHelper.InjectServices(patch);
                patch.Init();
                Patches.Add(patch);
            }

            Log("Finished patching!");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            foreach (IFezStitch patch in Patches)
            {
                patch.Dispose();
            }
            Patches.Clear();
        }
	}
}
