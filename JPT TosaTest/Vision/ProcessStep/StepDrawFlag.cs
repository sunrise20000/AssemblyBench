using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPT_TosaTest.Vision.ProcessStep
{
    public class StepDrawFlag : VisionProcessStepBase
    {
        public StepDrawFlag()
        {
            In_IsShowResult = true;
        }

        public Tuple<double, double, double, double> In_VLine { get; set; }
        public Tuple<double, double, double, double> In_HLine { get; set; }
        public bool In_IsAdd { get; set; }


        public EnumGeometryType In_GeometryType { get; set; }
      
        public override bool Process()
        {
           
            HalconVision.Instance.DrawGeometry(In_CamID, In_Image, In_VLine.Item1, In_VLine.Item2, In_VLine.Item3, In_VLine.Item4,
                                        In_HLine.Item1, In_HLine.Item2, In_HLine.Item3, In_HLine.Item4, In_GeometryType, In_IsAdd,In_IsShowResult);
            return true;
        }
    }
}
