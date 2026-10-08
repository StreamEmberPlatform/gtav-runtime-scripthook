//
// StreamEmber: 2D drawing helpers the natives do not offer (ChaosModV Memory::DrawLine / WorldToScreen).
//

using System.Drawing;
using GTA.Math;

namespace GTA.UI
{
    /// <summary>
    /// StreamEmber: draws on the game's 2D draw list (same list as <c>DRAW_RECT</c>, drawn this frame only).
    /// </summary>
    public static class ScreenDraw
    {
        /// <summary>Whether <see cref="Line"/> works in this game build.</summary>
        public static bool IsLineSupported => SHVDN.StreamEmberMemory.DrawLineAvailable;

        /// <summary>
        /// Draws a 2D line for this frame between two points in screen units (0..1). <paramref name="width"/> is in
        /// screen units too (e.g. 0.002). Up to 5000 lines per frame (the game's limit of 500 rects is raised).
        /// </summary>
        public static bool Line(PointF from, PointF to, float width, Color color)
            => SHVDN.StreamEmberMemory.DrawLine(from.X, from.Y, to.X, to.Y, width,
                (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B));

        /// <summary>
        /// Projects a world position to screen units (0..1) with the game's own projection. Returns
        /// <see langword="false"/> when the point is behind the camera; unlike
        /// <c>GET_SCREEN_COORD_FROM_WORLD_COORD</c> it also gives coordinates outside the screen.
        /// </summary>
        public static bool WorldToScreen(Vector3 position, out PointF screen)
        {
            bool ok = SHVDN.StreamEmberMemory.WorldToScreen(position.X, position.Y, position.Z, out float x, out float y);
            screen = new PointF(x, y);
            return ok;
        }
    }
}
