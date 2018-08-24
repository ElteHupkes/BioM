using System.Drawing;

namespace PointTracker
{
    public static class Extensions
    {
        public static PointF Center(this RectangleF rectangle)
        {
            return new PointF(rectangle.X + rectangle.Width / 2,
                rectangle.Y + rectangle.Height / 2);
        }

        public static PointF Add(this PointF point, PointF other)
        {
            return new PointF(point.X + other.X, point.Y + other.Y);
        }

        public static PointF Sub(this PointF point, PointF other)
        {
            return new PointF(point.X - other.X, point.Y - other.Y);
        }
        
        public static PointF Mult(this PointF point, float fac)
        {
            return new PointF(fac * point.X, fac * point.Y);
        }
        
        public static PointF Div(this PointF point, float fac)
        {
            return new PointF(point.X / fac, point.Y / fac);
        }
    }
}