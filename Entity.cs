using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.ColorSpaces.Conversion;

namespace swtor_ESP
{
    public class Entity
    {
        public Vector3 coords;
        public string baseAddrStr = "";
        public UIntPtr baseAddr = 0x0;
        public float magnitude;
        public float playermagnitude;
        public bool selected = false;
        public Vector2 rectMin = new Vector2();
        public Vector2 rectMax = new Vector2();
        public Vector4 entESPColor = new Vector4();
    }
}
