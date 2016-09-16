using System;
using Microsoft.Xna.Framework;
using SharpXNA.Collision;

namespace Orbis
{
    public class Entity
    {
        private static World GameWorld => Game.GameWorld;

        private Vector2 _linearPosition;
        public Vector2 LinearPosition
        {
            get { return _linearPosition; }
            set
            {
                if (IsBoundByWorld)
                {
                    float maxWidth = float.MaxValue, maxHeight = float.MaxValue;
                    if (GameWorld != null)
                    {
                        maxWidth = ((GameWorld.Width - 1)*Tile.Size);
                        maxHeight = ((GameWorld.Height - 1)*Tile.Size);
                    }
                    value.X = MathHelper.Clamp(value.X, (Tile.Size + (Hitbox.Width/2f)), (maxWidth - (Hitbox.Width/2f)));
                    value.Y = MathHelper.Clamp(value.Y, (Tile.Size + (Hitbox.Height/2f)), (maxHeight - (Hitbox.Height/2f)));
                }
                _linearPosition = value;
                _worldPosition = new Vector2((int) Math.Round(_linearPosition.X), (int) Math.Round(_linearPosition.Y));
                Hitbox.Position = _worldPosition;
                TileX = (int) Math.Floor(_worldPosition.X/Tile.Size);
                TileY = (int) Math.Floor(_worldPosition.Y/Tile.Size);
            }
        }

        protected Vector2 _worldPosition;
        public Vector2 WorldPosition => _worldPosition;

        public float LinearX
        {
            get { return LinearPosition.X; }
            set
            {
                if (IsBoundByWorld)
                {
                    var maxWidth = float.MaxValue;
                    if (GameWorld != null) maxWidth = ((GameWorld.Width - 1)*Tile.Size);
                    value = MathHelper.Clamp(value, (Tile.Size + (Hitbox.Width/2f)), (maxWidth - (Hitbox.Width/2f)));
                }
                _linearPosition.X = value;
                _worldPosition.X = (int) Math.Round(value);
                Hitbox.X = _worldPosition.X;
                TileX = (int)Math.Floor(_worldPosition.X / Tile.Size);
            }
        }
        public float LinearY
        {
            get { return LinearPosition.Y; }
            set
            {
                if (IsBoundByWorld)
                {
                    var maxHeight = float.MaxValue;
                    if (GameWorld != null) maxHeight = ((GameWorld.Height - 1)*Tile.Size);
                    value = MathHelper.Clamp(value, (Tile.Size + (Hitbox.Height/2f)), (maxHeight - (Hitbox.Height/2f)));
                }
                _linearPosition.Y = value;
                _worldPosition.Y = (int) Math.Round(value);
                Hitbox.Y = _worldPosition.Y;
                TileY = (int)Math.Floor(_worldPosition.Y / Tile.Size);
            }
        }

        public int TileX, TileY;

        private readonly CollisionOptions _collisionOptions;
        [Flags] public enum CollisionOptions { None = 0, CollidesWithTerrain = 1 }

        /// <summary>
        /// Create a new Entity.
        /// </summary>
        /// <param name="collisionOptions">The flags for collision detection when moving.</param>
        public Entity(CollisionOptions collisionOptions) { _collisionOptions = collisionOptions; }
        /// <summary>
        /// Create a new Entity.
        /// </summary>
        /// <param name="hitBox">The hitbox polygon for the entity.</param>
        /// <param name="collisionOptions">The flags for collision detection when moving.</param>
        public Entity(Polygon hitBox, CollisionOptions collisionOptions) { Hitbox = hitBox; _collisionOptions = collisionOptions; }

        /// <summary>
        /// True = The entity is moving downward. False = The entity is either on the ground or in the air (but not falling).
        /// </summary>
        public bool IsFalling;
        /// <summary>
        /// True = The entity is on top of a tile. False = The entity is in the air.
        /// </summary>
        public bool IsOnGround;
        /// <summary>
        /// Defines whether this entity is affected by the force of gravity. This is normally true.
        /// <para>True = Gravity affects this entity. (true in most cases)</para>
        /// <para>False = Gravity does not affect this entity. (useful for things like bullets and some particles)</para>
        /// </summary>
        public bool IsAffectedByGravity;
        public bool IsVelocityLocked;
        public bool IsBoundByWorld;
        /// <summary>
        /// How fast the entity is moving around in the world.
        /// </summary>
        public Vector2 Velocity;
        public const float Gravity = 720, MaxYVel = 840, MaxXVel = 1200;
        /// <summary>
        /// The direction that the entity is facing. -128 is left, 128 is right.
        /// </summary>
        public sbyte Direction;
        public float MovementResistance { get; private set; }

        
        private Polygon _hitBox;
        private int _tilesWidth, _tilesHeight;
        /// <summary>
        /// The collision polygon for this entity.
        /// </summary>
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

        public virtual void Update(GameTime time)
        {
            Move(Velocity*(float) time.ElapsedGameTime.TotalSeconds);
            if (!IsVelocityLocked)
            {
                if (_collisionOptions.HasFlag(CollisionOptions.CollidesWithTerrain))
                {
                    MovementResistance = GameWorld.Tiles[TileX, (TileY + 2)].MovementResistance;
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
                        if (GameWorld.InBounds(x, y) && GameWorld.Tiles[x, y].Solid)
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
                var val = MathHelper.Clamp(offset.X, -Tile.Size, Tile.Size);
                LinearX += val;
                if (_collisionOptions.HasFlag(CollisionOptions.CollidesWithTerrain) && CollidesWithTerrain)
                {
                    Hitbox.Y -= Tile.Size;
                    if (CollidesWithTerrain) Hitbox.Y += Tile.Size;
                    else { LinearY = Hitbox.Y; break; }
                    LinearX -= val;
                    Velocity.X = 0;
                    var normal = offset.X < 0 ? -specific : specific;
                    while (!CollidesWithTerrain) LinearX += normal;
                    while (CollidesWithTerrain) LinearX -= normal;
                    break;
                }
                if (offset.X > 0) offset.X = MathHelper.Max(0, (offset.X - val));
                else offset.X = MathHelper.Min(0, (offset.X - val));
            }
            while (offset.Y != 0)
            {
                var val = MathHelper.Clamp(offset.Y, -Tile.Size, Tile.Size);
                LinearY += val;
                IsOnGround = false;
                if (offset.Y > 0) IsFalling = true;
                if (_collisionOptions.HasFlag(CollisionOptions.CollidesWithTerrain) && CollidesWithTerrain)
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
                if (offset.Y > 0) offset.Y = MathHelper.Max(0, (offset.Y - val));
                else offset.Y = MathHelper.Min(0, (offset.Y - val));
            }
        }
    }
}