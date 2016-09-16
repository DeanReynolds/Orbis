using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orbis
{
    public class BufferedString
    {
        public string Text;
        public float Offset;
        public Rectangle Rectangle;
        public const double StartingLife = 5;
        public double Life = StartingLife;
        public const float Scale = .15f, PlayerYOffset = 20;

        public BufferedString(string text) { Text = text; }

        public void CalculateRectangle(SpriteFont font, Vector2 position)
        {
            var width = (int) Math.Ceiling(font.MeasureString(Text).X * Scale);
            var height = (int) Math.Ceiling(font.MeasureString(Text).Y * Scale);
            Rectangle = new Rectangle((int) Math.Floor(position.X - (width/2f)), (int) Math.Floor((position.Y - (height/2f)) + Offset), width, height);
        }
    }
}