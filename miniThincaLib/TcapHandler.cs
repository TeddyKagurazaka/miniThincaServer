using System;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.Linq;

namespace miniThincaLib
{
	public class TcapHandler
	{
		public enum TcapRequestType
		{
			initAuth,
			emStage2,
			AuthorizeSales,
			BalanceInquire,
			Others
		}

		enum MachineState
		{
			Handshake,
			Idle,
            initAuth_SentInitInfo,
			RequestOp_InitCardSwipe,
			RequestOp_SuccessSwipe,
			RequestOp_SuccessPayment,
			Farewell,
			Error,
			Init
		}
		class MachineInfo
		{
			public TcapRequestType reqType { get; } = TcapRequestType.Others;
            public MachineState state { get; set; } = MachineState.Init;

            public DateTime firstRequest { get; }
            public DateTime lastRequest { get; set; }

            public MachineInfo(TcapRequestType ReqType, MachineState State)
            {
                reqType = ReqType;
                state = State;
                firstRequest = DateTime.Now;
                lastRequest = DateTime.Now;
            }

            
		}

		static Dictionary<string, MachineInfo> machineInfo = new Dictionary<string, MachineInfo>();


		public byte[] HandleTcapRequest(TcapRequestType requestMethod, byte[] Input,string termSerial = "")
		{
			var RequestMessage = new Models.TcapMessageRequest(Input);
            

			switch (RequestMessage.pktType)
			{
				case Models.TcapPacketType.Handshake:
                    return Builder.BuildHandshakeResult();
				case Models.TcapPacketType.EmptyPacket: //空包
					switch (requestMethod)
					{
						case TcapRequestType.initAuth:
						case TcapRequestType.emStage2:  //认证的时候先获取机器信息（需要序列号）
							return Builder.BuildGetMachineInfoPacket();
						case TcapRequestType.AuthorizeSales:    //付款的时候先打开Aime读卡器
                            if(string.IsNullOrEmpty(termSerial)) Builder.BuildFarewellResult();

                            //这时候是肯定有termSerial的(因为emStage2)
                            machineInfo.Add(termSerial, new MachineInfo(TcapRequestType.AuthorizeSales, MachineState.RequestOp_InitCardSwipe));
							return Builder.BuildGetAimeCardResult();
						default:                        //其他未知方法直接送走
                            return Builder.BuildFarewellResult();
                    }
				case Models.TcapPacketType.OperateEntity:
                    switch (requestMethod)
                    {
                        case TcapRequestType.initAuth:
                            return HandleInitAuthPacket(RequestMessage);
                        case TcapRequestType.emStage2:
                            return HandleInitAuthPacket(RequestMessage,true);
                        case TcapRequestType.AuthorizeSales:
                            if (string.IsNullOrEmpty(termSerial)) Builder.BuildFarewellResult();

                            return HandleAuthSalesPacket(RequestMessage,termSerial);
                        default:
                            return Builder.BuildFarewellResult();
                    }
                default:
					return Builder.BuildFarewellResult();
            }

		}

