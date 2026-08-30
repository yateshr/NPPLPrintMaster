using System;
using System.Drawing;

namespace NPPLPrintMaster
{
    public class CanvasItem
    {
        public string FilePath;
        public Image Img;
        public int X, Y, Width, Height;
        public double OriginalAspect;
        public string TextTemplate;
        public bool ShowText;
        public Font ItemFont;
        public int Rotation { get; set; } = 0;

        public Rectangle ResizeHandle
        {
            get
            {
                // DYNAMIC RESIZE FIX: The box scales proportionally to the image width!
                int dynamicBoxHeight = (int)(Width * 0.13);
                return new Rectangle(X + Width - 15, Y + Height + (ShowText ? dynamicBoxHeight + 10 : 0) - 15, 30, 30);
            }
        }
    }
}