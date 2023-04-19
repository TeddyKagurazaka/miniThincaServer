using Newtonsoft.Json;
using System.Text;
using static miniThincaLib.Models.SecurityMessage;
using static miniThincaLib.Models.ClientIoOperation;
using static miniThincaLib.Models;
using static miniThincaLib.Helper.Helper;

namespace miniThincaLib
{
    /// <summary>
    /// 构建TCAP包序列的类
    /// </summary>
	public class Builder
	{
        /// <summary>
        /// 此处有确定能用的基础包，来回拼凑一下就能用了
        /// </summary>
        public class BasicBuilder
        {
            /// <summary>
            /// 在VFD上显示指定信息
            /// </summary>
            /// <param name="BrandType">钱包Brand(对应resource.xml)</param>
            /// <param name="MessageID">信息ID(对应resource.xml)</param>
            /// <returns></returns>
            public static TcapSubPacket ShowMessage(byte BrandType, byte MessageID)
            {
                var json = JsonConvert.SerializeObject(new MessageEventIo(0, 0, BrandType, MessageID, 20000)); //-> PASELI支払 カードをタッチしてください
                var PaseliMsgParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
                var PaseliCmdPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: PaseliMsgParam);
                var PaseliMessageCmd = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, PaseliCmdPacket);
                PaseliMessageCmd.setParam(new byte[] { 0x00, 0x03 });

                return PaseliMessageCmd;
            }

            /// <summary>
            /// Aime支持的LED颜色
            /// </summary>
            public enum LEDColor
            {
                Red = 0,
                Green = 1,
                Blue = 2,
                White = 3
            }
            /// <summary>
            /// Aime支持的灯光模式
            /// </summary>
            public enum LEDMode
            {
                Static = 0,
                Off = 1,
                Blink = 2
            }
            /// <summary>
            /// 控制Aime LED
            /// </summary>
            /// <param name="Mode">灯光模式</param>
            /// <param name="Color">颜色</param>
            /// <param name="Duration">总亮灯时长(毫秒)(闪烁，亮灯时有效)</param>
            /// <param name="OnDuration">开灯时长(毫秒)(闪烁时有效)</param>
            /// <param name="OffDuration">关灯时长(毫秒)(闪烁时有效)</param>
            /// <returns></returns>
            public static TcapSubPacket OperateLED(LEDMode Mode, LEDColor Color, short Duration = 0, short OnDuration = 0, short OffDuration = 0)
            {
                //LedCmd (00)(00)(a2 == 03)(a3 == 0,1,2,3)(a4 == 1,2)(byteLength)(2byte)(2byte)(2byte)(2byte)
                //a3 = 03 a4 = 01
                var DurationByte = returnReversedByte(Duration);
                var OnDurationByte = returnReversedByte(OnDuration);
                var OffDurationByte = returnReversedByte(OffDuration);


                var ledCommand = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg,
                       GenerateOpCmdPacket(new byte[] { 0x31 }, new byte[] {
                       0x00,0x00,
                       0x03,        //a2 必须为0x03
                       (byte)Color,        //a3 可以设置为0(红色),1(绿色),2(蓝色),3(白色)
                       (byte)Mode,        //a4 可以设置为0,1,2
                                    //  0的时候只读取a5[1],长度要求4  (常亮)
                                    //  1的时候关闭
                                    //  2的时候读取a5[1:3],长度要求8 （闪烁)
                       0x00,0x08,   //后面部分长度
                       0x00,0x00,   //a5 无视
                       DurationByte[0],DurationByte[1],   //a5[1] 亮灯时间(5s)
                       OnDurationByte[0],OnDurationByte[1],   //a5[2] 开灯时间(msec)
                       OffDurationByte[0],OffDurationByte[1]    //a5[3] 关灯时间(msec)
                       }, true)
                    );
                ledCommand.setParam(new byte[] { 0x00, 0x05 });

