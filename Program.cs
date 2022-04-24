using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;

namespace PythonOrderXml2RevitResultXml
{
    public class MainProgram
    {
        public MainProgram(string[] args)
        {
            Main(args);
        }
        static void Main(string[] args)
        {
            String path_RevitData;
            String path_PythonOrder;
            String savePath_RevitResult;

            if (args.Length < 1) 
            {
                //Console.WriteLine("参数1:RevitData路径,参数2:Python输出的Order路径.");
                Console.WriteLine("参数不够:参数1.RevitOrder(集成RevitData)路径,参数2.输出路径");
                throw new ArgumentException("参数不够:参数1.RevitOrder(集成RevitData)路径,参数2.输出路径");
                //Console.ReadKey();
                //return;
            }
            else
            {
                path_RevitData= path_PythonOrder = args[0];
                savePath_RevitResult = args[1];
            }
            //else 
            //{
            //    path_RevitData = args[0];
            //    path_PythonOrder = args[1];
            //}

            //XDocument document_RevitData = XDocument.Load(@"C:\Users\le\Desktop\Input test 01-20220416A\Input test 01-20220416A.xml");   //原始错误案例输出
            //XDocument document_RevitData = XDocument.Load(@"D:\Autodesk\Revit 2020\export\RevitData-allBend.xml");                       //修改错误案例后输出
            //XDocument document_RevitData = XDocument.Load(@"C:\Users\le\Desktop\Input test 01-20220416A\RevitResult-①.xml");            //完成①后的,接着需完成②
            //XDocument document_PythonOrder = XDocument.Load(@"C:\Users\le\Desktop\Input test 01-20220416A\RevitOrder_allbend.xml");      //路径

            XDocument document_RevitData = XDocument.Load(path_RevitData);
            XDocument document_PythonOrder = XDocument.Load(path_PythonOrder);

            //① 去除RevitData的管道信息
            //1.管道
            //(1)实体: 水管 风管 线水管分身 线风管分身
            //(2)管道连接件及信息 fitting
            //2.线管
            //(1) 线管输入圆圈
            //(2) 线管实体
            //(3) 线管连接信息
            RemovePipeInfomation(document_RevitData);


            //② 添加Order到RevitData中
            //1.添加管道: Entitys-Entity[type:Pipe/Duct] 水管 风管
            //2.添加连接信息 Fittings? Entitys-InputConnector?
            AddPipeInfomation(document_PythonOrder,document_RevitData);


            //③ 保存
            document_RevitData.Save(savePath_RevitResult);
        }

        private static void AddPipeInfomation(XDocument document_PythonOrder,XDocument document_RevitData)
        {
            Console.WriteLine("Entitys下添加Pipe/Duct");
            //② 添加Order到RevitData中
            //1.添加管道: Entitys-Entity[type:Pipe/Duct] 水管 风管
            var RevitData_entitys = document_RevitData.Root.Element("Entitys");
            var pipeBaseXml = document_PythonOrder.Root.Element("PipeBase");
            if (pipeBaseXml == null)
                pipeBaseXml = document_PythonOrder.Root;

            foreach (var pipeGroupXml in pipeBaseXml.Elements())
                foreach (var pipeListXml in pipeGroupXml.Elements())
                    foreach (var pipeXml in pipeListXml.Elements())
                        RevitData_entitys.Add(MakeEntity(pipeXml, pipeListXml, pipeGroupXml.Attribute("Type").Value));

            ////2.添加连接信息 Fittings× Entitys-InputConnector✓
            //foreach (var pipeGroupXml in pipeBaseXml.Elements())
            //    foreach (var pipeListXml in pipeGroupXml.Elements()) 
            //    {
            //        IEnumerable<XElement> currentPipes = pipeListXml.Elements();
            //        int count = currentPipes.Count();
            //        XElement currentPipe = null;
            //        foreach (var pipeListXml in pipeGroupXml.Elements())
            //        { }
            //    }
        }

