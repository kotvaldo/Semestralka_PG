using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Semestralka_PG
{
    using System;
    using System.Drawing;

    public static class CoordTrans
    {
        public static float xMin = -296.0f;
        public static float xMax = 296.0f;
        public static float yMin = -225.0f;
        public static float yMax = 225.0f;

        public static int uMin = 0;
        public static int uMax = 606;
        public static int vMin = 450;
        public static int vMax = 0;

        public static float xRange { get { return Math.Abs(xMax - xMin); } }
        public static float yRange { get { return Math.Abs(yMax - yMin); } }
        public static float uRange { get { return Math.Abs(uMax - uMin); } }
        public static float vRange { get { return Math.Abs(vMax - vMin); } }

        public static Point FromXYtoUV(PointF worldPoint)
        {
            return new Point(
                (int)((worldPoint.X - xMin) / xRange * uRange) + uMin,
                (int)((worldPoint.Y - yMin) / yRange * vRange) + vMin
            );
        }

      
        public static PointF FromXYtoUVF(PointF worldPoint)
        {
            return new PointF(
                (worldPoint.X - xMin) / xRange * uRange + uMin,
                (worldPoint.Y - yMin) / yRange * vRange + vMin
            );
        }

       
        public static PointF FromUVtoXY(PointF screenPoint)
        {
            return new PointF(
                ((screenPoint.X - uMin) / uRange * xRange) + xMin,
                ((screenPoint.Y - vMin) / vRange * yRange) + yMin
            );
        }

     
        public static PointF FromUVtoXY(Point screenPoint)
        {
            return new PointF(
                ((screenPoint.X - uMin) / uRange * xRange) + xMin,
                ((screenPoint.Y - vMin) / vRange * yRange) + yMin
            );
        }
    }

}