                return ledCommand;
            }

            /// <summary>
            /// Aime读卡器支持的卡片类型
            /// </summary>
            public enum AimeReaderCardType
            {
                Mifare = 1,
                Felica = 8,
                Both = 9
            }

            /// <summary>
            /// 开启Aime读卡器
            /// </summary>
            /// <param name="detectType">检测的卡片类型</param>
            /// <returns></returns>
            public static TcapSubPacket OpenAimeReader(AimeReaderCardType detectType = AimeReaderCardType.Mifare | AimeReaderCardType.Felica)
            {
                var openRwPacket = GenerateOpCmdPacket("OPEN_RW", new byte[] { 0x00, 0x00, (byte)detectType }, true); 

                var openRw = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, openRwPacket);
                openRw.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW

                return openRw;
            }

            /// <summary>
            /// 开始检测读卡（会塞死）直到读到卡片或超时
            /// </summary>
            /// <param name="TimeoutMsec">超时时间(毫秒)</param>
            /// <returns></returns>
            public static TcapSubPacket DetectAimeCard(short TimeoutMsec)
            {
                var timeoutByte = returnReversedByte(TimeoutMsec);

                //(他会在这个包塞死 所以回传的时候要么没数据要么带卡号)
                var detectPacket = GenerateOpCmdPacket("TARGET_DETECT", opCmdPacket: new byte[] { 0x00, 0x00, timeoutByte[0], timeoutByte[1] }, true);   //(00 00)(2byte timeout millsec)
                var detect = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, detectPacket);
                detect.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW

                return detect;
            }

            /// <summary>
            /// 关闭Aime读卡器
            /// </summary>
            /// <returns></returns>
            public static TcapSubPacket CloseAimeReader()
            {
                var openRwPacket = GenerateOpCmdPacket("CLOSE_RW", new byte[] { 0x00, 0x00 }, true);

                var openRw = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, openRwPacket);
                openRw.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW

                return openRw;
            }

            /// <summary>
            /// 让机台返回基础信息
            /// </summary>
            /// <returns></returns>
            public static TcapSubPacket RequestOperateXml()
            {
                var RequestPacket = GenerateOpCmdPacket("REQUEST");
                var RequestCmd = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, RequestPacket);
                RequestCmd.setParam(new byte[] { 0x00, 0x01 }); //Generic CLIENT

                return RequestCmd;
            }

            /// <summary>
            /// 播放声音
            /// </summary>
            /// <param name="brandType">钱包Brand</param>
            /// <param name="soundID">声音ID(一般0是成功 1是失败)</param>
            /// <returns></returns>
            public static TcapSubPacket PlaySound(byte brandType,byte soundID = 0)
            {
                var json = JsonConvert.SerializeObject(new SoundEventIo(0, brandType, soundID, 20000)); //-> paseli//
                var opioCmdParam = Encoding.UTF8.GetBytes(json);
                opioCmdParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(opioCmdParam).ToArray();
                var opCmdPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: opioCmdParam);
                var opCommand = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
                opCommand.setParam(new byte[] { 0x00, 0x03 });

                return opCommand;
            }

            /// <summary>
            /// 设定机台的基础信息
            /// </summary>
            /// <param name="operateXml">ReturnOperateEntityXml()</param>
            /// <returns></returns>
            public static TcapSubPacket UpdateOperateXml(string operateXml)
            {
                var CurrentBody = GenerateOpCmdPacket("CURRENT", opCmdPacket: Encoding.UTF8.GetBytes(operateXml));
                var CurrentPacket = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, CurrentBody);
                CurrentPacket.setParam(new byte[] { 0x00, 0x01 });

                return CurrentPacket;
            }

            /// <summary>
            /// 设定机台的基础信息，但是有时候机台会对基础信息的包头检测(一般是DirectIO 注册机台时)，用这个直接传附带包头的版本
            /// </summary>
            /// <param name="operateXmlByte">附带上包头的ReturnOperateEntityXml()</param>
            /// <returns></returns>
            public static TcapSubPacket UpdateOperateXml(byte[] operateXmlByte)
            {
                var CurrentBody = GenerateOpCmdPacket("CURRENT", opCmdPacket: operateXmlByte);
                var CurrentPacket = new TcapSubPacket(TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, CurrentBody);
                CurrentPacket.setParam(new byte[] { 0x00, 0x01 });

                return CurrentPacket;
            }
        }

        /// <summary>
        /// 生成能让机台调用Aime读卡器的Tcap包序列(显示刷卡提示,闪烁白灯,打开读卡器并开始检测卡片,返回机台基础信息)
        /// </summary>
        /// <returns></returns>
        public static byte[] BuildGetAimeCardResult(byte brandType,byte messageId)
        {
            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            //显示信息
            OperatePkt.AddSubType(BasicBuilder.ShowMessage(brandType, messageId));
            //读卡器闪烁白灯 时长5秒 开0.5秒关0.5秒
            OperatePkt.AddSubType(BasicBuilder.OperateLED(BasicBuilder.LEDMode.Blink, BasicBuilder.LEDColor.White, 5000,500,500));
            //打开读卡器
            OperatePkt.AddSubType(BasicBuilder.OpenAimeReader());
            //5秒内检测读到的卡片
            OperatePkt.AddSubType(BasicBuilder.DetectAimeCard(5000));
            //返回机台信息
            OperatePkt.AddSubType(BasicBuilder.RequestOperateXml());

            return OperatePkt.Generate();
        }

        /// <summary>
        /// 生成能让机台认为付款成功的Tcap包序列(播放成功音,LED亮绿灯,更新机台Xml)
        /// </summary>
        /// <param name="cardNo">卡号</param>
        /// <param name="seqNumber">顺序号码，机台用于区分支付</param>
        /// <param name="amount">支付额，固定余额返回支付额+1</param>
        /// <returns></returns>
        public static byte[] BuildSuccessPaymentResult(byte brandType, int amount = 100, string cardNo = "01391144551419198100", string seqNumber = "1")
        {
            //发送支付成功状态回服务器
            string paymentPacket = BuildPaymentJsonByBrandType(brandType, amount,cardNo);
            string paymentXml = ReturnOperateEntityXml_AuthorizeSales(
                serviceName: "AuthorizeSales",
                AdditionalSecurityInformation: paymentPacket,
                SeqNumber: seqNumber,
                balance: (amount + 1).ToString(),
                setAmount: amount.ToString(),
                account: cardNo);

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            //播放刷卡成功音
            OperatePkt.AddSubType(BasicBuilder.PlaySound(brandType));
            //LED常量绿灯 5秒
            OperatePkt.AddSubType(BasicBuilder.OperateLED(BasicBuilder.LEDMode.Static, BasicBuilder.LEDColor.Green,5000));
            //更新机台的OperateXml，内含账单
            OperatePkt.AddSubType(BasicBuilder.UpdateOperateXml(paymentXml));
            return OperatePkt.Generate();
        }

        public static byte[] BuildBalanceInquireResult(byte brandType, string cardNo = "01391144551419198100", string seqNumber = "1")
        {
            //var returnBalanceJson = new Receipts.Receipt_BalanceInquire.Receipt_BalanceInquire_Paseli();
            //returnBalanceJson.CardNo = cardNo;
            //returnBalanceJson.Balance = 1234;

            //string paymentPacket = JsonConvert.SerializeObject(returnBalanceJson);
            string paymentPacket = BuildBalanceInquireResult(brandType, cardNo);
            string paymentXml = ReturnOperateEntityXml_AuthorizeSales(
                serviceName: "AuthorizeSales",
                AdditionalSecurityInformation: paymentPacket,
                SeqNumber: seqNumber,
                balance: "1234",
                setAmount: "0",
                account: cardNo);

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            //播放刷卡成功音
            OperatePkt.AddSubType(BasicBuilder.PlaySound(brandType));
            //LED常量绿灯 5秒
            OperatePkt.AddSubType(BasicBuilder.OperateLED(BasicBuilder.LEDMode.Static, BasicBuilder.LEDColor.Green, 5000));
            //更新机台的OperateXml，内含账单
            OperatePkt.AddSubType(BasicBuilder.UpdateOperateXml(paymentXml));
            return OperatePkt.Generate();

        }

        /// <summary>
        /// 生成能让机台通过握手的Tcap包序列(固定流程,不用管)
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
        /// 生成能让机台结束Tcap处理流程的Tcap包序列(固定流程,不用管)
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
            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            OperatePkt.AddSubType(BasicBuilder.RequestOperateXml());
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
            OperatePkt.AddSubType(BasicBuilder.UpdateOperateXml(opCmdParamBytes));

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
            OperatePkt.AddSubType(BasicBuilder.UpdateOperateXml(opCmdParamBytes));

            return OperatePkt.Generate();
        }

        static string BuildPaymentJsonByBrandType(byte brandType,int amount,string cardNo)
        {
            switch ((ThincaBrandType)brandType)
            {
                case ThincaBrandType.Nanaco:
                    var newResult_nnc = new Receipts.ReceiptInfo_Nanaco();
                    return JsonConvert.SerializeObject(newResult_nnc);

                case ThincaBrandType.Edy:
                    var newResult_edy = new Receipts.ReceiptInfo_Edy.ReceiptInfo_Edy_Payment();
                    newResult_edy.DealBefERemainder = 201;
                    newResult_edy.DealAftRemainder = 101;
                    return JsonConvert.SerializeObject(newResult_edy);

                case ThincaBrandType.Id:
                    var newResult_iD = new Receipts.Receipt_Payment.Receipt_Payment_iD();
                    newResult_iD.SettledAmount = amount;
                    newResult_iD.ApprovalNumber = cardNo;
                    return JsonConvert.SerializeObject(amount);

                case ThincaBrandType.Quicpay:
                    var newResult_qcp = new Receipts.Receipt_Payment.Receipt_Payment_QuicPay();
                    newResult_qcp.SettledAmount = amount;
                    newResult_qcp.AccountNumber = cardNo;
                    return JsonConvert.SerializeObject(newResult_qcp);

                case ThincaBrandType.Transport:
                    var newResult_tsp = new Receipts.Receipt_Payment.Receipt_Payment_Transport();
                    newResult_tsp.SettledAmount = amount;
                    newResult_tsp.DealBefRemainder = 201;
                    newResult_tsp.DealAftRemainder = 101;
                    newResult_tsp.AccountNumber = cardNo;
                    return JsonConvert.SerializeObject(newResult_tsp);

                case ThincaBrandType.Waon:
                    var newResult_wao = new Receipts.Receipt_Payment.Receipt_Payment_Waon();
                    newResult_wao.SettledAmount = amount;
                    newResult_wao.DealBefRemainder = 201;
                    newResult_wao.DealAftRemainder = 101;
                    newResult_wao.AccountNumber = cardNo;
                    return JsonConvert.SerializeObject(newResult_wao);

                case ThincaBrandType.Nanaco2:
                    var newResult_nc2 = new Receipts.Receipt_Payment.Receipt_Payment_Nanaco2();
                    newResult_nc2.DealBefRemainder = 201;
                    newResult_nc2.DealAftRemainder = 101;
                    newResult_nc2.SettledAmount = amount;
                    newResult_nc2.AccountNumber = cardNo;
                    return JsonConvert.SerializeObject(newResult_nc2);

                case ThincaBrandType.Paseli:
                    var newResult_psl = new Receipts.Receipt_Payment.Receipt_Payment_Paseli();
                    newResult_psl.CardNo = cardNo;
                    newResult_psl.SettledAmount = amount;
                    newResult_psl.Balance = amount + 1;
                    return JsonConvert.SerializeObject(newResult_psl);

                case ThincaBrandType.Sapica:
                    var newResult_spc = new Receipts.Receipt_Payment.Receipt_Payment_Sapica();
                    newResult_spc.AccountNumber = cardNo;
                    newResult_spc.DealBefRemainder = 201;
                    newResult_spc.DealAftRemainder = 101;
                    newResult_spc.SettledAmount = amount;
                    return JsonConvert.SerializeObject(newResult_spc);

                default:
                    var newResult = new Receipts.Receipt_Payment.Receipt_Payment_Base();
                    newResult.AccountNumber = cardNo;
                    newResult.SettledAmount = amount;
                    return JsonConvert.SerializeObject(newResult);
            }
        }

        static string BuildBalanceInquireResult(byte brandType, string cardNo)
        {
            switch ((ThincaBrandType)brandType)
            {
                case ThincaBrandType.Nanaco:
                    var resp_nnc = new Receipts.ReceiptInfo_Nanaco();
                    return JsonConvert.SerializeObject(resp_nnc);
                case ThincaBrandType.Edy:
                    var resp_edy = new Receipts.ReceiptInfo_Edy.ReceiptInfo_Edy_BalanceInquire();
                    return JsonConvert.SerializeObject(resp_edy);
                case ThincaBrandType.Nanaco2:
                    var resp_nc2 = new Receipts.Receipt_BalanceInquire.Receipt_BalanceInquire_Nanaco();
                    resp_nc2.AccountNumber = cardNo;
                    return JsonConvert.SerializeObject(resp_nc2);
                case ThincaBrandType.Paseli:
                    var resp_psl = new Receipts.Receipt_BalanceInquire.Receipt_BalanceInquire_Paseli();
                    resp_psl.AccountNumber = cardNo;
                    resp_psl.Balance = 201;
                    resp_psl.CardNo = cardNo;
                    resp_psl.SettledAmount = 0;
                    return JsonConvert.SerializeObject(resp_psl);
                case ThincaBrandType.Sapica:
                    var resp_spc = new Receipts.Receipt_BalanceInquire.Receipt_BalanceInquire_Sapica();
                    resp_spc.AccountNumber = cardNo;
                    resp_spc.SettledAmount = 0;
                    return JsonConvert.SerializeObject(resp_spc);

                default:
                    var resp = new Receipts.Receipt_BalanceInquire.Receipt_BalanceInquire_Base();
                    resp.AccountNumber = cardNo;
                    resp.SettledAmount = 0;
                    return JsonConvert.SerializeObject(resp);

            }
        }

    }
}