        private static XElement MakeEntity(XElement pipeXml, XElement pipeListXml, String typeStr)
        {
            //<Entity UniqueId="e2d76d32-4cec-40b4-ad7b-8ebcfd33815a-00731451" type="Pipe">
            //  <FamilyName value="DR Line" />
            //  <SymbolName value="DR Input" />
            //  <Color value="" />
            //  <SystemClassfy value="SWP" />
            //  <SystemType value="SWP" />
            //①<Radius value="150" />
            //②<DuctType value="矩形" Length1="0" Length2="0" />
            //  <LocationEnt type="LineEntity" StartPoint="83354.360753559, 117274.620022954, 52930.000000000" EndPoint="82522.330897938, 117274.620022954, 52930.000000000" />
            //  <HorizonOffset value="中心" />
            //  <VerticalOffset value="中" />
            //</Entity>

            //<Entity UniqueId="e2d76d32-4cec-40b4-ad7b-8ebcfd33815a-00731451" type="Pipe">
            XElement entity = new XElement("Entity");
            entity.SetAttributeValue("UniqueId", pipeXml.Attribute("UniqueId").Value);
            if (typeStr == "Pipe" || typeStr == "LinePipe")
            {
                entity.SetAttributeValue("type", "Pipe");
            }
            else if (typeStr == "Duct" || typeStr == "LineDuct")
            {
                entity.SetAttributeValue("type", "Duct");
            }
            else
                throw new Exception();
            //  <FamilyName value="DR Line" />
            XElement subNode;
            void GetFromPipeListXml(String attrStr)
            {
                subNode = new XElement(attrStr);
                var Value = pipeListXml.Attribute(attrStr);
                subNode.SetAttributeValue("value", Value==null?"": Value.Value);
                entity.Add(subNode);
            };
            GetFromPipeListXml("FamilyName");
            //  <SymbolName value="DR Input" />
            GetFromPipeListXml("SymbolName");
            //  <Color value="" />
            GetFromPipeListXml("Color");
            //  <SystemClassfy value="SWP" />
            GetFromPipeListXml("SystemClassfy");
            //  <SystemType value="SWP" />
            GetFromPipeListXml("SystemType");
            //①<Radius value="150" />
            //②<DuctType value="矩形" Length1="0" Length2="0" />
            if (typeStr == "Pipe" || typeStr == "LinePipe")
            {
                subNode = new XElement("Radius");
                subNode.SetAttributeValue("value", Double.Parse(pipeXml.Attribute("Width").Value)/2);
                entity.Add(subNode);
            }
            else if (typeStr == "Duct" || typeStr == "LineDuct")
            {
                subNode = new XElement("DuctType");
                var DuctTypeValue = pipeListXml.Attribute("DuctType");
                subNode.SetAttributeValue("value", DuctTypeValue == null?"矩形": DuctTypeValue.Value);
                subNode.SetAttributeValue("Length1", pipeXml.Attribute("Width").Value);
                subNode.SetAttributeValue("Length2", pipeXml.Attribute("Height").Value);
                entity.Add(subNode);
            }
            else
                throw new Exception();
            //  <LocationEnt type="LineEntity" StartPoint="83354.360753559, 117274.620022954, 52930.000000000" EndPoint="82522.330897938, 117274.620022954, 52930.000000000" />
            subNode = new XElement("LocationEnt");
            subNode.SetAttributeValue("type", "LineEntity");
            subNode.SetAttributeValue("StartPoint", pipeXml.Attribute("StartPoint").Value);
            subNode.SetAttributeValue("EndPoint", pipeXml.Attribute("EndPoint").Value);
            entity.Add(subNode);
            //  <HorizonOffset value="中心" />
            GetFromPipeListXml("HorizonOffset");
            //  <VerticalOffset value="中" />
            GetFromPipeListXml("VerticalOffset");
            //</Entity>
            return entity;
        }

        //① 去除RevitData的管道信息
        private static void RemovePipeInfomation(XDocument document_RevitData)
        {
            var root = document_RevitData.Root;
            var entitys = root.Element("Entitys");
            //Console.WriteLine("删除Entity");
            ////(2)Entitys-* 管道连接件及信息 
            //entitys.RemoveNodes();

            var fittings = root.Element("Fittings");
            Console.WriteLine("删除FittingEntity");
            //(2)Fitting-* 管道连接件及信息 
            fittings.RemoveNodes();

            List<XElement> deleteList = new List<XElement>();

            Console.WriteLine("删除Entitys下InputConnector");
            //(3) Entitys-InputConnector 线管连接信息
            foreach (XElement entity in entitys.Elements("InputConnector"))
                deleteList.Add(entity);
            foreach (XElement entity in deleteList)
                entity.Remove();
            deleteList.Clear();

            Console.WriteLine("删除Entitys下Pipe/Duct");
            Console.WriteLine("删除Entitys下Point/Line");
            //1.管道
            //(1)Entitys-Entity-type:Pipe/Duct/Pipe[symbol:DR Line]/Duct[symbol:AC Line] 实体: 风管 水管 线风管分身 线水管分身
            //3.线管
            //(1) Entitys-Entity-type:AC Point/DR Input 线管输入圆圈
            //(2) Entitys-Entity-type:DR Line/AC Line 线管实体
            foreach (XElement entity in entitys.Elements("Entity"))
            {
                var tempStr = entity.Attribute("type").Value;
                if (tempStr == "Pipe" || tempStr == "Duct" || tempStr.Contains("Point") || tempStr.Contains("Line"))
                    deleteList.Add(entity);
            }
            foreach (XElement entity in deleteList)
                entity.Remove();
            deleteList.Clear();


        }
    }
}

