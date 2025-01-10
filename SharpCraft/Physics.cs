using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SharpCraft.Utility;


namespace SharpCraft
{
    public class Physics
    {
        public bool Moving => Velocity.Length() > 0;

        public Vector3 Velocity;

        Player player;

        float maxSpeed;
        float acceleration;

        double elapsedTime;

        List<Vector3> collisionNormals;

        bool ceilingCollision;

        Dictionary<Vector3, Faces> faceNormals;


        public Physics(Player player)
        {
            this.player = player;

            maxSpeed = 0.055f;
            acceleration = 0.02f;

            collisionNormals = new List<Vector3>(2);

            faceNormals = new Dictionary<Vector3, Faces>
            {
                { new Vector3(0, 0, -1), Faces.ZPos },
                { new Vector3(0, 0, 1), Faces.ZNeg },
                { new Vector3(-1, 0, 0), Faces.XPos },
                { new Vector3(1, 0, 0), Faces.XNeg }
            };
        }

        public void Collision(Vector3 blockPosition, FacesState visibleSides)
        {
            float deltaY = player.Position.Y - blockPosition.Y - 1f;

            Vector3 blockCenterDirection = new(blockPosition.X - player.Position.X,
                0, blockPosition.Z - player.Position.Z);

            Vector3 normal = GetNormal(blockCenterDirection);

            bool insideBlock = Math.Abs(blockCenterDirection.X) < 0.7f &&
                Math.Abs(blockCenterDirection.Z) < 0.7f;

            //Ceiling collision
            if (deltaY < -1.5f && insideBlock)
            {
                Velocity.Y = 0;
                ceilingCollision = true;
                return;
            }

            //Floor collision
            if (deltaY > 0.5f && insideBlock)
            {
                player.Position = new Vector3(player.Position.X, blockPosition.Y + 1.97f, player.Position.Z);
                Velocity.Y = 0;

                player.Flying = false;
                player.Walking = true;
                return;
            }

            //Side collision
            if (deltaY < 0.5f)
            {
                if (faceNormals.TryGetValue(normal, out Faces value) && visibleSides.GetFaceValue(value))
                {
                    collisionNormals.Add(normal);

                    Velocity.X = 0;
                    Velocity.Z = 0;

                    player.Sprinting = false;
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            Vector3 positionDelta = new(0f, 0f, 0f);

            KeyboardState currentKeyboardState = Keyboard.GetState();

            elapsedTime = gameTime.ElapsedGameTime.TotalMilliseconds;

            float delta = (float)elapsedTime / 20f;
            float friction = 0.5f;
            float gravity = 9.8f;

            bool ascendingInWater = false;

            //Velocity update && friction
            if (player.Flying && Math.Abs(Velocity.Y) > 5e-3f)
            {
                Velocity.Y -= Math.Sign(Velocity.Y) * delta * friction * acceleration;
            }
            else if (player.Flying)
            {
                Velocity.Y = 0;
            }

            if (player.Flying)
            {
                friction = 0.1f;
            }

            if (Math.Abs(Velocity.X) > 5e-3f)
            {
                Velocity.X -= Math.Sign(Velocity.X) * delta * friction * acceleration;
            }
            else
            {
                Velocity.X = 0;
            }

            if (Math.Abs(Velocity.Z) > 5e-3f)
            {
                Velocity.Z -= Math.Sign(Velocity.Z) * delta * friction * acceleration;
            }
            else
            {
                Velocity.Z = 0;
            }


            //Movement control
            if (currentKeyboardState.IsKeyDown(Keys.W))
            {
                if (Math.Abs(Velocity.X) < maxSpeed)
                {
                    Velocity.X += delta * acceleration;
                }
                else
                {
                    Velocity.X = maxSpeed;
                }
            }

            if (currentKeyboardState.IsKeyDown(Keys.S))
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

            if (currentKeyboardState.IsKeyDown(Keys.A))
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

            if (currentKeyboardState.IsKeyDown(Keys.D))
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
            if (!ceilingCollision && player.Flying
                && currentKeyboardState.IsKeyDown(Keys.Space))
            {
                if (Velocity.Y < 2 * maxSpeed)
                {
                    Velocity.Y += 2 * delta * acceleration;
                }
                positionDelta.Y += Velocity.Y;
            }
            //Move up in water
            else if (!ceilingCollision && player.Swimming &&
                     currentKeyboardState.IsKeyDown(Keys.Space))
            {
                positionDelta.Y += 0.05f;
                ascendingInWater = true;
            }

            //Move down in flight mode
            if (player.Flying && currentKeyboardState.IsKeyDown(Keys.LeftShift))
            {
                if (Velocity.Y < 2 * maxSpeed)
                {
                    Velocity.Y += 2 * delta * acceleration;
                }

                positionDelta.Y -= Velocity.Y;
            }

            //Apply gravity in normal mode
            if (!player.Flying && !player.Walking && !ascendingInWater)
            {
                if (Velocity.Y < 1f)
                {
                    Velocity.Y += (delta * gravity) / 800f;
                }

                positionDelta.Y -= delta * Velocity.Y;
            }

            positionDelta += delta * Velocity.X * player.Camera.HorizontalDirection;

            if (player.Sprinting && !player.Swimming)
            {
                positionDelta.X *= 2;
                positionDelta.Z *= 2;
            }

            positionDelta += delta * Velocity.Z * Vector3.Cross(Vector3.Up, player.Camera.HorizontalDirection);

            foreach (var normal in collisionNormals)
            {
                if ((Math.Sign(positionDelta.Z) == Math.Sign(normal.Z) ||
                    Math.Sign(positionDelta.X) == Math.Sign(normal.X)))
                {
                    float dot = Vector3.Dot(positionDelta, normal);
                    positionDelta -= dot * normal;
                }
            }

            if (player.Swimming)
            {
                positionDelta.X *= 0.3f;
                positionDelta.Y *= 0.8f;
                positionDelta.Z *= 0.3f;
            }

            player.Position += positionDelta;

            collisionNormals.Clear();

            player.Walking = false;
            player.Swimming = false;

            ceilingCollision = false;
        }

        Vector3 GetNormal(Vector3 vector)
        {
            if (Math.Abs(vector.X) > Math.Abs(vector.Z))
            {
                return new Vector3(Math.Sign(vector.X), 0, 0);
            }
            else
            {
                return new Vector3(0, 0, Math.Sign(vector.Z));
            }
        }
    }
}
