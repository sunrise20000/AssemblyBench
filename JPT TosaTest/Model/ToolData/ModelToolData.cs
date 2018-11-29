using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JPT_TosaTest.Vision;

namespace JPT_TosaTest.Model.ToolData
{
    public class ModelToolData : ToolDataBase
    {
        public string ModelName { get; set; }
        public double MinThreshold { get; set; }
        public double MaxThreshold { get; set; }
        public int MatchNumber { get; set; }

        public override EnumToolType ToolType { get { return EnumToolType.ModelTool; } set { } }

        public override string ToString()
        {
            
            return base.ToString();
        }
        public override bool FromString(string ParaList)
        {
            throw new NotImplementedException();
        }
    }
}
