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
    }
}
