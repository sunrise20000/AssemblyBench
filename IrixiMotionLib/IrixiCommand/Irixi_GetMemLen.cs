﻿using Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.MotionCards.IrixiCommand
{
    public class Irixi_GetMemLen : ZigBeePackage
    {
        protected override void WriteData()
        {
            writer.Write((byte)Enumcmd.GetMemLength);
        }
        public override ZigBeePackage ByteArrToPackage(byte[] RawData)
        {
            UInt32 DataLengthRecv = 0;
            for (int i = 0; i < 4; i++)
            {
                DataLengthRecv += (UInt32)(RawData[7 + i] << (8 * i));
            }
            this.ReturnObject = DataLengthRecv;
            return base.ByteArrToPackage(RawData);
        }
    }
}
