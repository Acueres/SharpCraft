using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;


namespace SharpCraft
{
    public class Player
    {
        public Vector3 Position;
        public Vector3 PreviousPosition;
        public Camera Camera;
        public Physics Physics;
        public Ray Ray;
        public BoundingBox Bounds;

        public bool IsFlying;
        public bool OnGround;
        public bool Sprint;
        public bool UpdateOccured;
        public bool Swimming;

        public MouseState CurrentMouseState;
        public MouseState PreviousMouseState;

        public double LastClickTime;

        public GameTime GameTime;

        public KeyboardState CurrentKeyboardState;
        public KeyboardState PreviousKeyboardState;

        Vector3 min;
        Vector3 max;

        Keys lastKeyPressed;
        double lastPressTime;


        public Player(GraphicsDeviceManager graphics, Vector3 position, Vector3 target)
        {
            Camera = new Camera(graphics, Position, target);

            Physics = new Physics(this);

            Ray = new Ray(position, Camera.Direction);

            Position = position;

            IsFlying = Parameters.IsFlying;
            Sprint = false;
            UpdateOccured = true;
            Swimming = false;

            CurrentKeyboardState = Keyboard.GetState();

            CurrentMouseState = Mouse.GetState();

            LastClickTime = 0;

            min = new Vector3(-0.25f, -1.6f, -0.25f);
            max = new Vector3(0.25f, 0.5f, 0.25f);

            Bounds = new BoundingBox(min + Position, max + Position);
        }


        public void Update(GameTime _gameTime)
        {
            GameTime = _gameTime;

            CurrentKeyboardState = Keyboard.GetState();

            PreviousMouseState = CurrentMouseState;
            CurrentMouseState = Mouse.GetState();

            PreviousPosition = Position;

            UpdateState();
            Camera.Update(Position, GameTime);
            Physics.Update();

            UpdateOccured = Camera.cameraDelta.Length() > 0 || Physics.Velocity.Length() > 0 ||
                Util.LeftButtonClicked(CurrentMouseState, PreviousMouseState) ||
                Util.RightButtonClicked(CurrentMouseState, PreviousMouseState);

            Ray.Position = Position;
            Ray.Direction = Camera.Direction;

            Bounds.Min = min + Position;
            Bounds.Max = max + Position;

            PreviousKeyboardState = CurrentKeyboardState;
        }


        void UpdateState()
        {
            //State change
            if (Util.KeyPressed(Keys.Space, CurrentKeyboardState, PreviousKeyboardState) &&
                lastKeyPressed == Keys.Space && GameTime.TotalGameTime.TotalMilliseconds - lastPressTime < 225)
            {
                IsFlying ^= true;
                Physics.Velocity.Y = 0;
            }
            else if (CurrentKeyboardState.IsKeyDown(Keys.Space))
            {
                lastPressTime = GameTime.TotalGameTime.TotalMilliseconds;
                lastKeyPressed = Keys.Space;

                if (!IsFlying && OnGround)
                {
                    Position.Y += 0.1f;

                    Physics.Velocity.Y = -0.12f;

                    OnGround = false;
                }
            }

            if (Util.KeyReleased(Keys.W, CurrentKeyboardState, PreviousKeyboardState))
            {
                Sprint = false;
            }

            if (Util.KeyPressed(Keys.W, CurrentKeyboardState, PreviousKeyboardState) && lastKeyPressed == Keys.W && GameTime.TotalGameTime.TotalMilliseconds - lastPressTime < 225)
            {
                Sprint = true;
            }

            if (Util.KeyPressed(Keys.W, CurrentKeyboardState, PreviousKeyboardState))
            {
                lastPressTime = GameTime.TotalGameTime.TotalMilliseconds;
                lastKeyPressed = Keys.W;
            }
        }
    }
}
