using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace YawGLAPI {
    public class YawColor : INotifyPropertyChanged {
        public static YawColor BLACK = new YawColor(0, 0, 0);
        public static YawColor WHITE = new YawColor(0, 0, 0);
        public static YawColor GRAY = new YawColor(80, 80, 80);
        private byte r, g, b;

        public byte R {  get { return r; } set { r = value; OnPropertyChanged(String.Empty); } }
        public byte G {  get { return g; } set { g = value; OnPropertyChanged(String.Empty); } }
        public byte B {  get { return b; } set { b = value; OnPropertyChanged(String.Empty); } }


        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public YawColor(byte r, byte g, byte b) {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public override bool Equals(object obj) {
            return obj is YawColor color &&
                   r == color.r &&
                   g == color.g &&
                   b == color.b;
        }

        /// <summary>
        ///   <para>Linearly interpolates between colors a and b by t.</para>
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="t"></param>
        /// 
       
        public static YawColor Lerp(YawColor a, YawColor b, float t) {
            YawColor tmpColor = new YawColor(0, 0, 0);
            t = Math.Abs(t);
            tmpColor.R = (byte)(a.R + (b.R - a.R) * t);
            tmpColor.G = (byte)(a.G + (b.G - a.G) * t);
            tmpColor.B = (byte)(a.B + (b.B - a.B) * t);

            return tmpColor;
        }
        static Random randomGenerator = new Random();


        public static YawColor RandomColor() {
            byte[] randBytes = new byte[3];
            randomGenerator.NextBytes(randBytes);

            byte maxValue = randBytes.Max();
      

            float scaleFactor = 255 / maxValue;
            randBytes[0] = (byte)(randBytes[0] * scaleFactor);
            randBytes[1] = (byte)(randBytes[1] * scaleFactor);
            randBytes[2] = (byte)(randBytes[2] * scaleFactor);
           
            return new YawColor(randBytes[0],randBytes[1],randBytes[2]);
        }
      
        public override int GetHashCode() {
            var hashCode = -839137856;
            hashCode = hashCode * -1521134295 + r.GetHashCode();
            hashCode = hashCode * -1521134295 + g.GetHashCode();
            hashCode = hashCode * -1521134295 + b.GetHashCode();
            return hashCode;
        }
    }
}
