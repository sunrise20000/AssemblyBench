﻿using Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.MotionCards.IrixiCommand
{
    public class Irixi_HOST_CMD_SET_T_OUT : ZigBeePackage
    {
        protected override void WriteData()
        {
            writer.Write((byte)Enumcmd.HOST_CMD_SET_T_OUT);
        }
        public override ZigBeePackage ByteArrToPackage(byte[] RawData)
        {
            return base.ByteArrToPackage(RawData);
        }
    }
}
