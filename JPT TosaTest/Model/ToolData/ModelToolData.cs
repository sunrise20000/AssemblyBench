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

        //数据类型
        public override EnumToolType ToolType { get { return EnumToolType.ModelTool; } set { } }

        public string ModelName { get; set; }
        //拉伸灰度时候需要用到
        public double MinScaleThreshold { get; set; }
        public double MaxScaleThreshold { get; set; }

        //模板类型
        public EnumShapeModelType ModelType { get; set; }

        //查找边界时候需要用到
        public double MinThreshold { get; set; }
        public double MaxThreshold { get; set; }

        //匹配的最低分数
        public double MinScore { get; set; }
        //存储的是模板在原图像当中的位置 Row, Col, Phi
        public string HalconData { get; set; }

        public override string ToString()
        {
            return $"{ToolType.ToString()}|{ModelName}&{MinScaleThreshold}&{MaxScaleThreshold}&{MinThreshold}&{MaxThreshold}&{ModelType.ToString()}&{MinScore}|{HalconData}";
        }
        public override bool FromString(string ParaList)
        {
            string[] list = ParaList.Split('|');
            if (list.Count() == 3)
            {
                Enum.TryParse(list[0], out EnumToolType type);
                HalconData = list[2];
                if (type != EnumToolType.ModelTool)
                {
                    throw new Exception($"Wrong {ToolType.ToString()} when parse {ParaList}, Please check!");
                }
                else
                {
                    var L1 = list[1].Split('&');
                    if (L1.Count() != 7)
                        throw new Exception($"Wrong para num when parse {ParaList}");
                    bool bRet = true;
                    string modelName = L1[0];
                    bRet &= int.TryParse(L1[1], out int minScaleThreshold);
                    bRet &= int.TryParse(L1[2], out int maxScaleThreshold);
                    bRet &= int.TryParse(L1[3], out int minThreshold);
                    bRet &= int.TryParse(L1[4], out int maxThreshold);
                    bRet &= Enum.TryParse(L1[5], out EnumShapeModelType modelType);
                    bRet &= int.TryParse(L1[6], out int minScore);
                    if (bRet == false)
                        throw new Exception("Error happend when parse {ParaList}");
                    else
                    {
                        this.ModelName = modelName;
                        this.MinScaleThreshold = minScaleThreshold;
                        this.MaxScaleThreshold = maxScaleThreshold;
                        this.MinThreshold = minThreshold;
                        this.MaxThreshold = maxThreshold;
                        this.ModelType = modelType;
                        this.MinScore = minScore;
                        return true;
                    }
                }
            }
            else
            {
                throw new Exception($"Wrong {ToolType.ToString()} format when parse {ParaList}");
            }
        }
    }
}
