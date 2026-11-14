using UnityEngine;

namespace GravityWells.Core
{
    public static class GeometryUtils
    {
        private const float OrientationEpsilon = 1e-6f;

        public static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        public static int Orientation(Vector2 o, Vector2 a, Vector2 b)
        {
            float cross = Cross(o, a, b);
            if (cross > OrientationEpsilon) return 1;
            if (cross < -OrientationEpsilon) return -1;
            return 0;
        }

        public static bool SegmentsProperlyIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            int d1 = Orientation(b1, b2, a1);
            int d2 = Orientation(b1, b2, a2);
            int d3 = Orientation(a1, a2, b1);
            int d4 = Orientation(a1, a2, b2);

            // General position: each segment's endpoints straddle the other segment's line.
            if (d1 != d2 && d3 != d4 && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0)
            {
                return true;
            }

            // Any zero orientation short of full collinearity means one endpoint merely
            // touches the other segment's line (shared endpoint or T-junction) — not a crossing.
            if (d1 == 0 && d2 == 0 && d3 == 0 && d4 == 0)
            {
                return CollinearSegmentsOverlapMoreThanAPoint(a1, a2, b1, b2);
            }

            return false;
        }

        // Where two properly-crossing segments meet. False for anything SegmentsProperlyIntersect rejects.
        public static bool TryGetIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 point)
        {
            point = default;
            if (!SegmentsProperlyIntersect(a1, a2, b1, b2))
            {
                return false;
            }

            Vector2 r = a2 - a1;
            Vector2 s = b2 - b1;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-12f)
            {
                return false; // collinear overlap has no single crossing point
            }

            float t = ((b1.x - a1.x) * s.y - (b1.y - a1.y) * s.x) / denominator;
            point = a1 + r * t;
            return true;
        }

        private static bool CollinearSegmentsOverlapMoreThanAPoint(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            // Project onto whichever axis the segment spans more, to stay stable near-vertical or near-horizontal.
            bool useX = Mathf.Abs(a2.x - a1.x) >= Mathf.Abs(a2.y - a1.y);

            float aMin, aMax, bMin, bMax;
            if (useX)
            {
                aMin = Mathf.Min(a1.x, a2.x);
                aMax = Mathf.Max(a1.x, a2.x);
                bMin = Mathf.Min(b1.x, b2.x);
                bMax = Mathf.Max(b1.x, b2.x);
            }
            else
            {
                aMin = Mathf.Min(a1.y, a2.y);
                aMax = Mathf.Max(a1.y, a2.y);
                bMin = Mathf.Min(b1.y, b2.y);
                bMax = Mathf.Max(b1.y, b2.y);
            }

            float overlapStart = Mathf.Max(aMin, bMin);
            float overlapEnd = Mathf.Min(aMax, bMax);
            return overlapEnd - overlapStart > OrientationEpsilon;
        }

        public static bool PointInCircumcircle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Double precision throughout: the incircle determinant is notoriously sensitive,
            // and float32 error here produces wrong triangulations (flickering/degenerate edges).
            double ax = a.x - p.x, ay = a.y - p.y;
            double bx = b.x - p.x, by = b.y - p.y;
            double cx = c.x - p.x, cy = c.y - p.y;

            double aSq = ax * ax + ay * ay;
            double bSq = bx * bx + by * by;
            double cSq = cx * cx + cy * cy;

            double det =
                ax * (by * cSq - cy * bSq) -
                ay * (bx * cSq - cx * bSq) +
                aSq * (bx * cy - cx * by);

            // The determinant's sign convention assumes a,b,c wound CCW; flip if they're CW
            // so the result is correct regardless of the triangle's stored winding.
            if (Cross(a, b, c) < 0)
            {
                det = -det;
            }

            return det > 0;
        }

        public static float SqrDistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.x * ab.x + ab.y * ab.y;
            if (lengthSq < OrientationEpsilon)
            {
                return (p - a).sqrMagnitude;
            }

            float t = Vector2.Dot(p - a, ab) / lengthSq;
            t = Mathf.Clamp01(t);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }
    }
}
