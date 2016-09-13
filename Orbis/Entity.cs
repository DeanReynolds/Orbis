using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpXNA.Collision;

namespace Orbis
{
    public class Entity
    {
        private static World World => Game.World;
        private static Texture2D PlayerTexture => Game.PlayerTexture;

        private Vector2 _linearPosition;
        public Vector2 LinearPosition
        {
            get { return _linearPosition; }
            set
            {
                value.X = MathHelper.Clamp(value.X, (Tile.Size + (PlayerTexture.Width / 2)), (((World.Tiles.GetLength(0) - 1) * Tile.Size) - (PlayerTexture.Width / 2)));
                value.Y = MathHelper.Clamp(value.Y, (Tile.Size + (PlayerTexture.Height / 2)), (((World.Tiles.GetLength(1) - 1) * Tile.Size) - (PlayerTexture.Height / 2)));
                _linearPosition = value;
                _worldPosition = new Vector2((int) Math.Round(value.X), (int) Math.Round(value.Y));
                Hitbox.Position = _worldPosition;
                TileX = (int)Math.Floor(value.X / Tile.Size);
                TileY = (int)Math.Floor(value.Y / Tile.Size);
            }
        }

        private Vector2 _worldPosition;
        public Vector2 WorldPosition => _worldPosition;

        public float LinearX
        {
            get { return LinearPosition.X; }
            set
            {
                value = MathHelper.Clamp(value, (Tile.Size + (PlayerTexture.Width / 2)), (((World.Tiles.GetLength(0) - 1) * Tile.Size) - (PlayerTexture.Width / 2)));
                _linearPosition.X = value;
                _worldPosition.X = (int) Math.Round(value);
                Hitbox.X = _worldPosition.X;
                TileX = (int)Math.Floor(value / Tile.Size);
            }
        }
        public float LinearY
        {
            get { return LinearPosition.Y; }
            set
            {
                value = MathHelper.Clamp(value, (Tile.Size + (PlayerTexture.Height / 2)), (((World.Tiles.GetLength(1) - 1) * Tile.Size) - (PlayerTexture.Height / 2)));
                _linearPosition.Y = value;
                _worldPosition.Y = (int) Math.Round(value);
                Hitbox.Y = _worldPosition.Y;
                TileY = (int)Math.Floor(value / Tile.Size);
            }
        }

        public int TileX, TileY;

        private readonly CollosionOptions _collisionOptions;
        [Flags] public enum CollosionOptions { None = 0, CollidesWithTerrain = 1 }

        /// <summary>
        /// Create a new Entity.
        /// </summary>
        /// <param name="collisionOptions">The flags for collision detection when moving.</param>
        public Entity(CollosionOptions collisionOptions) { _collisionOptions = collisionOptions; }
        /// <summary>
        /// Create a new Entity.
        /// </summary>
        /// <param name="hitBox">The hitbox polygon for the entity.</param>
        /// <param name="collisionOptions">The flags for collision detection when moving.</param>
        public Entity(Polygon hitBox, CollosionOptions collisionOptions) { Hitbox = hitBox; _collisionOptions = collisionOptions; }

        public bool IsFalling, IsOnGround, IsAffectedByGravity, IsVelocitLocked;
        public Vector2 Velocity;
        public const float Gravity = 720, MaxYVel = 840, MaxXVel = 1200;
        public float MovementResistance { get; private set; }

        private Polygon _hitBox;
        private int _tilesWidth, _tilesHeight;
        public Polygon Hitbox
        {
            get { return _hitBox; }
            set
            {
                _tilesWidth = (int) Math.Ceiling((value.Width/Tile.Size)/2);
                _tilesHeight = (int) Math.Ceiling((value.Height/Tile.Size)/2);
                _hitBox = value;
            }
        }

        public void Update(GameTime time)
        {
            Move(Velocity*(float) time.ElapsedGameTime.TotalSeconds);
            if (!IsVelocitLocked)
            {
                if (_collisionOptions.HasFlag(CollosionOptions.CollidesWithTerrain))
                {
                    MovementResistance = World.Tiles[TileX, (TileY + 2)].MovementResistance;
                    var movementResistance = (MovementResistance*(float) time.ElapsedGameTime.TotalSeconds);
                    if (Velocity.X > 0) Velocity.X = MathHelper.Clamp((Velocity.X - movementResistance), 0, MaxXVel);
                    else if (Velocity.X < 0) Velocity.X = MathHelper.Clamp((Velocity.X + movementResistance), -MaxXVel, 0);
                }
                if (IsAffectedByGravity) Velocity.Y = MathHelper.Min(MaxYVel, (Velocity.Y + (Gravity*(float) time.ElapsedGameTime.TotalSeconds)));
            }
        }

        public bool CollidesWithTerrain
        {
            get
            {
                for (var x = (TileX - _tilesWidth); x <= (TileX + _tilesWidth); x++)
                    for (var y = (TileY - _tilesHeight); y <= (TileY + _tilesHeight); y++)
                        if (World.InBounds(x, y) && World.Tiles[x, y].Solid)
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
            while (offset.X != 0)
            {
                var val = MathHelper.Min(MathHelper.Max(offset.X, -Tile.Size), Tile.Size);
                LinearX += val;
                if (_collisionOptions.HasFlag(CollosionOptions.CollidesWithTerrain) && CollidesWithTerrain)
                {
                    LinearX -= val;
                    Velocity.X = 0;
                    var normal = offset.X < 0 ? -specific : specific;
                    while (!CollidesWithTerrain) LinearX += normal;
                    while (CollidesWithTerrain) LinearX -= normal;
                    break;
                }
                offset.X -= val;
            }
            while (offset.Y != 0)
            {
                var val = MathHelper.Min(MathHelper.Max(offset.Y, -Tile.Size), Tile.Size);
                LinearY += val;
                IsOnGround = false;
                if (offset.Y > 0) IsFalling = true;
                if (_collisionOptions.HasFlag(CollosionOptions.CollidesWithTerrain) && CollidesWithTerrain)
                {
                    LinearY -= val;
                    Velocity.Y = 0;
                    IsFalling = false;
                    if (offset.Y > 0) IsOnGround = true;
                    var normal = offset.Y < 0 ? -specific : specific;
                    while (!CollidesWithTerrain) LinearY += normal;
                    while (CollidesWithTerrain) LinearY -= normal;
                    break;
                }
                offset.Y -= val;
            }
        }
    }
}