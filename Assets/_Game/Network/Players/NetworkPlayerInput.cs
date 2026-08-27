using Fusion;
using Game.Core.Players;
using UnityEngine;

namespace Game.Network.Players
{
    internal enum NetworkPlayerButton
    {
        Jump = 0,
        Sprint = 1,
        Crouch = 2,
        Prone = 3,
        Attack = 4
    }

    /// <summary>Input sent by one client for one Fusion simulation tick.</summary>
    public struct NetworkPlayerInput : INetworkInput
    {
        public Vector2 Move;
        public float LookYawDegrees;
        public NetworkButtons Buttons;

        public static NetworkPlayerInput FromIntent(PlayerInputIntent intent)
        {
            var buttons = default(NetworkButtons);
            buttons.Set(
                (int)NetworkPlayerButton.Jump,
                intent.IsPressed(PlayerInputButtons.Jump));
            buttons.Set(
                (int)NetworkPlayerButton.Sprint,
                intent.IsPressed(PlayerInputButtons.Sprint));
            buttons.Set(
                (int)NetworkPlayerButton.Crouch,
                intent.IsPressed(PlayerInputButtons.Crouch));
            buttons.Set(
                (int)NetworkPlayerButton.Prone,
                intent.IsPressed(PlayerInputButtons.Prone));
            buttons.Set(
                (int)NetworkPlayerButton.Attack,
                intent.IsPressed(PlayerInputButtons.Attack));

            return new NetworkPlayerInput
            {
                Move = new Vector2(intent.MoveX, intent.MoveY),
                LookYawDegrees = intent.LookYawDegrees,
                Buttons = buttons
            };
        }

        internal bool IsPressed(NetworkPlayerButton button) =>
            Buttons.IsSet((int)button);

        internal bool WasPressed(NetworkPlayerButton button, NetworkButtons previous) =>
            Buttons.WasPressed(previous, (int)button);
    }
}
