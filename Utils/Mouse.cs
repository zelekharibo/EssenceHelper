using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GameOffsets2.Native;

namespace EssenceHelper.Utils
{
    internal static class Mouse
    {
        public enum MouseEvents
        {
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040,
            Move = 0x00000001,
            Absolute = 0x00008000,
            RightDown = 0x00000008,
            RightUp = 0x00000010
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Vector2i lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public static Vector2i GetCursorPosition()
        {
            GetCursorPos(out Vector2i lpPoint);
            return lpPoint;
        }

        public static async Task MoveMouse(Vector2 pos)
        {
            Vector2i currentPos;
            var targetX = (int)pos.X;
            var targetY = (int)pos.Y;
            
            do
            {
                SetCursorPos(targetX, targetY);
                GetCursorPos(out currentPos);
                await Task.Delay(10);
            }
            while (currentPos.X != targetX || currentPos.Y != targetY);
        }

        public static async Task LeftDown()
        {
            mouse_event((int)MouseEvents.LeftDown, 0, 0, 0, 0);
            await Task.Delay(10);
        }

        public static async Task LeftUp()
        {
            mouse_event((int)MouseEvents.LeftUp, 0, 0, 0, 0);
            await Task.Delay(10);
        }

        public static async Task RightDown()
        {
            mouse_event((int)MouseEvents.RightDown, 0, 0, 0, 0);
            await Task.Delay(10);
        }

        public static async Task RightUp()
        {
            mouse_event((int)MouseEvents.RightUp, 0, 0, 0, 0);
            await Task.Delay(10);
        }
    }
}