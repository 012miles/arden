using System;
using System.Collections.Generic;

namespace Rocket
{
    /// <summary>
    /// Two-body Kepler orbit built from a state vector. Handles ellipses and hyperbolas.
    /// Used to coast vessels "on rails" (exact, so orbits never drift) and to draw the map view.
    /// </summary>
    public class Orbit
    {
        public double mu, epoch;
        public double a;       // semi-major axis (negative for hyperbolic)
        public double e;       // eccentricity
        public double p;       // semi-latus rectum
        public double n;       // mean motion (rad/s)
        public double M0;      // mean anomaly at epoch
        public Vector3d P, Q, H; // periapsis direction, in-plane perpendicular, angular momentum (unit)

        public bool IsElliptic => e < 1.0;
        public double Periapsis => a * (1 - e);
        public double Apoapsis => IsElliptic ? a * (1 + e) : double.PositiveInfinity;
        public double Period => IsElliptic ? 2 * Math.PI / n : double.PositiveInfinity;

        public static Orbit FromState(Vector3d r, Vector3d v, double mu, double epoch)
        {
            var o = new Orbit { mu = mu, epoch = epoch };
            double rm = r.magnitude;
            Vector3d h = Vector3d.Cross(r, v);
            double hm = h.magnitude;
            if (hm < 1e-3 * Math.Max(1, rm))
            {
                // Straight up/down: nudge sideways so the math stays finite (display only).
                Vector3d side = Vector3d.Cross(r, Math.Abs(r.normalized.y) < 0.9 ? Vector3d.up : Vector3d.right).normalized;
                v = v + side * 0.01;
                h = Vector3d.Cross(r, v);
                hm = h.magnitude;
            }
            o.H = h / hm;
            o.p = hm * hm / mu;
            Vector3d eVec = Vector3d.Cross(v, h) / mu - r / rm;
            o.e = eVec.magnitude;
            if (Math.Abs(o.e - 1) < 1e-7) o.e = 1 - 1e-7; // avoid the exactly-parabolic case
            o.a = o.p / (1 - o.e * o.e);
            o.P = o.e > 1e-9 ? eVec / o.e : r / rm;
            o.Q = Vector3d.Cross(o.H, o.P);

            double nu = Math.Atan2(Vector3d.Dot(r, o.Q), Vector3d.Dot(r, o.P));
            if (o.IsElliptic)
            {
                o.n = Math.Sqrt(mu / (o.a * o.a * o.a));
                double E = Math.Atan2(Math.Sqrt(1 - o.e * o.e) * Math.Sin(nu), o.e + Math.Cos(nu));
                o.M0 = E - o.e * Math.Sin(E);
            }
            else
            {
                double am = -o.a;
                o.n = Math.Sqrt(mu / (am * am * am));
                double sinhF = Math.Sqrt(o.e * o.e - 1) * Math.Sin(nu) / (1 + o.e * Math.Cos(nu));
                double F = Asinh(sinhF);
                o.M0 = o.e * Math.Sinh(F) - F;
            }
            return o;
        }

        public double MeanAnomalyAt(double t) => M0 + n * (t - epoch);

        public void StateAt(double t, out Vector3d r, out Vector3d v)
        {
            double M = MeanAnomalyAt(t);
            if (IsElliptic)
            {
                double E = SolveElliptic(M, e);
                double cosE = Math.Cos(E), sinE = Math.Sin(E);
                double b = a * Math.Sqrt(1 - e * e);
                double Edot = n / (1 - e * cosE);
                r = P * (a * (cosE - e)) + Q * (b * sinE);
                v = P * (-a * sinE * Edot) + Q * (b * cosE * Edot);
            }
            else
            {
                double F = SolveHyperbolic(M, e);
                double coshF = Math.Cosh(F), sinhF = Math.Sinh(F);
                double am = -a;
                double b = am * Math.Sqrt(e * e - 1);
                double Fdot = n / (e * coshF - 1);
                r = P * (am * (e - coshF)) + Q * (b * sinhF);
                v = P * (-am * sinhF * Fdot) + Q * (b * coshF * Fdot);
            }
        }

        public double RadiusAt(double t)
        {
            double M = MeanAnomalyAt(t);
            if (IsElliptic) return a * (1 - e * Math.Cos(SolveElliptic(M, e)));
            return -a * (e * Math.Cosh(SolveHyperbolic(M, e)) - 1);
        }

        public double TimeToPeriapsis(double t)
        {
            double M = MeanAnomalyAt(t);
            if (IsElliptic)
            {
                double m = M % (2 * Math.PI);
                if (m < 0) m += 2 * Math.PI;
                return (2 * Math.PI - m) / n;
            }
            return M < 0 ? -M / n : double.NaN;
        }

