using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Semestralka_PG
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    public static class DeCasteljau
    {
        public static PointF GetCurvePoint(List<PointF> controlPoints, float t)
        {
            if (controlPoints.Count == 1)
                return controlPoints[0];

            var nextLevel = new List<PointF>();

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                nextLevel.Add(Lerp(controlPoints[i], controlPoints[i + 1], t));
            }

            return GetCurvePoint(nextLevel, t);
        }

        public static List<PointF> GetCurvePoints(List<PointF> bezierControlPoints, int pointCount)
        {
            if (pointCount < 2)
                throw new ApplicationException($"Invalid parameter: you must request at least 2 points to be returned from the curve!");

            if (bezierControlPoints == null || bezierControlPoints.Count < 2)
                return null;

            var result = new List<PointF>();

            for (int i = 0; i < pointCount; i++)
            {
                float time = i == 0 ? 0 : i / (float)(pointCount - 1);
                result.Add(GetCurvePoint(bezierControlPoints, time));
            }

            return result;
        }

        private static PointF Lerp(PointF p1, PointF p2, float t)
        {
            return new PointF(
                (1 - t) * p1.X + t * p2.X,
                (1 - t) * p1.Y + t * p2.Y
            );
        }
    }

}
