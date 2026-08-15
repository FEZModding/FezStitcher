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
        private List<IFezStitch> Patches = [];

        public override void Initialize()
        {
            base.Initialize();

            Console.WriteLine("FezStitcher patching...");

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()
                    .Where(t => t.IsClass && typeof(IFezStitch).IsAssignableFrom(t)))
            {
                IFezStitch patch = (IFezStitch)Activator.CreateInstance(type);
                ServiceHelper.InjectServices(patch);
                patch.Init();
                Patches.Add(patch);
            }

            Console.WriteLine("FezStitcher finished patching!");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            foreach (IFezStitch patch in Patches)
            {
                patch.Dispose();
            }
            Patches = [];
        }
	}
}
