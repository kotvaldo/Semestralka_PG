using System;
using System.Collections.Generic;
using System.Drawing;

namespace Semestralka_PG
{
    public class BezierCurve
    {
        private List<PointF> controlPoints = new List<PointF>();
        private List<PointF> curvePoints = new List<PointF>();
        private int curvePrecision = 8;

        public void SetControlPoints(List<PointF> points)
        {
            controlPoints = points ?? throw new ArgumentNullException(nameof(points));
            RecalculateCurve();
        }

        public void SetCurvePrecision(int precision)
        {
            if (precision < 2)
                throw new ArgumentException("Curve precision must be at least 2.");
            curvePrecision = precision;
            RecalculateCurve();
        }

        public void Draw(Graphics g)
        {
            if (g == null || controlPoints.Count < 2) return;

            // Draw curve points
           g.DrawCurve(Pens.Red, curvePoints.ToArray());

            // Draw lines between control points
            float[] dashValues = { 10, 6 };

            using (var pen = new Pen(Color.DarkGray))
            {
                pen.DashPattern = dashValues;
                for (int i = 0; i < controlPoints.Count - 1; i++)
                {
                    g.DrawLine(pen, controlPoints[i], controlPoints[i + 1]);
                }
            }

            // Draw control points
            DrawControlPoints(g);
        }

        public void DrawControlPoints(Graphics graphics)
        {
            using (var font = new Font("Arial", 8)) 

            for (int i = 0; i < controlPoints.Count; i++)
            {
                var uvPoint = controlPoints[i];
                var rect = new Rectangle((int)uvPoint.X - 5, (int)uvPoint.Y - 5, 10, 10);
                var brush = (i == 0 || i == controlPoints.Count - 1) ? Brushes.Green : Brushes.Orange;
                var label = (i == 0) ? "Start" : (i == controlPoints.Count - 1) ? "End" : $"C{i}";
                var textPosition = new Point((int)uvPoint.X + 10, (int)uvPoint.Y - 8);

                graphics.FillRectangle(brush, rect);
                graphics.DrawString(label, font, brush, textPosition);
            }
        }
        private void RecalculateCurve()
        {
            if (controlPoints.Count < 2)
            {
                curvePoints.Clear();
                return;
            }

            curvePoints = DeCasteljau.GetCurvePoints(controlPoints, curvePrecision);
        }
    }
}
