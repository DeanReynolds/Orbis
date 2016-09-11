using System;
using Microsoft.Xna.Framework;

namespace Orbis
{
    public class Entity
    {
        private Vector2 linearPosition;
        public Vector2 LinearPosition { get { return linearPosition; } set { linearPosition = value; WorldPosition = new Vector2((int)Math.Round(value.X), (int)Math.Round(value.Y)); } }
        public Vector2 WorldPosition;

        public float LinearX { get { return LinearPosition.X; } set { linearPosition.X = value; WorldPosition.X = (int)Math.Round(value); } }
        public float LinearY { get { return LinearPosition.Y; } set { linearPosition.Y = value; WorldPosition.Y = (int)Math.Round(value); } }
    }
}