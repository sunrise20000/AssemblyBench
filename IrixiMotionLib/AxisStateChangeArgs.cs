using AxisParaLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IrixiMotionLib
{
    public class AxisStateChangeArgs : EventArgs
    {
        public byte AxisNo { get; set; }
        public AxisArgs AxisState { get; set; }
    }
}
