using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using SharpCraft.Utility;


namespace SharpCraft
{
    public class Player
    { 
        public bool LeftClick
        {
            get
            {
                return Clicked && Util.LeftButtonClicked(currentMouseState, previousMouseState);
            }
        }

        public bool RightClick
        {
            get
            {
                return Clicked && Util.RightButtonClicked(currentMouseState, previousMouseState);
            }
        }

        public Vector3 Position;

        public Camera Camera;
        public Physics Physics;

        public Ray Ray;
        public BoundingBox Bound;

        public bool Flying;
        public bool Walking;
        public bool Sprinting;
        public bool UpdateOccured;
        public bool Swimming;

        bool Clicked
        {
            get
            {
                return gameTime.TotalGameTime.TotalMilliseconds - lastClickTime > 100;
            }
        }

        double lastClickTime;
        double lastPressTime;

        Keys lastKeyPressed;

        GameTime gameTime;

        MouseState currentMouseState;
        MouseState previousMouseState;

        KeyboardState currentKeyboardState;
        KeyboardState previousKeyboardState;

        Vector3 boundMin;
        Vector3 boundMax;


        public Player(MainGame game, GraphicsDevice graphics, Parameters parameters)
        {
            Camera = new Camera(game, graphics, Position, parameters.Direction);

            Physics = new Physics(this);

            Ray = new Ray(parameters.Position, Camera.Direction);

            boundMin = new Vector3(-0.25f, -1.6f, -0.25f);
            boundMax = new Vector3(0.25f, 0.5f, 0.25f);

            Bound = new BoundingBox(boundMin + Position, boundMax + Position);

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

            Camera.Update(Position, gameTime);

            Physics.Update(gameTime);

            UpdateOccured = Camera.UpdateOccured || Physics.Moving || LeftClick || RightClick;

            Ray.Position = Position;
            Ray.Direction = Camera.Direction;

            Bound.Min = boundMin + Position;
            Bound.Max = boundMax + Position;

            previousKeyboardState = currentKeyboardState;
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
                    Position.Y += 0.1f;

                    Physics.Velocity.Y = -0.12f;

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
