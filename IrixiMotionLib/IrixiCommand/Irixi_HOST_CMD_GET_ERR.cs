﻿using Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.MotionCards.IrixiCommand
{
    public class Irixi_HOST_CMD_GET_ERR : ZigBeePackage
    {
        protected override void WriteData()
        {
            writer.Write((byte)Enumcmd.HOST_CMD_GET_ERR);
        }
        public override ZigBeePackage ByteArrToPackage(byte[] RawData)
        {
            ReturnObject = new Tuple<byte, byte>(RawData[7], RawData[8]);
            return base.ByteArrToPackage(RawData);
        }
    }
}
