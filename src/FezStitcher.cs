using Microsoft.Xna.Framework;

namespace FezStitcher
{
	public class FezStitcher(Game game) : GameComponent(game)
	{
        public override void Initialize()
        {
            base.Initialize();

			Console.WriteLine("FezStitcher loaded!");
        }
	}
}
