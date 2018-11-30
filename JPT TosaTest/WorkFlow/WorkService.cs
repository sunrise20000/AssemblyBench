﻿
using JPT_TosaTest.Classes;
using JPT_TosaTest.Config.SoftwareManager;
using JPT_TosaTest.Model.ToolData;
using JPT_TosaTest.Vision;
using JPT_TosaTest.Vision.ProcessStep;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JPT_TosaTest.WorkFlow
{
    public enum STEP : int
    {
        Init,
        CmdGetProductPLC,
        CmdGetProductSupport,
        CmdFindLine,
        DO_NOTHING,



        EMG,
        EXIT,
    }
    public enum EnumProductType
    {
        SUPPORT,
        PLC,
    }
    public enum EnumRTShowType
    {
        Support,
        Tia,
        None,
    }
    public class WorkService : WorkFlowBase
    {
        #region Private
        //start from 0
        private const int AXIS_X = 3, AXIS_Y1 = 1, AXIS_Y2 = 2, AXIS_Z = 0, AXIS_CY = 5, AXIS_CZ = 4, AXIS_R = 6;
        private const int PT_X = 0, PT_Y1 = 1, PT_Y2 = 2, PT_Z = 3, PT_R = 4, PT_CY = 5, PT_CZ = 6;
        private const int VAC_PLC = 0, VAC_HSG = 2, TouchSensor = 6;
        private MotionCards.Motion_IrixiEE0017 MotionCard = null;
        private IOCards.IIO IOCard = null;
        private List<object> TopLines = null;
        private List<object> BottomLines = null;
        private object TiaFlag = null;
        private string File_ToolParaPath = $"{FileHelper.GetCurFilePathString()}VisionData\\ToolData\\";
        private string File_ModelFilePath = $"VisionData\\Model\\";    //Model
       
        private int[] ProductIndex = { 0, 0 };


        List<double> PtCamTop_PLC = null;
        List<double> PtCamBottom_PLC = null;
        List<double> PtLeftTop_PLC = null;
        List<double> PtRightDown_PLC = null;
        List<double> PtDropDown_PLC = null;
        List<double> PtPreFitPos_PLC = null;

        List<double> PTCamTop_Support = null;
        List<double> PtCamBottom_Support = null;
        List<double> PtLeftTop_Support = null;
        List<double> PtRightDown_Support = null;
        List<double> PtDropDown_Support = null;
        List<double> PtPreFitPos_Support = null;

        Task GrabTask = null;
        private object MonitorLock = new object();
        private bool IsPauseMonitor = true;

        private Tuple<double,double,double,double> PartRegion=new Tuple<double, double, double, double>(-1,-1,-1,-1);
        private Tuple<double, double, double, double> PartHsg = new Tuple<double, double, double, double>(-1, -1, -1, -1);
        private Tuple<double, double, double, double> PartTia = new Tuple<double, double, double, double>(-1, -1, -1, -1);

        private UInt16 LowPressure = 1500;
        private UInt16 HightPressure = 2000;
        private EnumRTShowType ShowType = EnumRTShowType.None;
        //Tool
        private StepFindModel Tool_StepFindHsgModel = null;
        private StepFindeLineByModel Tool_StepFindLineBottomByModel = null;
        private StepFindeLineByModel Tool_StepFindLineTopByModel = null;
        private StepShowLineTop Tool_ShowLineTop = null;
        private StepShowLineBottom Tool_ShowLineBottom = null;
        private StepCalibImage Tool_CalibImage = null;

        #endregion

        protected override bool UserInit()
        {
            MotionCard = MotionCards.MotionMgr.Instance.FindMotionCardByCardName("Motion_IrixiEE0017[0]") as MotionCards.Motion_IrixiEE0017;
            IOCard = IOCards.IOCardMgr.Instance.FindIOCardByCardName("IO_IrixiEE0017[0]");
            IOCard.WriteIoOutBit(TouchSensor, true);
            //LowPressure = (UInt16)(Config.ConfigMgr.Instance.ProcessData.Presure);
            //HightPressure = (UInt16)(Config.ConfigMgr.Instance.ProcessData.Presure+1);
            MotionCard.SetCssThreshold(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, LowPressure, HightPressure);
            MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, false);
            SubStep = -1;
            return GetAllPoint() && MotionCard != null && IOCard != null;
        }
        public WorkService(WorkFlowConfig cfg) : base(cfg)
        {

        }

        protected override int WorkFlow()
        {
            try
            {
                ClearAllStep();
                PushStep(STEP.Init);
                while (!cts.IsCancellationRequested)
                {
                    Thread.Sleep(10);
                    Step = PeekStep();
                    if (bPause || Step == null)
                        continue;

                    switch (Step)
                    {
                        case STEP.Init:     //初始化
                            ShowInfo();
                            HomeAll();
                            ClearAllStep();
                            break;
                        case STEP.CmdGetProductSupport: //抓取Support
                            {
                                Int32 Flag = ((Int32)CmdPara >> 16) & 0xFFFF;
                                if ((Flag >> (ProductIndex[0]++) & 0x01) != 0)
                                {
                                    GetProduct(ProductIndex[0], EnumProductType.SUPPORT);
                                    if (ProductIndex[0] >= 6)
                                        ProductIndex[0] = 0;
                                    ClearAllStep();
                                }
                            }
                            break;
                        case STEP.CmdGetProductPLC:    //抓取PLC
                            {
                                Int32 Flag = (Int32)CmdPara & 0xFFFF;
                                if ((Flag >> (ProductIndex[1]++) & 0x01) != 0)
                                {
                                    GetProduct(ProductIndex[1], EnumProductType.PLC);
                                    if (ProductIndex[1] >= 6)
                                        ProductIndex[1] = 0;
                                    ClearAllStep();
                                }
                            }
                            break;

                        case STEP.CmdFindLine:  //FindLine
                            lock (MonitorLock)
                            {
                                FindAndGetModelData();
                                Thread.Sleep(1000);
                            }
                            ClearAllStep();
                            break;

                        case STEP.EXIT:
                            return 0;
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                SubStep = -1;
                return -1;
            }
        }

        #region Private
        private void HomeAll()
        {
            SubStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (SubStep)
                {
                    case 0:
                        MotionCard.Home(AXIS_CZ, 0, 500, 5, 10);
                        MotionCard.Home(AXIS_Z, 0, 500, 20, 50);

                        SubStep = 1;
                        break;
                    case 1:
                        if (MotionCard.IsHomeStop(AXIS_CZ) && MotionCard.IsHomeStop(AXIS_Z))
                        {
                            SubStep = 2;
                        }
                        break;
                    case 2:
                        MotionCard.Home(AXIS_X, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_Y2, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_Y1, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_CY, 0, 500, 5, 10);
                        SubStep = 3;
                        break;
                    case 3:
                        if (MotionCard.IsHomeStop(AXIS_X) && MotionCard.IsHomeStop(AXIS_Y1) && MotionCard.IsHomeStop(AXIS_Y2) && MotionCard.IsHomeStop(AXIS_CY))
                        {
                            SubStep = 4;
                        }
                        break;
                    case 4:
                        ShowInfo("HomeOk");
                        return;
                    default:
                        return;

                }
            }
        }

        private void GetProduct(int Index, EnumProductType ProductType)
        {
            if (!GetAllPoint())
            {
                ShowError("获取点位出现错误");
                return;
            }
            if (Index < 1 || Index > 6)
                return;

            double DeltaX = 0.0f, DeltaY = 0.0f;  //DeltaX
            double TargetX = 0; //每次的吸取位置X
            double TargetY1 = 0;    //每次的吸取位置Y
            List<double> PtLeftTop = null;
            List<double> PtRightDown = null;
            List<double> PtDropDown = null;


            if (ProductType == EnumProductType.PLC)
            {
                PtLeftTop = PtLeftTop_PLC;
                PtRightDown = PtRightDown_PLC;
                PtDropDown = PtDropDown_PLC;
                //2行3列
                DeltaX = (PtRightDown[PT_X] - PtLeftTop[PT_X]) / 2;
                DeltaY = (PtRightDown[PT_Y1] - PtLeftTop[PT_Y1]) / 1;

                int Row = (Index - 1) / 3 + 1;
                int Col = (Index - 1) % 3 + 1;

                TargetX = PtLeftTop[PT_X] + DeltaX * (Col - 1);
                TargetY1 = PtLeftTop[PT_Y1] + DeltaY * (Row - 1);
            }
            else  //Support
            {
                PtLeftTop = PtLeftTop_Support;
                PtRightDown = PtRightDown_Support;
                PtDropDown = PtDropDown_Support;
                //3行2列
                DeltaX = (PtRightDown[PT_X] - PtLeftTop[PT_X]) / 1;
                DeltaY = (PtRightDown[PT_Y1] - PtLeftTop[PT_Y1]) / 2;
                int Row = (Index - 1) / 2 + 1;
                int Col = (Index - 1) % 2 + 1;
                TargetX = PtLeftTop[PT_X] + DeltaX * (Col - 1);
                TargetY1 = PtLeftTop[PT_Y1] + DeltaY * (Row - 1);
            }

            SubStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (SubStep)
                {
                    case 0: //先将Z升起防止干涉
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, 0);
                        IOCard.WriteIoOutBit(VAC_PLC, false);
                        SubStep = 1;
                        break;
                    case 1: //移动PT_X到取料点
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X, 500, 100, TargetX);
                            SubStep = 2;
                        }
                        break;
                    case 2: // PT_Y1到取料点
                        if (MotionCard.IsNormalStop(AXIS_X))
                        {
                            MotionCard.MoveAbs(AXIS_Y1, 500, 100, TargetY1);
                            SubStep = 3;
                        }
                        break;
                    case 3:
                        if (MotionCard.IsNormalStop(AXIS_Y1))
                        {
                            SubStep = 4;
                        }
                        break;
                    case 4: //下降PT_Z轴//粗略下降
                        MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, true);
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, PtLeftTop[PT_Z]-1);
                        SubStep = 5;
                        break;
                    case 5:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, false);
                            SubStep = 6;
                        }
                        break;
                    case 6: //下降PT_Z轴//，慢慢下降
                        MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, true);
                        MotionCard.MoveAbs(AXIS_Z, 500, 1, PtLeftTop[PT_Z]);
                        SubStep = 7;
                        break;
                    case 7:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, false);
                            IOCard.WriteIoOutBit(VAC_PLC, true);
                            SubStep = 8;
                        }
                        break;
                  
                    case 8: //PT_Z轴抬起
                        Thread.Sleep(500);
                        ShowInfo("将Z轴抬起");
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, 0);
                        SubStep = 9;
                        break;
                    case 9:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            ShowInfo("移动X轴到放置位");
                            MotionCard.MoveAbs(AXIS_X, 500, 100, PtDropDown[PT_X]);
                            SubStep = 10;
                        }
                        break;
                    case 10: //移动PT_Y2过去
                        if (MotionCard.IsNormalStop(AXIS_X))
                        {
                            MotionCard.MoveAbs(AXIS_Y2, 500, 100, PtDropDown[PT_Y2]);
                            SubStep = 11;
                        }
                        break;
                    case 11: //移动到放置点的PT_X位置并下降PT_Z到HSG表面，不要下去
                        if (MotionCard.IsNormalStop(AXIS_Y2))
                        {
                            MotionCard.MoveAbs(AXIS_Z, 500, 100, PtDropDown[PT_Z]);
                            SubStep = 12;
                        }
                        break;
                    
                    case 12: //
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            if (ProductType == EnumProductType.PLC)
                            {
                                PopAndPushStep(STEP.CmdGetProductPLC);
                                Work_PLC();
                            }
                            else
                            {
                                PopAndPushStep(STEP.CmdGetProductSupport);
                                Work_Support();
                            }
                            SubStep = 13;
                        }
                        break;
                    case 13:    //等待调整位置
                        //ShowInfo("取料完毕");
                        return;
                    default:
                        ShowInfo("未知步骤");
                        return;
                }

            }

        }
        /// <summary>
        /// 找模板
        /// </summary>
        private void FindAndGetModelData()
        {
            try
            {
                SubStep = 0;
                ShowInfo("正在标记......");
                while (!cts.IsCancellationRequested)
                {
                    switch (SubStep)
                    {
                        case 0: //移动CY
                            IOCard.WriteIoOutBit(VAC_HSG, true);
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamBottom_Support[PT_CY]);
                            SubStep = 1;
                            break;
                        case 1: //CZ到下表面
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                                SubStep = 2;

                            }
                            break;

                        case 2: //开始寻找模板
                            if (MotionCard.IsNormalStop(AXIS_CZ))
                            {
                                Thread.Sleep(200);
                                lock (MonitorLock)
                                {
                                    HalconVision.Instance.GrabImage(0);
                                    HalconVision.Instance.GrabImage(0);
                                }

                                //找Hsg
                                string ModelFulllPathFileName = $"{File_ModelFilePath}{Config.ConfigMgr.Instance.ProcessData.HsgModelName}.shm";
                                Tool_StepFindHsgModel = new StepFindModel()
                                {
                                    In_CamID = 0,
                                    In_ModelNameFullPath = ModelFulllPathFileName,
                                    In_MinScaleThreshold=40,
                                    In_MaxScaleThreshold=80
                                };
                                HalconVision.Instance.ProcessImage(Tool_StepFindHsgModel);
                                PartHsg = new Tuple<double, double, double, double>(Tool_StepFindHsgModel.Out_ModelRow.D-238, 
                                                                                    Tool_StepFindHsgModel.Out_ModelCol.D-2038,
                                                                                    Tool_StepFindHsgModel.Out_ModelRow.D +119,
                                                                                    Tool_StepFindHsgModel.Out_ModelCol.D +580);
                                SubStep = 3;
                            }
                            break;
                        case 3: //模板找到以后开始找上表面线
                            {
                                MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamTop_PLC[PT_CZ]);
                                SubStep = 4;
                            }
                            break;
                        case 4:
                            if (MotionCard.IsNormalStop(AXIS_CZ))
                            {
                                {
                                    MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamTop_PLC[PT_CY]);
                                    SubStep = 5;
                                }
                            }
                            break;

                        case 5: //升到上表面寻找上表面的线
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                Thread.Sleep(300);
                                lock (MonitorLock)
                                {
                                    HalconVision.Instance.GrabImage(0);
                                    FindLineTop();
                                }
                                SubStep = 6;
                            }
                            break;
                        case 6: //移动下来找下表面的线
                            //Thread.Sleep(100);
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamBottom_Support[PT_CY]);
                            SubStep = 7;
                            break;
                        case 7:
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                                SubStep = 8;
                            }
                            break;
                        case 8:
                            {
                                if (MotionCard.IsNormalStop(AXIS_CZ))
                                {
                                    Thread.Sleep(200);
                                    lock (MonitorLock)
                                        HalconVision.Instance.GrabImage(0);
                                    SubStep = 9;
                                }
                            }
                            break;
                        case 9:
                            FindLineBottom();
                            SubStep = 10;
                            break;
                        case 10: // 完毕,等待工作
                            ShowInfo("标记完毕");
                            StartMonitor(0, EnumRTShowType.Support, true);
                            return;
                        default:
                            return;
                    }
                }
                ShowInfo("标记被终止");
            }
            catch(Exception ex)
            {
                ShowInfo("找模板时候发生错误");
            }
        }
        //贴PLC
        private void Work_PLC()
        {
            try
            {
                ShowInfo("正在贴PLC......");
                SubStep = 0;
                while (!cts.IsCancellationRequested)
                {
                    switch (SubStep)
                    {


                        case 0: //移动CY到工作平面
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtDropDown_PLC[PT_CY]);
                            SubStep = 1;
                            break;
                        case 1: //移动CZ到工作平面
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                                SubStep = 2;
                            }
                            break;

                        case 2:
                            if (MotionCard.IsNormalStop(AXIS_CZ))
                            {
                                SubStep = 3;
                            }
                            break;
                        //去找Tia的Model和基准线，并画出region
                        case 3:
                            lock (MonitorLock)
                            {
                                ShowInfo("寻找Tia标记");
                                if (TiaFlag == null)
                                {
                                    Thread.Sleep(200);
                                    lock (MonitorLock)
                                    {
                                        HalconVision.Instance.GrabImage(0);
                                        FindLineTia0();
                                        Thread.Sleep(1000);
                                    }
                                }
                                else
                                    HalconVision.Instance.ShowRoi(0, TiaFlag);
                            }
                            StartMonitor(0, EnumRTShowType.Tia, true);
                            SubStep = 5;
                            break;

                        case 5:
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                Thread.Sleep(200);
                                ShowInfo("(1/3)请调整PLC的位置，完毕后点击【PLC按键】");
                                SubStep = 6;
                            }
                            break;
                        case 6: //开始拍照并显示
                            if (GetCurStepCount() == 0)   //要自动下降贴合PLC
                            {
                                ShowInfo("(2/3)请先调整PLC的位置，完毕后点击【PLC按键】自动贴合");
                                PushStep(STEP.CmdGetProductPLC);
                               
                                MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, true);
                                MotionCard.MoveAbs(AXIS_Z, 500, 100, PtPreFitPos_PLC[PT_Z]);
                                SubStep = 7;
                            }
                            break;
                        case 7: //将相机移动到下表面 
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                            SubStep = 8;
                            break;
                        case 8: //保持CY在DropDown处
                            if (MotionCard.IsNormalStop(AXIS_CZ))
                            {
                                MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtDropDown_PLC[PT_CY]);
                                SubStep = 9;
                            }
                            break;

                        case 9:
                            if (MotionCard.IsNormalStop(AXIS_Z))
                            {
                                if (GetCurStepCount() == 0)   //要自动下降贴合PLC,直到Sensor停止
                                {
                                    ShowInfo("(3/3)正在完成PLC贴合......");
                                    MotionCard.MoveAbs(AXIS_Z, 500, 1, PtPreFitPos_PLC[PT_Z] + 3);
                                    SubStep = 10;
                                }
                            }
                            break;
                        case 10: // 完毕,等待工作完毕
                            if (MotionCard.IsNormalStop(AXIS_Z))
                            {
                                MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, false);
                                IOCard.WriteIoOutBit(VAC_HSG, false);
                                BackToTempPos();
                                ShowInfo("PLC贴合完毕");
                                return;
                            }
                            break;
                        default:
                            return;
                    }
                }
                ShowInfo("PLC贴合被终止");
            }
            catch
            {
                ShowInfo("贴合PLC的时候发生错误");
            }
        }
        //贴Support
        private void Work_Support()
        {
            try
            {
                SubStep = 0;
                ShowInfo("正在贴合Support......");
                while (!cts.IsCancellationRequested)
                {
                    switch (SubStep)
                    {

                        case 0: //移动CY到工作平面
                            StartMonitor(0, EnumRTShowType.Support, true, PartHsg);
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtDropDown_Support[PT_CY]);
                            SubStep = 1;
                            break;
                        case 1: //移动CZ到工作平面
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtDropDown_Support[PT_CZ]);
                                SubStep = 2;
                            }
                            break; 
                        case 2: //开始拍照
                            if (MotionCard.IsNormalStop(AXIS_CZ))
                            {
                                ShowInfo("(1/3)请调整Support的位置，完毕后点击【Support按键】");
                                SubStep = 3;
                            }
                            break;
                        case 3: //ShowLineBottom 
                            if (GetCurStepCount() == 0)    //调整完毕，要自动下降到预贴合位置，同时打开下压检测使能
                            {
                                ShowInfo("(2/3)请再次调整Support的位置，然后点击【Support按键】完成自动贴合");
                                PushStep(STEP.CmdGetProductSupport);
                               
                                MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, true);
                                MotionCard.MoveAbs(AXIS_Z, 500, 20, PtPreFitPos_Support[PT_Z]);
                                Thread.Sleep(200);
                                TiaFlag = null; //清空上次的Tia线条
                                SubStep = 4;
                            }
                            break;

                        case 4: //移动CY
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, PtCamBottom_Support[PT_CY]);
                            SubStep = 5;
                            break;
                        case 5: //CZ到下表面
                            if (MotionCard.IsNormalStop(AXIS_CY))
                            {
                                MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PtCamBottom_Support[PT_CZ]);
                                SubStep = 6;
                            }
                            break;
                        
                        case 6: //等待最后确认
                            if (MotionCard.IsNormalStop(AXIS_Z))
                            {
                                if (GetCurStepCount() == 0)    //要自动下降到贴合位置
                                {
                                    ShowInfo("(3/3)正在贴合Support，请稍后......");
                                    MotionCard.MoveAbs(AXIS_Z, 500, 1, PtPreFitPos_Support[PT_Z] + 3);
                                    SubStep = 7;
                                }
                            }
                            break;
                        case 7: //下压完成
                            if (MotionCard.IsNormalStop(AXIS_Z))
                            {
                                ShowInfo("贴合完成，返回临时停靠位置");
                                MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, false);
                                StartMonitor(0, EnumRTShowType.None,true);
                                BackToTempPos();
                                SubStep = 11;
                            }

                            break;


                        case 11: // 完毕,等待工作完毕
                            ShowInfo("Support完毕");
                            return;
                        default:
                            return;
                    }
                }
                ShowInfo("Support贴合被终止");
            }
            catch (Exception ex)
            {
                ShowInfo($"Error:{ex.Message}".Replace("HALCON error",""));
            }
        }
        #endregion    
        private void BackToTempPos()
        {
            SubStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (SubStep)
                {
                    case 0: //Z轴抬起
                        MotionCard.SetCssEnable(MotionCards.IrixiCommand.EnumCssChannel.CSSCH1, false);
                        IOCard.WriteIoOutBit(VAC_PLC, false);
                        Thread.Sleep(5000);
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, 0);
                        SubStep = 1;
                        break;
                    case 1: //X轴回到120的固定位置
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X, 500, 100, 120);
                            SubStep = 2;
                        }
                        break;
                    case 2:
                        if (MotionCard.IsNormalStop(AXIS_X))
                            return;
                        break;
                }
            }
        }

        /// <summary>
        /// 找Bottom的基线
        /// </summary>
        /// <returns></returns>
        private bool FindLineBottom()
        {
            string ModelName = Config.ConfigMgr.Instance.ProcessData.HsgModelName;
            List<string> LineList = new List<string>();
            var fileList = FileHelper.GetProfileList(File_ToolParaPath);
            //既包含Pair也包含Line
            foreach (var file in fileList)
            {
                if (file.Contains("Bottom"))
                {
                    string text = File.ReadAllText($"{File_ToolParaPath}{file}.para");
                    LineList.Add(text);
                }
            }
            Tool_StepFindLineBottomByModel = new StepFindeLineByModel()
            {
                In_CamID = 0,
                In_ModelRow = Tool_StepFindHsgModel.Out_ModelRow,
                In_ModelCOl = Tool_StepFindHsgModel.Out_ModelCol,
                In_ModelPhi = Tool_StepFindHsgModel.Out_ModelPhi,
                In_Hom_mat2D = Tool_StepFindHsgModel.Out_Hom_mat2D,
                In_LineRoiPara = LineList,
                In_MinScaleThreshold=40,
                In_MaxScaleThreshold=80
            };
            HalconVision.Instance.ProcessImage(Tool_StepFindLineBottomByModel);
            BottomLines = new List<object>();
            foreach (var it in Tool_StepFindLineBottomByModel.Out_Lines)
            {
                BottomLines.Add(it);
            }

            return true;
        }

        private bool ShowLineBottom()
        {
            //显示最终计算的线
            var LineListForCalc = new List<Tuple<double, double, double, double>>();
            foreach (var it in Tool_StepFindLineBottomByModel.Out_Lines)
                LineListForCalc.Add(new Tuple<double, double, double, double>(it.Item1.D, it.Item2.D, it.Item3.D, it.Item4.D));
            Tool_ShowLineBottom = new StepShowLineBottom()
            {
                In_CamID = 0,
                In_PixGainFactor = Tool_CalibImage.Out_PixGainFactor,
                In_Lines = LineListForCalc
            };

            HalconVision.Instance.ProcessImage(Tool_ShowLineBottom);//会自动显示
            return true;
        }

        /// <summary>
        /// 找Top的基线
        /// </summary>
        /// <returns></returns>
        private bool FindLineTop()
        {
            string ModelName = Config.ConfigMgr.Instance.ProcessData.HsgModelName;
            List<string> LineList = new List<string>();
            var fileList = FileHelper.GetProfileList(File_ToolParaPath);
            foreach (var file in fileList)
            {
                if (file.Contains("Top"))
                {
                    string text = File.ReadAllText($"{File_ToolParaPath}{file}.para");
                    LineList.Add(text);
                }
            }

            Tool_StepFindLineTopByModel = new StepFindeLineByModel()
            {
                In_CamID = 0,
                In_LineRoiPara = LineList,
                In_Hom_mat2D = Tool_StepFindHsgModel.Out_Hom_mat2D,
                In_ModelRow = Tool_StepFindHsgModel.Out_ModelRow,
                In_ModelCOl = Tool_StepFindHsgModel.Out_ModelCol,
                In_ModelPhi = Tool_StepFindHsgModel.Out_ModelPhi,
                In_MinScaleThreshold=40,
                In_MaxScaleThreshold=80
            };
            HalconVision.Instance.ProcessImage(Tool_StepFindLineTopByModel);


            //找线
            var LinesForCalib = new List<Tuple<double, double, double, double>>();
            TopLines = new List<object>();
            foreach (var it in Tool_StepFindLineTopByModel.Out_Lines)
            {
                TopLines.Add(it);
                LinesForCalib.Add(new Tuple<double, double, double, double>(it.Item1.D, it.Item2.D, it.Item3.D, it.Item4.D));
            }
            Tool_CalibImage = new StepCalibImage()
            {
                In_CamID = 0,
                In_RealDistance = 6600,
                In_Line1 = LinesForCalib[0],
                In_Line2 = LinesForCalib[1]
            };
            //顺便将图像尺寸标定了
            HalconVision.Instance.ProcessImage(Tool_CalibImage);

            //显示最终的线
            Tool_ShowLineTop = new StepShowLineTop()
            {
                In_CamID = 0,
                In_PixGainFactor = Tool_CalibImage.Out_PixGainFactor,
                In_Line1 = LinesForCalib[0],
                In_Line2 = LinesForCalib[1]
            };
            HalconVision.Instance.ProcessImage(Tool_ShowLineTop);
            TopLines.Add(Tool_ShowLineTop.Out_Line);


            return true;
        }

        /// <summary>
        /// 找Tia的基准线
        /// </summary>
        /// <param name="ModelName">只需要名称，不需要路径</param>
        /// <param name="lineList"></param>
        /// <returns></returns>
        private bool FindLineTia()
        {
            //找Model
            string ModelFulllPathFileName = $"{File_ModelFilePath}{Config.ConfigMgr.Instance.ProcessData.TiaModelName}.shm";
            StepFindModel FindTiaModelStep = new StepFindModel()
            {
                In_CamID = 0,
                In_ModelNameFullPath = ModelFulllPathFileName,
                In_MinScaleThreshold=40,
                In_MaxScaleThreshold=80
            };
            HalconVision.Instance.ProcessImage(FindTiaModelStep);

            //准备Tia的FlagData,读取L1与L2
            List<string> LineList = new List<string>();
            FlagToolDaga FlagData = new FlagToolDaga();
            FlagData.FromString(File.ReadAllText(File_ToolParaPath + "Flag.para"));
            LineList.Add(File.ReadAllText(File_ToolParaPath + FlagData.L1Name + ".para"));
            LineList.Add(File.ReadAllText(File_ToolParaPath + FlagData.L2Name + ".para"));

            //找线
            StepFindeLineByModel FindTiaLine = new StepFindeLineByModel()
            {
                In_CamID = 0,
                In_Hom_mat2D = FindTiaModelStep.Out_Hom_mat2D,
                In_ModelRow = FindTiaModelStep.Out_ModelRow,
                In_ModelCOl = FindTiaModelStep.Out_ModelCol,
                In_ModelPhi = FindTiaModelStep.Out_ModelPhi,
                In_LineRoiPara = LineList
            };
            HalconVision.Instance.ProcessImage(FindTiaLine);

            //显示region
            //定义两条直线
            string RegionFullPathFileName = $"{File_ToolParaPath}Flag.reg";
            List<Tuple<double, double, double, double>> TiaLineList = new List<Tuple<double, double, double, double>>();
            foreach (var it in FindTiaLine.Out_Lines)
                TiaLineList.Add(new Tuple<double, double, double, double>(it.Item1.D, it.Item2.D, it.Item3.D, it.Item4.D));

            StepShowFlag ShowFlagStep = new StepShowFlag()
            {
                In_CamID = 0,
                In_CenterRow = FlagData.Halcon_Row,
                In_CenterCol = FlagData.Halcon_Col,
                In_Phi = FlagData.Halcon_Phi,
                In_HLine = TiaLineList[0],
                In_VLine = TiaLineList[1],
                In_RegionFullPathFileName = RegionFullPathFileName
            };
            HalconVision.Instance.ProcessImage(ShowFlagStep);

            TiaFlag = ShowFlagStep.Out_Region;
            return true;
        }
        private bool FindLineTia0()
        {
            //找Model
            string ModelFulllPathFileName = $"{File_ModelFilePath}{Config.ConfigMgr.Instance.ProcessData.TiaModelName}.shm";
            StepFindModel FindTiaModelStep = new StepFindModel()
            {
                In_CamID = 0,
                In_ModelNameFullPath = ModelFulllPathFileName,
                In_MinScaleThreshold=0,
                In_MaxScaleThreshold=255
            };
            HalconVision.Instance.ProcessImage(FindTiaModelStep);

            StepFindTia0 FindTia0Tool = new StepFindTia0()
            {
                In_CamID = 0,
                In_ModelRow = FindTiaModelStep.Out_ModelRow,
                In_ModelCol = FindTiaModelStep.Out_ModelCol,
                In_ModelPhi = FindTiaModelStep.Out_ModelPhi

            };
            HalconVision.Instance.ProcessImage(FindTia0Tool);

            TiaFlag = FindTia0Tool.Out_Region;
            return true;
        }
        private bool GetAllPoint()
        {
            PTCamTop_Support = WorkFlowMgr.Instance.GetPoint("Pad相机顶部位置");
            PtCamBottom_Support = WorkFlowMgr.Instance.GetPoint("Pad相机底部位置");
            PtCamTop_PLC = WorkFlowMgr.Instance.GetPoint("Lens相机顶部位置");
            PtCamBottom_PLC = WorkFlowMgr.Instance.GetPoint("Lens相机底部位置");

            PtLeftTop_PLC = WorkFlowMgr.Instance.GetPoint("PLC左上吸取点");
            PtRightDown_PLC = WorkFlowMgr.Instance.GetPoint("PLC右下吸取点");
            PtDropDown_PLC = WorkFlowMgr.Instance.GetPoint("PLC放置点");

            PtLeftTop_Support = WorkFlowMgr.Instance.GetPoint("Support左上吸取点");
            PtRightDown_Support = WorkFlowMgr.Instance.GetPoint("Support右下吸取点");
            PtDropDown_Support = WorkFlowMgr.Instance.GetPoint("Support放置点");

            PtPreFitPos_Support = WorkFlowMgr.Instance.GetPoint("Support预贴合位置");
            PtPreFitPos_PLC = WorkFlowMgr.Instance.GetPoint("PLC预贴合位置");

            return PTCamTop_Support != null &&
                    PtCamBottom_Support != null &&
                    PtCamTop_PLC != null &&
                    PtCamBottom_PLC != null &&
                    PtLeftTop_PLC != null &&
                    PtRightDown_PLC != null &&
                    PtDropDown_PLC != null &&
                    PtLeftTop_Support != null &&
                    PtRightDown_Support != null &&
                    PtDropDown_Support != null &&
                    PtPreFitPos_Support != null &&
                    PtRightDown_PLC != null;
        }

        private void StartMonitor(int nCamID, EnumRTShowType type, bool IsStart, Tuple<double,double,double,double> Part=null)
        {
            IsPauseMonitor = !IsStart;
            ShowType = type;
            if(Part!=null)
                PartRegion = Part;
            if (GrabTask == null || GrabTask.IsCanceled || GrabTask.IsCompleted)
            {
                GrabTask = new Task(() =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        lock (MonitorLock)
                        {
                            if (!IsPauseMonitor)
                            {
                                
                                switch (ShowType)
                                {
                                    case EnumRTShowType.Support:
                                        HalconVision.Instance.SetRefreshWindow(nCamID, false);
                                        HalconVision.Instance.GrabImage(nCamID, true, true, PartRegion.Item1, PartRegion.Item2, PartRegion.Item3, PartRegion.Item4);
                                        ShowLineBottom();
                                        HalconVision.Instance.SetRefreshWindow(nCamID, true);
                                        break;
                                    case EnumRTShowType.Tia:
                                        HalconVision.Instance.SetRefreshWindow(nCamID, false);
                                        HalconVision.Instance.GrabImage(nCamID, true, true, PartRegion.Item1, PartRegion.Item2, PartRegion.Item3, PartRegion.Item4);
                                        HalconVision.Instance.ShowRoi(nCamID, TiaFlag);
                                        HalconVision.Instance.SetRefreshWindow(nCamID, true);
                                        break;
                                    case EnumRTShowType.None:
                                        HalconVision.Instance.GrabImage(nCamID, true, true, PartRegion.Item1, PartRegion.Item2, PartRegion.Item3, PartRegion.Item4);
                                        break;

                                }
                                HalconVision.Instance.ShowRoi(nCamID, null);

                            }
                            else
                                Thread.Sleep(10);

                        }
                    }
                }, cts.Token);
                GrabTask.Start();
            }
        }
    }
}
