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
			RequestOp_InitCardSwipe,
			RequestOp_SuccessSwipe,
			RequestOp_SuccessPayment,
			Farewell,
			Error,
			Init
		}
		class MachineInfo
		{
			string machineId = "";
			TcapRequestType reqType = TcapRequestType.Others;
            MachineState state = MachineState.Init;

			DateTime firstRequest = new DateTime();
			DateTime lastRequest = new DateTime();
		}

		static Dictionary<string, MachineInfo> machineInfo = new Dictionary<string, MachineInfo>();


		public byte[] HandleTcapRequest(TcapRequestType requestMethod, byte[] Input)
		{
			var RequestMessage = new Models.TcapMessageRequest(Input);

			switch (RequestMessage.pktType)
			{
				case Models.TcapPacketType.Handshake:
                    return Builder.BuildHandshakeResult();
				case Models.TcapPacketType.EmptyPacket:
					switch (requestMethod)
					{
						case TcapRequestType.initAuth:
							return Builder.BuildInitAuthOperateMsgResult();
						case TcapRequestType.emStage2:
							return Builder.BuildInitAuthOperateMsgResult_em2();
						case TcapRequestType.AuthorizeSales:
							return HandleAuthSalesPacket(RequestMessage);
						default:
                            return Builder.BuildFarewellResult();
                    }
				default:
					return Builder.BuildFarewellResult();
            }

		}

		byte[] HandleAuthSalesPacket(Models.TcapMessageRequest request)
		{
            //完成OperateEntity写入之后使用Farewell包完成TCAP流程
            //因为发多少OperateDeviceMsg就会按指定顺序返回多少Packet,所以直接按发包顺序找对应的包
            if (request.messages.Count == Builder.AimeCardResultPktCount) // -> BuildGetAimeCardResult();
            {
                //MsgParam LEDParam OpenRW Detect(这个包找卡号) REQUEST(这个包找SeqNumber)
                var DetectPkt = request.messages[3].MessageHex;
                var RequestPkt = request.messages[4].MessageBody;

                if (DetectPkt.Length == 54) //读到了Felica卡号
                {
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

                    return Builder.BuildSuccessPaymentResult(cardId, SequenceNumber, AmountInt);
                }
                else
                {
                    //没读到卡号 直接退
                    //否则连续发的话会进入死循环
                    return Builder.BuildFarewellResult();
                }
            }
            else if (request.messages.Count == Builder.SuccessPaymentResultPktCount)
            {
                //完事 结束
                return Builder.BuildFarewellResult();
            }
            else
            {
                //继续让他请求卡号
                return Builder.BuildGetAimeCardResult();
            }
        }
	}
}

