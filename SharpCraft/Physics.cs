using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SharpCraft.Utilities;

namespace SharpCraft
{
    public class Physics(Player player)
    {
        public bool Moving => Velocity.Length() > 0;
        public Vector3 Velocity;

        readonly Player player = player;

        public void ResolveCollision(BoundingBox blockBox)
        {
            int iterations = 0;
            const int maxIterations = 1;

            // Loop until the player's bounding box no longer intersects the block
            // or until the iteration limit is reached
            while (player.Bound.Intersects(blockBox) && iterations < maxIterations)
            {
                var playerBox = player.Bound;

                // Compute penetration (overlap) amounts along each axis
                float overlapX = Math.Min(playerBox.Max.X, blockBox.Max.X) - Math.Max(playerBox.Min.X, blockBox.Min.X);
                float overlapY = Math.Min(playerBox.Max.Y, blockBox.Max.Y) - Math.Max(playerBox.Min.Y, blockBox.Min.Y);
                float overlapZ = Math.Min(playerBox.Max.Z, blockBox.Max.Z) - Math.Max(playerBox.Min.Z, blockBox.Min.Z);

                // Choose the axis with the smallest overlap to resolve first
                float minOverlap = overlapX;
                AxisDirection side = AxisDirection.X;
                if (overlapY < minOverlap)
                {
                    minOverlap = overlapY;
                    side = AxisDirection.Y;
                }
                if (overlapZ < minOverlap)
                {
                    side = AxisDirection.Z;
                }

                Vector3 playerCenter = (playerBox.Min + playerBox.Max) * 0.5f;
                Vector3 blockCenter = (blockBox.Min + blockBox.Max) * 0.5f;

                // Resolve collision along the axis
                if (side == AxisDirection.X)
                {
                    int dir = Math.Sign(playerCenter.X - blockCenter.X);
                    player.Position.X += dir * (overlapX);
                    Velocity.X = 0;
                    player.Sprinting = false;
                }
                else if (side == AxisDirection.Y)
                {
                    int dir = Math.Sign(playerCenter.Y - blockCenter.Y);

                    if (dir > 0) // Floor collision
                    {
                        player.Position.Y += overlapY;
                        player.Walking = true;
                        player.Flying = false;
                    }
                    else // Ceiling collision
                    {
                        player.Position.Y -= overlapY;
                    }
                    Velocity.Y = 0;
                }
                else // side == AxisDirection.Z
                {
                    int dir = Math.Sign(playerCenter.Z - blockCenter.Z);
                    player.Position.Z += dir * overlapZ;
                    Velocity.Z = 0;
                    player.Sprinting = false;
                }

                // Update the player's bounding box after each correction.
                player.UpdateBound();
                player.UpdateCamera();
                iterations++;
            }
        }

        public void Update(GameTime gameTime)
        {
            KeyboardState ks = Keyboard.GetState();

            const float maxSpeed = 0.055f;
            const float gravityAcceleration = 9.8f;
            const float acceleration = 0.02f;
            const float groundFriction = 0.5f;
            const float airFriction = 0.1f;
            const float eps = 5e-3f;
            const float terminalVelocity = 1f;

            float friction = player.Flying ? airFriction : groundFriction;

            double elapsedTime = gameTime.ElapsedGameTime.TotalMilliseconds;

            float delta = (float)elapsedTime / 20f;

            if (player.Flying && Math.Abs(Velocity.Y) > eps)
            {
                Velocity.Y -= Math.Sign(Velocity.Y) * delta * friction * acceleration;
            }
            else if (player.Flying)
            {
                Velocity.Y = 0;
            }

            if (Math.Abs(Velocity.X) > eps)
            {
                Velocity.X -= Math.Sign(Velocity.X) * delta * friction * acceleration;
            }
            else
            {
                Velocity.X = 0;
            }

            if (Math.Abs(Velocity.Z) > eps)
            {
                Velocity.Z -= Math.Sign(Velocity.Z) * delta * friction * acceleration;
            }
            else
            {
                Velocity.Z = 0;
            }

            //Apply gravity in normal mode
            if (!player.Flying)
            {
                if (Velocity.Y < terminalVelocity)
                {
                    Velocity.Y -= (delta * gravityAcceleration) / 800f;
                }

                //positionDelta.Y -= delta * Velocity.Y;
            }


            //Movement control
            if (ks.IsKeyDown(Keys.W))
            {
                if (Math.Abs(Velocity.X) <  maxSpeed)
                {
                    Velocity.X += delta * acceleration;
                }
                else
                {
                    Velocity.X = maxSpeed;
                }
            }

            if (ks.IsKeyDown(Keys.S))
            {
                if (Math.Abs(Velocity.X) < maxSpeed)
                {
                    Velocity.X -= delta * acceleration;
                }
                else
                {
                    Velocity.X = -maxSpeed;
                }
            }

            if (ks.IsKeyDown(Keys.A))
            {
                if (Math.Abs(Velocity.Z) < maxSpeed)
                {
                    Velocity.Z += delta * acceleration;
                }
                else
                {
                    Velocity.Z = maxSpeed;
                }
            }

            if (ks.IsKeyDown(Keys.D))
            {
                if (Math.Abs(Velocity.Z) < maxSpeed)
                {
                    Velocity.Z -= delta * acceleration;
                }
                else
                {
                    Velocity.Z = -maxSpeed;
                }
            }


            //Vertical movement
            //Move up in flight mode
            if (player.Flying && ks.IsKeyDown(Keys.Space))
            {
                if (Math.Abs(Velocity.Y) < 2 * maxSpeed)
                {
                    Velocity.Y += delta * acceleration;
                }
                else
                {
                    Velocity.Y = 2 * maxSpeed;
                }
            }

            //Move down in flight mode
            if (player.Flying && ks.IsKeyDown(Keys.LeftShift))
            {
                if (Math.Abs(Velocity.Y) < 2 * maxSpeed)
                {
                    Velocity.Y -= delta * acceleration;
                }
                else
                {
                    Velocity.Y = -2 * maxSpeed;
                }
            }

            Vector3 positionDelta = Vector3.Zero;

            positionDelta.Y += delta * Velocity.Y;
            positionDelta += delta * Velocity.X * player.Camera.HorizontalDirection;
            positionDelta += delta * Velocity.Z * Vector3.Cross(Vector3.Up, player.Camera.HorizontalDirection);

            player.Position += positionDelta;

            player.UpdateBound();
        }
    }
}