		byte[] HandleAuthSalesPacket(Models.TcapMessageRequest request,string termSerial)
		{
            var currentInfo = machineInfo[termSerial];
            if(currentInfo == null)
            {
                machineInfo.Add(termSerial, new MachineInfo(TcapRequestType.AuthorizeSales, MachineState.RequestOp_InitCardSwipe));
                return Builder.BuildGetAimeCardResult();
            };

            currentInfo.lastRequest = DateTime.Now;
            if (currentInfo.state == MachineState.RequestOp_InitCardSwipe)
            {
                //MsgParam LEDParam OpenRW Detect(这个包找卡号) REQUEST(这个包找SeqNumber)
                var DetectPkt = request.messages[3].MessageHex;
                if (DetectPkt.Length == 54) //读到了Felica卡号
                {
                    var RequestPkt = request.messages[4].MessageBody;
                    var cardId = DetectPkt.Substring(18, 20);

                    byte[] reqXmlStr = new byte[RequestPkt.Length - 6];
                    Array.Copy(RequestPkt, 6, reqXmlStr, 0, RequestPkt.Length - 6);
                    //var reqXmlStr = RequestPkt.SubArray(6, RequestPkt.Length - 6);
                    var reqXml = new XmlDocument();
                    reqXml.LoadXml(Encoding.UTF8.GetString(reqXmlStr));

                    var nsmgr = new XmlNamespaceManager(reqXml.NameTable);
                    nsmgr.AddNamespace("ICAS", "http://www.hp.com/jp/FeliCa/ICASClient");


                    var SequenceNumber = (reqXml.SelectSingleNode("//ICAS:longValue[@name='SequenceNumber']", nsmgr) as XmlElement).GetAttribute("value");
                    var Amount = (reqXml.SelectSingleNode("//ICAS:currencyValue[@name='Amount']", nsmgr) as XmlElement).GetAttribute("value");
                    var AmountInt = int.Parse(Amount);

                    currentInfo.state = MachineState.RequestOp_SuccessPayment;
                    return Builder.BuildSuccessPaymentResult(cardId, SequenceNumber, AmountInt);
                }
                else if((currentInfo.lastRequest - currentInfo.firstRequest).TotalSeconds >= 28)
                {
                    machineInfo.Remove(termSerial);
                    return Builder.BuildFarewellResult();
                }
                else
                {
                    return Builder.BuildGetAimeCardResult();
                }
            }
            else if (currentInfo.state == MachineState.RequestOp_SuccessPayment)
            {
                machineInfo.Remove(termSerial);
                return Builder.BuildFarewellResult();
            }
            else
            {
                currentInfo.state = MachineState.RequestOp_InitCardSwipe;
                return Builder.BuildGetAimeCardResult();
            }

        }

        byte[] HandleInitAuthPacket(Models.TcapMessageRequest request,bool emStage2 = false)
        {
            if(request.messages.Count == 1)
            {
                if (request.messages[0].MessageHex != "000000020000")
                {
                    var RequestPkt = request.messages[0].MessageBody;
                    byte[] reqXmlStr = new byte[RequestPkt.Length - 6];
                    Array.Copy(RequestPkt, 6, reqXmlStr, 0, RequestPkt.Length - 6);
                    //var reqXmlStr = RequestPkt.SubArray(6, RequestPkt.Length - 6);
                    var reqXml = new XmlDocument();
                    reqXml.LoadXml(Encoding.UTF8.GetString(reqXmlStr));

                    var nsmgr = new XmlNamespaceManager(reqXml.NameTable);
                    nsmgr.AddNamespace("ICAS", "http://www.hp.com/jp/FeliCa/ICASClient");

                    //Newtonsoft.Json.JsonConvert.DeserializeXmlNode("{\"Root\":" + (reqXml.SelectSingleNode("//ICAS:stringValue[@name='AdditionalSecurityInformation']", nsmgr) as XmlElement).GetAttribute("value") + "}")

                    var securityInformation = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(
                            "{\"Root\":" +
                            (reqXml.SelectSingleNode("//ICAS:stringValue[@name='AdditionalSecurityInformation']", nsmgr) as XmlElement).GetAttribute("value")
                            + "}");


                    var serial = "";
                    if (emStage2)
                        serial = securityInformation
                            .SelectSingleNode("/Root/TermSerial")
                            .InnerText;
                    else
                        serial = securityInformation
                            .SelectSingleNode("/Root/UniqueCode")
                            .InnerText;

                    //var additionalSecurity = (reqXml.SelectSingleNode("//ICAS:stringValue[@name='pString']", nsmgr) as XmlElement).GetAttribute("value");
                    //Dictionary<string, string> parsedSecurity = new Dictionary<string, string>();
                    //foreach (var securityData in additionalSecurity.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    //{
                    //    var kvPair = securityData.Split('=');
                    //    parsedSecurity[kvPair[0]] = kvPair[1];
                    //}
                    //stage2: UniqueCode=ACAE01A9999
                    //emstage2: TermSerial=ACAE01A9999

                    if (emStage2) { return Builder.BuildInitAuthOperateMsgResult_em2(serial); }
                    else return Builder.BuildInitAuthOperateMsgResult();
                }
                else
                {
                    //已经设置完成了，返回
                    return Builder.BuildFarewellResult();
                }

            }
            else
                return Builder.BuildGetMachineInfoPacket();
        }
	}
}

