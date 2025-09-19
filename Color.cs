using System.Globalization;

namespace Ash3 {
    public readonly struct Color(byte r, byte g, byte b) {
        public readonly byte R = r;
        public readonly byte G = g;
        public readonly byte B = b;

        public string ToHexCode() => $"{R:X2}{G:X2}{B:X2}";
        public int ToInt() => R << 16 | G << 8 | B;
        public override string ToString() => $"Color({R}, {G}, {B})";
        public override bool Equals(object? obj) => obj is Color color && R == color.R && G == color.G && B == color.B;
        public override int GetHashCode() => HashCode.Combine(R, G, B);

        public static Color FromHexCode(string str) {
            if (str[..1] == "#") str = str[1..];
            var x = int.Parse(str, NumberStyles.HexNumber);
            return new((byte) (x >> 16), (byte) (x >> 8), (byte) x);
        }
        public static Color FromInt(int x) => new((byte) (x >> 16), (byte) (x >> 8), (byte) x);
    }
}
