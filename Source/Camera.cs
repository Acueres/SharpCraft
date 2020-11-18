using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace SharpCraft
{
    public class Camera
    {
        public bool Moved
        {
            get
            {
                return cameraDelta.Length() > 0;
            }
        }

        public Matrix View;
        public Matrix Projection;

        public Vector3 Direction;
        public Vector3 HorizontalDirection;

        public BoundingFrustum Frustum;

        Vector3 target;

        Vector2 cameraDelta;
        Vector2 screenCenter;

        float rotationSpeed;


        public Camera(GraphicsDeviceManager graphics, Vector3 position, Vector3 _target)
        {
            target = _target;

            Direction = target - position;
            Direction.Normalize();

            HorizontalDirection = new Vector3(Direction.X, 0f, Direction.Z);
            HorizontalDirection.Normalize();

            rotationSpeed = 3.5f;

            View = Matrix.CreateLookAt(position, target, Vector3.Up);

            Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(70), 
                (float)graphics.GraphicsDevice.Viewport.Width / graphics.GraphicsDevice.Viewport.Height, 0.1f, 200);

            screenCenter = new Vector2(graphics.GraphicsDevice.Viewport.Width / 2,
                                       graphics.GraphicsDevice.Viewport.Height / 2);
            Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);

            Frustum = new BoundingFrustum(View * Projection);
        }

        public void Update(Vector3 position, GameTime gameTime)
        {
            MouseState currentMouseState = Mouse.GetState();

            float delta = (float)gameTime.ElapsedGameTime.TotalMilliseconds / 20f;
            cameraDelta = delta * (new Vector2(currentMouseState.X, currentMouseState.Y) - screenCenter);
            cameraDelta = Vector2.Clamp(cameraDelta, new Vector2(-20, -20), new Vector2(20, 20));
            cameraDelta *= rotationSpeed;

            if (Math.Abs(Direction.Y) > 0.99f && (Math.Sign(cameraDelta.Y) != Math.Sign(Direction.Y)))
            {
                cameraDelta.Y = 0;
            }

            Direction = Vector3.Transform(Direction,
                Matrix.CreateFromAxisAngle(Vector3.Up, (-MathHelper.PiOver4 / 150) * cameraDelta.X));
            Direction = Vector3.Transform(Direction,
                Matrix.CreateFromAxisAngle(Vector3.Cross(Vector3.Up, Direction), (MathHelper.PiOver4 / 100) * cameraDelta.Y));

            Direction.Normalize();

            HorizontalDirection.X = Direction.X;
            HorizontalDirection.Z = Direction.Z;

            HorizontalDirection.Normalize();

            target = Direction + position;

            Mouse.SetPosition((int)screenCenter.X, (int)screenCenter.Y);

            View = Matrix.CreateLookAt(position, target, Vector3.Up);

            Frustum.Matrix = View * Projection;
        }
    }
}
