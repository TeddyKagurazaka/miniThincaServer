using System;
using Newtonsoft.Json;
using System.Text;
using static miniThincaLib.Models.SecurityMessage;
using static miniThincaLib.Models.ClientIoOperation;
using static miniThincaLib.Models;
using static miniThincaLib.Helper.Helper;

namespace miniThincaLib
{
	public class Builder
	{
        /// <summary>
        /// 生成能让机台调用Aime读卡器的Tcap包序列
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildGetAimeCardResult()
        {
            var json = JsonConvert.SerializeObject(new MessageEventIo(0, 0, 8, 30, 20000)); //-> PASELI支払 カードをタッチしてください
            var PaseliMsgParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
            var PaseliCmdPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: PaseliMsgParam);
            var PaseliMessageCmd = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, PaseliCmdPacket);
            PaseliMessageCmd.setParam(new byte[] { 0x00, 0x03 }); //Generic OPTION

            var ledjson = JsonConvert.SerializeObject(new LedEventIo(3, 0, 20000, 0, 0, 0));
            var ledJsonByte = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(ledjson)).ToArray();
            var ledJsonPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: ledJsonByte);
            var ledMessageCmd = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, ledJsonPacket);
            ledMessageCmd.setParam(new byte[] { 0x00, 0x03 }); //Generic OPTION

            var openRwPacket = GenerateOpCmdPacket("OPEN_RW", new byte[] { 0x00, 0x00, 0x09 }, true); //(code1)(2byte code2) (code1==0)(code2 = 1(mifare only?),8(felica only?),9(both?))
                                                                                                      //9 AND 1 = 1
                                                                                                      //9 >> 2 = 2
                                                                                                      //-> 1 = 1,8 = 2,9 = 3

            var openRw = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, openRwPacket);
            openRw.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW

            //(他会在这个包塞死 所以回传的时候要么没数据要么带卡号)
            var detectPacket = GenerateOpCmdPacket("TARGET_DETECT", opCmdPacket: new byte[] { 0x00, 0x00, 0x13, 0x88 }, true);   //(00 00)(2byte timeout millsec)
            var detect = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, detectPacket);
            detect.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW


            var RequestPacket = GenerateOpCmdPacket("REQUEST");
            var RequestCmd = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, RequestPacket);
            RequestCmd.setParam(new byte[] { 0x00, 0x01 }); //Generic CLIENT

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            OperatePkt.AddSubType(PaseliMessageCmd);
            OperatePkt.AddSubType(ledMessageCmd);
            OperatePkt.AddSubType(openRw);
            OperatePkt.AddSubType(detect);
            OperatePkt.AddSubType(RequestCmd);

            return OperatePkt.Generate();
        }

        /// <summary>
        /// 生成能让机台认为付款成功的Tcap包序列
        /// </summary>
        /// <param name="cardNo">卡号</param>
        /// <param name="seqNumber">顺序号码，机台用于区分支付</param>
        /// <param name="amount">支付额，固定余额返回支付额+1</param>
        /// <returns></returns>
        public static byte[] BuildSuccessPaymentResult(string cardNo = "01391144551419198100", string seqNumber = "1", int amount = 100)
        {

            //var json = JsonConvert.SerializeObject(new MessageEventIo(0,0,8,30,20000)); -> PASELI支払 カードをタッチしてください
            var json = JsonConvert.SerializeObject(new SoundEventIo(0, 8, 0, 20000)); //-> paseli//
                                                                                      //var json = JsonConvert.SerializeObject(new LedEventIo(0,0,20000,127,127,127)); // -> ?
                                                                                      //var json = JsonConvert.SerializeObject(new AmountEventIo(1,0,8,1,1000,20000));
            var opioCmdParam = Encoding.UTF8.GetBytes(json);
            opioCmdParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(opioCmdParam).ToArray();
            var opCmdPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: opioCmdParam);
            var opCommand = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
            opCommand.setParam(new byte[] { 0x00, 0x03 });


            //发送支付成功状态回服务器
            var returnSalesJson = new AdditionalSecurityMessage_AuthorizeSales();
            returnSalesJson.CardNo = cardNo;
            returnSalesJson.SettledAmount = amount;
            returnSalesJson.Balance = amount + 1;

            string paymentPacket = JsonConvert.SerializeObject(returnSalesJson);
            string paymentXml = ReturnOperateEntityXml_AuthorizeSales(
                serviceName: "AuthorizeSales",
                AdditionalSecurityInformation: paymentPacket,
                SeqNumber: seqNumber,
                balance: (amount + 1).ToString(),
                setAmount: amount.ToString(),
                account: cardNo);

            var CurrentBody = GenerateOpCmdPacket("CURRENT", opCmdPacket: Encoding.UTF8.GetBytes(paymentXml));
            var CurrentPacket = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, CurrentBody);
            CurrentPacket.setParam(new byte[] { 0x00, 0x01 });

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            OperatePkt.AddSubType(opCommand);
            OperatePkt.AddSubType(CurrentPacket);
            return OperatePkt.Generate();
        }

        /// <summary>
        /// 生成能让机台通过握手的Tcap包序列
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildHandshakeResult()
        {
            var HandshakePacket = new TcapPacket(TcapPacketType.Handshake);
            var Op23SubPacket = new TcapSubPacket(TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
            var Op81SubPacket = new TcapSubPacket(TcapPacketSubType.op81_HandshakeAccept_UpdateSetNetTimeout_OpPlaySoundMsg,
                new byte[] { 0x02, 0x05, 0x00 });
            var Op24SubPacket = new TcapSubPacket(TcapPacketSubType.op24_HandshakeReq_FarewellDone);

            HandshakePacket.AddSubType(Op23SubPacket);
            HandshakePacket.AddSubType(Op81SubPacket);
            HandshakePacket.AddSubType(Op24SubPacket);
            return HandshakePacket.Generate();
        }

        /// <summary>
        /// 生成能让机台结束Tcap处理流程的Tcap包序列
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildFarewellResult()
        {
            var Farewell = new TcapPacket(TcapPacketType.Farewell);
            var op23 = new TcapSubPacket(TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
            var op25 = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, new byte[] { 0x12, 0x34, 0x56, 0x78 });
            var op24 = new TcapSubPacket(TcapPacketSubType.op24_HandshakeReq_FarewellDone);

            Farewell.AddSubType(op23);
            Farewell.AddSubType(op25);
            Farewell.AddSubType(op24);
            return Farewell.Generate();
        }

        /// <summary>
        /// 生成获取机台当前信息的Tcap包序列
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildGetMachineInfoPacket()
        {
            var RequestPacket = GenerateOpCmdPacket("REQUEST");
            var RequestCmd = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, RequestPacket);
            RequestCmd.setParam(new byte[] { 0x00, 0x01 }); //Generic CLIENT

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            OperatePkt.AddSubType(RequestCmd);
            return OperatePkt.Generate();
        }

        /// <summary>
        /// 生成能让机台通过initAuth.jsp的Tcap包序列
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildInitAuthOperateMsgResult()
        {
            var additionalSecurity = JsonConvert.SerializeObject(new AdditionalSecurityMessage());

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(ReturnOperateEntityXml_initAuth("DirectIO", additionalSecurity))).ToArray();
            var opCmdPacket = GenerateOpCmdPacket("CURRENT", opCmdParamBytes);
            var opCommand = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
            opCommand.setParam(new byte[] { 0x00, 0x01 }); //必须0x01 否则4002144
            OperatePkt.AddSubType(opCommand);

            return OperatePkt.Generate();
        }

        /// <summary>
        /// 生成能让机台通过emlist.jsp的Tcap包序列
        /// </summary>
        /// <param name="TermSerial">机台序列号，用于下发专用的支付endpoint</param>
        /// <returns></returns>
        public static byte[] BuildInitAuthOperateMsgResult_em2(string TermSerial)
        {
            var additionalSecurity = JsonConvert.SerializeObject(new AdditionalSecurityMessage_em2(TermSerial));

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(ReturnOperateEntityXml_initAuth("DirectIO", additionalSecurity))).ToArray();
            var opCmdPacket = GenerateOpCmdPacket("CURRENT", opCmdParamBytes);
            var opCommand = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
            opCommand.setParam(new byte[] { 0x00, 0x01 }); //必须0x01 否则4002144
            OperatePkt.AddSubType(opCommand);

            return OperatePkt.Generate();
        }

    }
}

