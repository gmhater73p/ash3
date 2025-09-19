using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ash3.Cargo {
    internal struct Vector3 {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public Vector3(int x, int y, int z) {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, int b) => new(a.X * b, a.Y * b, a.Z * b);
        public static Vector3 operator /(Vector3 a, int b) => new(a.X / b, a.Y / b, a.Z / b);

        public static int Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector3 Normalized => this / (int) Magnitude;

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
