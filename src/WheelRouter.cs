using System;
using System.Drawing;
using System.Windows.Forms;

namespace ReInstala
{
    /// <summary>
    /// Encaminha a roda do mouse para a lista mesmo quando a busca, os botões
    /// ou outro controle está com o foco. O Windows entrega a roda ao controle
    /// focado; sem isso, rolar com o cursor sobre a lista podia não fazer nada.
    /// </summary>
    internal class WheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        private readonly AppList _list;
        private readonly Control _scrollBar;

        public WheelRouter(AppList list, Control scrollBar)
        {
            _list = list;
            _scrollBar = scrollBar;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if (_list == null || !_list.IsHandleCreated || !_list.Visible) return false;
            if (_list.RowCount == 0 || _list.Items.Count == 0) return false;

            Point cursor = new Point(
                (short)((long)m.LParam & 0xFFFF),
                (short)(((long)m.LParam >> 16) & 0xFFFF));
            if (!Over(_list, cursor) && !Over(_scrollBar, cursor)) return false;

            int delta = (short)((long)m.WParam >> 16);
            _list.ScrollByWheel(delta);
            return true;
        }

        private static bool Over(Control control, Point screenPoint)
        {
            if (control == null || control.IsDisposed) return false;
            if (!control.Visible || !control.IsHandleCreated) return false;

            Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
            return bounds.Contains(screenPoint);
        }
    }
}
