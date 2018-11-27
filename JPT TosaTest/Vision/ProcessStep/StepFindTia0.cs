using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.Vision.ProcessStep
{
    public class StepFindTia0 : VisionProcessStepBase
    {
        public Tuple<double, double, double, double,double> In_Rectangle2 { get; set; }
        public double In_ModelRow { get; set; }
        public double In_ModelCol { get; set; }
        public double In_ModelPhi { get; set; }

        public HObject Out_Region { get; set; }
        public override bool Process()
        {
            HalconVision.Instance.FindTia0(In_Image, In_CamID, In_ModelRow, In_ModelCol, In_ModelPhi, out HObject RegionOut);
            Out_Region = RegionOut;

            return true;
        }
    }
}
