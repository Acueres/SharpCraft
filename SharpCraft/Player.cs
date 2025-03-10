using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpCraft.MathUtilities;
using SharpCraft.Persistence;
using SharpCraft.Utilities;


namespace SharpCraft
{
    public class Player
    { 
        public bool LeftClick => HasClicked && Util.LeftButtonClicked(currentMouseState, previousMouseState);

        public bool RightClick => HasClicked && Util.RightButtonClicked(currentMouseState, previousMouseState);

        public Vector3I Index { get; set; }
        public Vector3 Position;

        public Camera Camera { get; set; }
        public Physics Physics { get; set; }

        public Ray Ray { get; set; }
        public BoundingBox Bound { get; set; }

        public bool Flying { get; set; }
        public bool Walking { get; set; }
        public bool Sprinting { get; set; }
        public bool UpdateOccured { get; set; }
        public bool Swimming { get; set; }

        bool HasClicked => gameTime.TotalGameTime.TotalMilliseconds - lastClickTime > 100;

        double lastClickTime;
        double lastPressTime;

        Keys lastKeyPressed;

        GameTime gameTime;

        MouseState currentMouseState;
        MouseState previousMouseState;

        KeyboardState currentKeyboardState;
        KeyboardState previousKeyboardState;

        Vector3 boundMax;


        public Player(GraphicsDevice graphics, Parameters parameters)
        {
            Vector3 cameraPosition = new(Position.X + 0.25f, Position.Y + 1.6f, Position.Z + 0.25f);

            Camera = new Camera(graphics, cameraPosition, parameters.Direction);

            Physics = new Physics(this);

            Ray = new Ray(parameters.Position, Camera.Direction);

            boundMax = new Vector3(0.5f, 1.8f, 0.5f);

            Bound = new BoundingBox(Position, boundMax + Position);

            Position = parameters.Position;

            Flying = parameters.IsFlying;
            Sprinting = false;
            UpdateOccured = true;
            Swimming = false;

            lastClickTime = 0;

            currentMouseState = Mouse.GetState();

            currentKeyboardState = Keyboard.GetState();
        }


        public void Update(GameTime _gameTime)
        {
            gameTime = _gameTime;

            currentKeyboardState = Keyboard.GetState();

            previousMouseState = currentMouseState;
            currentMouseState = Mouse.GetState();

            CheckInput();

            UpdateCamera();

            Physics.Update(gameTime);

            UpdateOccured = Camera.UpdateOccured || Physics.Moving || LeftClick || RightClick;

            Ray = new Ray(Position, Camera.Direction);

            Bound = new BoundingBox(Position, boundMax + Position);

            previousKeyboardState = currentKeyboardState;
        }

        public void UpdateBound()
        {
            Bound = new BoundingBox(Position, boundMax + Position);
        }

        public void UpdateCamera()
        {
            Camera.Position = new(Position.X + 0.25f, Position.Y + 1.6f, Position.Z + 0.25f);
            Camera.Update(gameTime);
        }

        public void SaveParameters(Parameters parameters)
        {
            parameters.IsFlying = Flying;
            parameters.Position = Position;
            parameters.Direction = Camera.Direction;
        }

        public void SetLastClickTime()
        {
            lastClickTime = gameTime.TotalGameTime.TotalMilliseconds;
        }

        void CheckInput()
        {
            if (Util.KeyPressed(Keys.Space, currentKeyboardState, previousKeyboardState) &&
                lastKeyPressed == Keys.Space && gameTime.TotalGameTime.TotalMilliseconds - lastPressTime < 225)
            {
                Flying ^= true;
                Physics.Velocity.Y = 0;
            }
            else if (currentKeyboardState.IsKeyDown(Keys.Space))
            {
                lastPressTime = gameTime.TotalGameTime.TotalMilliseconds;
                lastKeyPressed = Keys.Space;

                if (!Flying && Walking)
                {
                    Physics.Velocity.Y = 0.17f;
                    Walking = false;
                }
            }

            if (Util.KeyReleased(Keys.W, currentKeyboardState, previousKeyboardState))
            {
                Sprinting = false;
            }

            if (Util.KeyPressed(Keys.W, currentKeyboardState, previousKeyboardState) && lastKeyPressed == Keys.W &&
                gameTime.TotalGameTime.TotalMilliseconds - lastPressTime < 225)
            {
                Sprinting = true;
            }

            if (Util.KeyPressed(Keys.W, currentKeyboardState, previousKeyboardState))
            {
                lastPressTime = gameTime.TotalGameTime.TotalMilliseconds;
                lastKeyPressed = Keys.W;
            }
        }
    }
}
