using System;
using System.Runtime.InteropServices;

namespace ReInstala
{
    /// <summary>
    /// Ativa o modo escuro dos controles nativos (barra de rolagem, cabeçalho da lista).
    /// São APIs não documentadas do Windows 10/11; se falharem, o app segue funcionando.
    /// </summary>
    internal static class NativeTheme
    {
        public static void SetDark(bool dark)
        {
            try
            {
                SetPreferredAppMode(dark ? 2 : 0);
                FlushMenuThemes();
            }
            catch (Exception)
            {
            }
        }

        public static void ApplyToWindow(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                AllowDarkModeForWindow(hwnd, dark);
                SetWindowTheme(hwnd, dark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch (Exception)
            {
            }
        }

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        private static extern int SetPreferredAppMode(int appMode);

        [DllImport("uxtheme.dll", EntryPoint = "#133", SetLastError = true)]
        private static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        private static extern void FlushMenuThemes();

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);
    }
}
