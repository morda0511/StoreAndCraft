using HarmonyLib;
using UnityEngine;

namespace StoreAndCraft
{
    /// <summary>
    /// Short camera flight to a chest (display menu: right click on an item). The game camera keeps computing
    /// its normal pose every frame; after it, this blends the pose towards a view of the chest and back, so
    /// nothing has to be remembered or restored. The menu is hidden meanwhile so the world is visible.
    /// </summary>
    internal static class LocateCamera
    {
        private const float FlyIn = 0.8f;
        private const float Hold = 2.2f;
        private const float FlyOut = 0.8f;

        private static bool _active;
        private static Vector3 _focus;
        private static float _start;

        internal static bool Active
        {
            get { return _active; }
        }

        /// <summary>Total time until the camera is back (the blink starts when it arrives).</summary>
        internal static float ArriveDelay
        {
            get { return FlyIn; }
        }

        internal static void Fly(Vector3 focus)
        {
            _focus = focus;
            _start = Time.unscaledTime;
            _active = true;
            DisplayTypeMenu.SetHidden(true);
        }

        internal static void Stop()
        {
            if (!_active)
                return;
            _active = false;
            DisplayTypeMenu.SetHidden(false);
        }

        internal static void Apply(GameCamera cam)
        {
            if (!_active || cam == null)
                return;
            Player player = Player.m_localPlayer;
            float t = Time.unscaledTime - _start;
            if (player == null || t >= FlyIn + Hold + FlyOut)
            {
                Stop();
                return;
            }

            float w = t < FlyIn ? t / FlyIn : t < FlyIn + Hold ? 1f : (FlyIn + Hold + FlyOut - t) / FlyOut;
            w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w));

            // View from the player's side of the chest, a bit above, 5 m away.
            Vector3 side = player.transform.position - _focus;
            side.y = 0f;
            side = side.sqrMagnitude < 0.25f ? Vector3.back : side.normalized;
            Vector3 pos = _focus + side * 5f + Vector3.up * 2.5f;
            Quaternion rot = Quaternion.LookRotation((_focus + Vector3.up * 0.5f) - pos, Vector3.up);

            Transform tr = cam.transform;
            tr.position = Vector3.Lerp(tr.position, pos, w);
            tr.rotation = Quaternion.Slerp(tr.rotation, rot, w);
        }
    }

    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    internal static class LocateCameraPatch
    {
        private static void Postfix(GameCamera __instance)
        {
            LocateCamera.Apply(__instance);
        }
    }
}
