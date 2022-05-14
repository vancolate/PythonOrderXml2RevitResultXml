using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Text;

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
                path_RevitData = path_PythonOrder = args[0];
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


            //〇 读取PipeBase到对象中
            PipeBase pipeBase =ConstructPipeBaseInstance(document_PythonOrder);

            //① 去除RevitData的管道信息
            //1.管道
            //(1)实体: 水管 风管 线水管分身 线风管分身
            //2.线管
            //(1) 线管输入圆圈
            //(2) 线管实体
            //(3) 线管连接信息
            RemovePipeInfomation(document_RevitData);

            //② 添加Order到RevitData中
            //1.添加管道: Entitys-Entity[type:Pipe/Duct] 水管 风管
            AddPipeInfomation(pipeBase, document_RevitData);

            //③ 删除连接件关系(涉及到新增管道的列)
            //(2)管道连接件及信息 fitting
            //只要连接件的连接点存在uid找不到的就删除
            RemoveFittingInfomation(document_RevitData);

            //④ 添加连接件关系
            //1.管道(新增列)
            //(1)添加列内
            //(2)添加列间
            //2.线管(全部)
            //(1)添加列内
            //(2)添加列间
            AddFittingInfomation(pipeBase, document_RevitData);

            //⑤ 保存
            document_RevitData.Save(savePath_RevitResult);
        }

        private static PipeBase ConstructPipeBaseInstance(XDocument document_PythonOrder)
        {
            var pipeBaseXml = document_PythonOrder.Root.Element("PipeBase");
            if (pipeBaseXml == null)
                pipeBaseXml = document_PythonOrder.Root;

            PipeBase pipeBase = new PipeBase();
            foreach (var pipeGroupXml in pipeBaseXml.Elements())
            {
                PipeGroup pipeGroup = new PipeGroup()
                {
                    PipeGroupNo = pipeGroupXml.Attribute("PipeGroupNo").Value,
                    Type = pipeGroupXml.Attribute("Type").Value,
                };
                pipeBase._pipeGroups.Add(pipeGroup);
                foreach (var pipeListXml in pipeGroupXml.Elements())
                {
                    PipeList pipeList = new PipeList()
                    {
                        PipeListNo = pipeListXml.Attribute("PipeListNo").Value,
                        PrevListNo = pipeListXml.Attribute("PrevListNo").Value,

                        FamilyName = pipeListXml.Attribute("FamilyName").Value,
                        SymbolName = pipeListXml.Attribute("SymbolName").Value,
                        Color = pipeListXml.Attribute("Color").Value,
                        SystemClassfy = pipeListXml.Attribute("SystemClassfy").Value,
                        SystemType = pipeListXml.Attribute("SystemType").Value,
                        HorizonOffset = pipeListXml.Attribute("HorizonOffset").Value,
                        VerticalOffset = pipeListXml.Attribute("VerticalOffset").Value,

                        DuctType = pipeListXml.Attribute("DuctType")?.Value,
                    };
                    pipeGroup._pipeLists.Add(pipeList);

                    foreach (var pipeXml in pipeListXml.Elements())
                    {
                        Pipe pipe = new Pipe()
                        {
                            UniqueId = pipeXml.Attribute("UniqueId").Value,
                            Width = pipeXml.Attribute("Width").Value,
                            Height = pipeXml.Attribute("Height").Value,
                            StartPoint = pipeXml.Attribute("StartPoint").Value,
                            EndPoint = pipeXml.Attribute("EndPoint").Value,
                        };
                        pipeList._pipes.Add(pipe);
                    }
                }
            }

            return pipeBase;
        }

        private static void AddFittingInfomation(PipeBase pipeBase, XDocument document_RevitData)
        {
            Console.WriteLine("Fittings下添加FittingEntity");
            var RevitData_fittings = document_RevitData.Root.Element("Fittings");

            Dictionary<String, Stack<String>> dictionary = new Dictionary<string, Stack<string>>();
            //2.添加连接信息 Fittings✓ Entitys-InputConnector✘
            foreach (var pipeGroup in pipeBase._pipeGroups) 
            {
                if(pipeGroup.Type.Contains("Line"))
                    AddFittingInfomationWithLine(RevitData_fittings, pipeGroup, dictionary);
                else
                    AddFittingInfomationWithEntity(RevitData_fittings, pipeGroup, dictionary);
            }
        }

        private static void AddFittingInfomationWithLine(XElement RevitData_fittings, PipeGroup pipeGroup, Dictionary<String, Stack<String>> dictionary)
        {
            dictionary.Clear();

            //2.添加列间
            foreach (var pipeList in pipeGroup._pipeLists)
            {
                //2.暂存列间
                string prevListNo = pipeList.PrevListNo;
                if (pipeList.PipeListNo == "1") { }
                else
                {
                    if (dictionary.ContainsKey(prevListNo))
                    {
                        var stack = dictionary[prevListNo];
                        dictionary.Remove(prevListNo);
                        stack.Push(pipeList._pipes[0].UniqueId);
                        dictionary.Add(prevListNo, stack);
                    }
                    else
                    {
                        Stack<String> stack = new Stack<string>();
                        stack.Push(pipeList._pipes[0].UniqueId);
                        dictionary.Add(prevListNo, stack);
                    }
                }
                //线管全添加
                //1.添加列内
                var pipes = pipeList._pipes;
                for (int i = 0; i < pipes.Count() - 1; i++)
                    RevitData_fittings.Add(MakeFittingEntity(pipeList, pipes[i].UniqueId, pipes[i + 1].UniqueId));

            }

            //2.添加列间✓
            while (dictionary.Count > 0)
            {
                var pair = dictionary.First();
                dictionary.Remove(pair.Key);
                int pipeListNo = Int32.Parse(pair.Key);
                Stack<string> stack = pair.Value;

                //线管全添加
                PipeList pipeList = pipeGroup._pipeLists[pipeListNo - 1];
                RevitData_fittings.Add(MakeFittingEntity(pipeList, pipeList._pipes[pipeList._pipes.Count() - 1].UniqueId, stack));
            }
        }

        private static void AddFittingInfomationWithEntity(XElement RevitData_fittings, PipeGroup pipeGroup, Dictionary<String, Stack<String>> dictionary)
        {
            dictionary.Clear();

            //2.添加列间
            foreach (var pipeList in pipeGroup._pipeLists)
            {
                //2.暂存列间
                string prevListNo = pipeList.PrevListNo;
                if (pipeList.PipeListNo == "1") { }
                else
                {
                    if (dictionary.ContainsKey(prevListNo))
                    {
                        var stack = dictionary[prevListNo];
                        dictionary.Remove(prevListNo);
                        stack.Push(pipeList._pipes[0].UniqueId);
                        dictionary.Add(prevListNo, stack);
                    }
                    else
                    {
                        Stack<String> stack = new Stack<string>();
                        stack.Push(pipeList._pipes[0].UniqueId);
                        dictionary.Add(prevListNo, stack);
                    }
                }
                //实管部分添加
                //1.添加列内
                var pipes = pipeList._pipes;
                if (pipes[0].UniqueId.Length < 40)
                    for (int i = 0; i < pipes.Count() - 1; i++)
                        RevitData_fittings.Add(MakeFittingEntity(pipeList, pipes[i].UniqueId, pipes[i + 1].UniqueId));
            }

            //2.添加列间✓
            while (dictionary.Count > 0)
            {
                var pair = dictionary.First();
                dictionary.Remove(pair.Key);
                int pipeListNo = Int32.Parse(pair.Key);
                Stack<string> stack = pair.Value;

                //实管部分添加
                bool doAdd = false;
                foreach (var uniqueId in stack)
                {
                    if (uniqueId.Length < 40)
                    {
                        doAdd = true;
                        break;
                    }
                }
                if (doAdd)
                {
                    PipeList pipeList = pipeGroup._pipeLists[pipeListNo - 1];
                    RevitData_fittings.Add(MakeFittingEntity(pipeList, pipeList._pipes[pipeList._pipes.Count() - 1].UniqueId, stack));
                }
            }
        }
        private static XElement MakeFittingEntity(PipeList pipeList,String PipeUid,Stack<String> otherPipeUids)
        {
            //< FittingEntity UniqueId = "22b0d132-700a-4311-99c1-a897f3091e1b-007334c3"
            //FamilyName = "201905170901-rapid-s-ng-coupling"
            //SymbolName = "Standard"
            //SystemClassfy = "Hydronic Supply"
            //SystemType = "DR-WP"
            //Point = "83893.656351341, 132177.063059417, 54899.999640338"
            //Rotation = "3.14159265358979"
            //Color = "128,128,0"
            //ConnectorEntitys = "a34b07e8-2fe7-4e76-b514-b29b270c410c-007328d8;22b0d132-700a-4311-99c1-a897f3091e1b-007334b4" />

            XElement fittingEntity = new XElement("FittingEntity");
            fittingEntity.SetAttributeValue("UniqueId", "");
            fittingEntity.SetAttributeValue("SystemClassfy", pipeList.SystemClassfy);
            fittingEntity.SetAttributeValue("SystemType", pipeList.SystemType);
            fittingEntity.SetAttributeValue("Color", pipeList.Color);

            StringBuilder sb = new StringBuilder(PipeUid);
            String resultStr;
            while (otherPipeUids.TryPop(out resultStr)) 
            {
                sb.Append($";{resultStr}");
            }
            fittingEntity.SetAttributeValue("ConnectorEntitys", sb);

            return fittingEntity;
        }

        private static XElement MakeFittingEntity(PipeList pipeList, String PipeUid, String otherPipeUid)
        {
            //< FittingEntity UniqueId = "22b0d132-700a-4311-99c1-a897f3091e1b-007334c3"
            //FamilyName = "201905170901-rapid-s-ng-coupling"
            //SymbolName = "Standard"
            //SystemClassfy = "Hydronic Supply"
            //SystemType = "DR-WP"
            //Point = "83893.656351341, 132177.063059417, 54899.999640338"
            //Rotation = "3.14159265358979"
            //Color = "128,128,0"
            //ConnectorEntitys = "a34b07e8-2fe7-4e76-b514-b29b270c410c-007328d8;22b0d132-700a-4311-99c1-a897f3091e1b-007334b4" />

            XElement fittingEntity = new XElement("FittingEntity");
            fittingEntity.SetAttributeValue("UniqueId", "");
            fittingEntity.SetAttributeValue("SystemClassfy", pipeList.SystemClassfy);
            fittingEntity.SetAttributeValue("SystemType", pipeList.SystemType);
            fittingEntity.SetAttributeValue("Color", pipeList.Color);

            fittingEntity.SetAttributeValue("ConnectorEntitys", $"{PipeUid};{otherPipeUid}");

            return fittingEntity;
        }

        private static void AddPipeInfomation(PipeBase pipeBase, XDocument document_RevitData)
        {
            Console.WriteLine("Entitys下添加Pipe/Duct");
            //② 添加Order到RevitData中
            //1.添加管道: Entitys-Entity[type:Pipe/Duct] 水管 风管
            var RevitData_entitys = document_RevitData.Root.Element("Entitys");

            foreach (var pipeGroup in pipeBase._pipeGroups)
                foreach (var pipeList in pipeGroup._pipeLists)
                    foreach (var pipe in pipeList._pipes)
                        RevitData_entitys.Add(MakeEntity(pipe, pipeList, pipeGroup.Type));
        }


        private static XElement MakeEntity(Pipe pipe, PipeList pipeList, String typeStr)
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

            //string uid = pipe.UniqueId;
            //if (uid==String.Empty || uid == "")
            //    entity.SetAttributeValue("UniqueId", anonymous++);
            //else
            //  entity.SetAttributeValue("UniqueId", pipe.Attribute("UniqueId").Value);

            //if (typeStr == "Pipe" || typeStr == "LinePipe")
            //{
            //    entity.SetAttributeValue("type", "Pipe");
            //}
            //else if (typeStr == "Duct" || typeStr == "LineDuct")
            //{
            //    entity.SetAttributeValue("type", "Duct");
            //}
            //else
            //    throw new Exception();

            //<Entity UniqueId="e2d76d32-4cec-40b4-ad7b-8ebcfd33815a-00731451" type="Pipe">
            XElement entity = new XElement("Entity");
            entity.SetAttributeValue("UniqueId", pipe.UniqueId);
            entity.SetAttributeValue("type", typeStr);

            //  <FamilyName value="DR Line" />
            XElement subNode;
            Type pipeListType = typeof(PipeList);
            void GetFromPipeListXml(String attrStr)
            {
                subNode = new XElement(attrStr);
                String Value = pipeListType.GetField(attrStr).GetValue(pipeList).ToString();
                subNode.SetAttributeValue("value", Value==null?String.Empty: Value);
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
                subNode.SetAttributeValue("value", Double.Parse(pipe.Width)/2);
                entity.Add(subNode);
            }
            else if (typeStr == "Duct" || typeStr == "LineDuct")
            {
                subNode = new XElement("DuctType");
                var DuctTypeValue = pipeList.DuctType;
                subNode.SetAttributeValue("value", DuctTypeValue == null?"矩形": DuctTypeValue);
                subNode.SetAttributeValue("Length1", pipe.Width);
                subNode.SetAttributeValue("Length2", pipe.Height);
                entity.Add(subNode);
            }
            else
                throw new Exception();

            //  <LocationEnt type="LineEntity" StartPoint="83354.360753559, 117274.620022954, 52930.000000000" EndPoint="82522.330897938, 117274.620022954, 52930.000000000" />
            subNode = new XElement("LocationEnt");
            subNode.SetAttributeValue("type", "LineEntity");
            subNode.SetAttributeValue("StartPoint", pipe.StartPoint);
            subNode.SetAttributeValue("EndPoint", pipe.EndPoint);
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

            //var fittings = root.Element("Fittings");
            //Console.WriteLine("删除FittingEntity");
            ////(2)Fitting-* 管道连接件及信息
            //fittings.RemoveNodes();

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
        private static void RemoveFittingInfomation(XDocument document_RevitData)
        {
            var root = document_RevitData.Root;
            var entitys = root.Element("Entitys");
            var IDs =
                from entity in entitys.Elements("Entity")
                let type = entity.Attribute("type").Value
                where type == "Duct" || type == "Pipe"
                select entity.Attribute("UniqueId").Value
                ;
            //var IDs=entitys.Elements("Entity").Union(entitys.Elements("Entity")).Select<XElement,String>((elem) => elem.Attribute("UniqueId").Value);

            var fittings = root.Element("Fittings");
            var deleteFittingEntitys =
                from fittingEntity in fittings.Elements()
                let connectorIDs=fittingEntity.Attribute("ConnectorEntitys").Value.Split(';')
                where connectorIDs.Count()!=
                (
                    from connectorID in connectorIDs
                    where IDs.Contains(connectorID)
                    select String.Empty
                ).Count()
                select fittingEntity
                ;

            //(2)Fitting-* 管道连接件及信息
            Console.WriteLine("删除错误FittingEntity");
            foreach (var deleteFitting in deleteFittingEntitys.ToList()) 
            {
                deleteFitting.Remove();
            }
        }
    }

    internal class PipeBase 
    {
        public List<PipeGroup> _pipeGroups = new List<PipeGroup>();
    }
    internal class PipeGroup
    {
        public List<PipeList> _pipeLists = new List<PipeList>();
        public string PipeGroupNo;
        public string Type;
    }
    internal class PipeList
    {
        public List<Pipe> _pipes = new List<Pipe>();
        public string PipeListNo;
        public string PrevListNo;

        public string FamilyName;
        public string SymbolName;
        public string Color;
        public string SystemClassfy;
        public string SystemType;
        public string HorizonOffset;
        public string VerticalOffset;

        public string DuctType;
    }
    internal class Pipe
    {
        public string UniqueId;
        public string Width;
        public string Height;
        public string StartPoint;
        public string EndPoint;
    }
}