        public double TimeToApoapsis(double t)
        {
            if (!IsElliptic) return double.NaN;
            double m = MeanAnomalyAt(t) % (2 * Math.PI);
            if (m < 0) m += 2 * Math.PI;
            double d = Math.PI - m;
            if (d < 0) d += 2 * Math.PI;
            return d / n;
        }

        /// <summary>Points along the path (planet-centered inertial), clipped at maxRadius for escape paths.</summary>
        public List<Vector3d> Sample(int count, double maxRadius)
        {
            var pts = new List<Vector3d>(count + 1);
            if (IsElliptic && Apoapsis <= maxRadius)
            {
                double b = a * Math.Sqrt(1 - e * e);
                for (int i = 0; i <= count; i++)
                {
                    double E = 2 * Math.PI * i / count;
                    pts.Add(P * (a * (Math.Cos(E) - e)) + Q * (b * Math.Sin(E)));
                }
                return pts;
            }
            double c = (p / maxRadius - 1) / e;
            double nuMax = Math.Acos(Math.Max(-1, Math.Min(1, c)));
            if (!IsElliptic) nuMax = Math.Min(nuMax, Math.Acos(-1 / e) - 1e-3);
            for (int i = 0; i <= count; i++)
            {
                double nu = -nuMax + 2 * nuMax * i / count;
                double rr = p / (1 + e * Math.Cos(nu));
                pts.Add(P * (rr * Math.Cos(nu)) + Q * (rr * Math.Sin(nu)));
            }
            return pts;
        }

        /// <summary>Speed at periapsis: the fastest point on the path.</summary>
        public double MaxSpeed => Math.Sqrt(mu * (2 / Periapsis - 1 / a));

        static double WrapPi(double M)
        {
            M %= 2 * Math.PI;
            if (M > Math.PI) M -= 2 * Math.PI;
            else if (M < -Math.PI) M += 2 * Math.PI;
            return M;
        }

        /// <summary>
        /// Points between two times, spaced evenly in eccentric anomaly (dense where the path curves),
        /// each with the time the vessel will be there. Positions are relative to the orbited body.
        /// </summary>
        public List<(Vector3d p, double t)> SamplePath(double t0, double t1, int count)
        {
            var pts = new List<(Vector3d, double)>(count + 1);
            double Ma = MeanAnomalyAt(t0), Mb = MeanAnomalyAt(t1);
            if (IsElliptic)
            {
                double Ea = SolveElliptic(Ma, e) + (Ma - WrapPi(Ma));
                double Eb = SolveElliptic(Mb, e) + (Mb - WrapPi(Mb));
                double b = a * Math.Sqrt(1 - e * e);
                for (int i = 0; i <= count; i++)
                {
                    double E = Ea + (Eb - Ea) * i / count;
                    double M = E - e * Math.Sin(E);
                    pts.Add((P * (a * (Math.Cos(E) - e)) + Q * (b * Math.Sin(E)), epoch + (M - M0) / n));
                }
            }
            else
            {
                double Fa = SolveHyperbolic(Ma, e), Fb = SolveHyperbolic(Mb, e);
                double am = -a, b = am * Math.Sqrt(e * e - 1);
                for (int i = 0; i <= count; i++)
                {
                    double F = Fa + (Fb - Fa) * i / count;
                    double M = e * Math.Sinh(F) - F;
                    pts.Add((P * (am * (e - Math.Cosh(F))) + Q * (b * Math.Sinh(F)), epoch + (M - M0) / n));
                }
            }
            return pts;
        }

        public static double SolveElliptic(double M, double e)
        {
            M %= 2 * Math.PI;
            if (M > Math.PI) M -= 2 * Math.PI;
            else if (M < -Math.PI) M += 2 * Math.PI;
            double E = e < 0.8 ? M : (M >= 0 ? Math.PI : -Math.PI);
            for (int i = 0; i < 60; i++)
            {
                double f = E - e * Math.Sin(E) - M;
                double d = f / (1 - e * Math.Cos(E));
                E -= d;
                if (Math.Abs(d) < 1e-13) break;
            }
            return E;
        }

        public static double SolveHyperbolic(double M, double e)
        {
            double F = Asinh(M / e);
            for (int i = 0; i < 100; i++)
            {
                double f = e * Math.Sinh(F) - F - M;
                double d = f / (e * Math.Cosh(F) - 1);
                F -= d;
                if (Math.Abs(d) < 1e-12) break;
            }
            return F;
        }

        static double Asinh(double x)
        {
            double ax = Math.Abs(x);
            double r = Math.Log(ax + Math.Sqrt(ax * ax + 1));
            return x < 0 ? -r : r;
        }
    }
}
