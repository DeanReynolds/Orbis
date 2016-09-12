using System;
using Microsoft.Xna.Framework;
using Orbis.World;
using SharpXNA.Collision;

namespace Orbis
{
    public class Entity
    {
        private static Tile[,] Tiles => Game.Tiles;

        private Vector2 _linearPosition;
        public Vector2 LinearPosition
        {
            get { return _linearPosition; }
            set
            {
                _linearPosition = value;
                _worldPosition = new Vector2((int) Math.Round(value.X), (int) Math.Round(value.Y));
                Hitbox.Position = WorldPosition;
            }
        }

        private Vector2 _worldPosition;
        public Vector2 WorldPosition => _worldPosition;

        public float LinearX
        {
            get { return LinearPosition.X; }
            set
            {
                _linearPosition.X = value;
                _worldPosition.X = (int) Math.Round(value);
                Hitbox.X = WorldPosition.X;
            }
        }
        public float LinearY
        {
            get { return LinearPosition.Y; }
            set
            {
                _linearPosition.Y = value;
                _worldPosition.Y = (int) Math.Round(value);
                Hitbox.Y = WorldPosition.Y;
            }
        }

        private readonly CollosionOptions _collisionOptions;
        [Flags] public enum CollosionOptions { None = 0, CollidesWithTerrain = 1 }
        public Entity(CollosionOptions collisionOptions) { _collisionOptions = collisionOptions; }

        public bool IsFalling, IsOnGround;
        public Vector2 Velocity;
        public Polygon Hitbox;

        public void Update(GameTime time)
        {
            if (Velocity.Y > 0) IsFalling = true;
            Move(Velocity*(float) time.ElapsedGameTime.TotalSeconds);
        }

        public bool CollidesWithTerrain
        {
            get
            {
                for (var x = (int)Math.Floor((LinearPosition.X / Tile.Size) - 1); x <= (int)Math.Ceiling((LinearPosition.X / Tile.Size) + 1); x++)
                    for (var y = (int)Math.Floor((LinearPosition.Y / Tile.Size) - 1); y <= (int)Math.Ceiling((LinearPosition.Y / Tile.Size) + 1); y++)
                        if (Game.InBounds(x, y) && Tiles[x, y].Solid)
                        {
                            var hitbox = Polygon.CreateSquare(Tile.Size);
                            hitbox.Position = new Vector2(((x * Tile.Size) + (Tile.Size / 2f)), ((y * Tile.Size) + (Tile.Size / 2f)));
                            if (Hitbox.Intersects(hitbox)) return true;
                        }
                return false;
            }
        }
        public void Move(Vector2 offset)
        {
            const float specific = 1;
            if (offset.X != 0)
            {
                LinearX += offset.X;
                Hitbox.Position = WorldPosition;
                if (_collisionOptions.HasFlag(CollosionOptions.CollidesWithTerrain) && CollidesWithTerrain)
                {
                    LinearX -= offset.X;
                    Hitbox.Position = WorldPosition;
                    Velocity.X = 0;
                    while (!CollidesWithTerrain)
                    {
                        LinearX += offset.X < 0 ? -specific : specific;
                        Hitbox.Position = WorldPosition;
                    }
                    while (CollidesWithTerrain)
                    {
                        LinearX -= offset.X < 0 ? -specific : specific;
                        Hitbox.Position = WorldPosition;
                    }
                }
            }
            if (offset.Y != 0)
            {
                LinearY += offset.Y;
                Hitbox.Position = WorldPosition;
                IsOnGround = false;
                if (offset.Y > 0) IsFalling = true;
                if (_collisionOptions.HasFlag(CollosionOptions.CollidesWithTerrain) && CollidesWithTerrain)
                {
                    LinearY -= offset.Y;
                    Hitbox.Position = WorldPosition;
                    Velocity.Y = 0;
                    IsFalling = false;
                    if (offset.Y > 0) IsOnGround = true;
                    while (!CollidesWithTerrain)
                    {
                        LinearY += offset.Y < 0 ? -specific : specific;
                        Hitbox.Position = WorldPosition;
                    }
                    while (CollidesWithTerrain)
                    {
                        LinearY -= offset.Y < 0 ? -specific : specific;
                        Hitbox.Position = WorldPosition;
                    }
                }
            }
        }
    }
}