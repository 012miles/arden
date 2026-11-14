using System;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Double-precision vector. All orbital and flight state lives in these;
    /// floats are only used at the very end, for rendering relative to the camera.
    /// </summary>
    [Serializable]
    public struct Vector3d
    {
        public double x, y, z;

        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }

        public static readonly Vector3d zero = new Vector3d(0, 0, 0);
        public static readonly Vector3d up = new Vector3d(0, 1, 0);
        public static readonly Vector3d right = new Vector3d(1, 0, 0);

        public double sqrMagnitude => x * x + y * y + z * z;
        public double magnitude => Math.Sqrt(x * x + y * y + z * z);

        public Vector3d normalized
        {
            get
            {
                double m = magnitude;
                return m > 1e-300 ? new Vector3d(x / m, y / m, z / m) : zero;
            }
        }

        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3d operator -(Vector3d a) => new Vector3d(-a.x, -a.y, -a.z);
        public static Vector3d operator *(Vector3d a, double s) => new Vector3d(a.x * s, a.y * s, a.z * s);
        public static Vector3d operator *(double s, Vector3d a) => new Vector3d(a.x * s, a.y * s, a.z * s);
        public static Vector3d operator /(Vector3d a, double s) => new Vector3d(a.x / s, a.y / s, a.z / s);

        public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3d Cross(Vector3d a, Vector3d b) =>
            new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        public static double Distance(Vector3d a, Vector3d b) => (a - b).magnitude;
        public static Vector3d Lerp(Vector3d a, Vector3d b, double t) => a + (b - a) * t;

        /// <summary>Angle between two vectors in radians.</summary>
        public static double Angle(Vector3d a, Vector3d b)
        {
            double d = Dot(a.normalized, b.normalized);
            return Math.Acos(Math.Max(-1, Math.Min(1, d)));
        }

        /// <summary>Rotate about +Y by an angle in radians. Matches Quaternion.Euler(0, deg, 0).</summary>
        public static Vector3d RotateY(Vector3d v, double angle)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            return new Vector3d(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
        }

        /// <summary>Rotate by a Unity quaternion in double precision.</summary>
        public static Vector3d Rotate(Quaternion q, Vector3d v)
        {
            double qx = q.x, qy = q.y, qz = q.z, qw = q.w;
            double tx = 2 * (qy * v.z - qz * v.y);
            double ty = 2 * (qz * v.x - qx * v.z);
            double tz = 2 * (qx * v.y - qy * v.x);
            return new Vector3d(
                v.x + qw * tx + (qy * tz - qz * ty),
                v.y + qw * ty + (qz * tx - qx * tz),
                v.z + qw * tz + (qx * ty - qy * tx));
        }

        public Vector3 ToFloat() => new Vector3((float)x, (float)y, (float)z);
        public static Vector3d FromFloat(Vector3 v) => new Vector3d(v.x, v.y, v.z);

        public override string ToString() => $"({x:F1}, {y:F1}, {z:F1})";
    }
}
