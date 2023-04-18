using System;
using System.Text;
using miniThincaLib.Helper;
using static miniThincaLib.Helper.Helper;

namespace miniThincaLib
{
	public static class Models
    {
        public enum ThincaBrandType
        {
            Nanaco = 1, //0x2010001,0x1040001,0x2050001,0x3010001
            Edy = 2,    //0,1,2,3,4,5
            Id = 3,
            Quicpay = 4,
            Transport = 5,
            Waon = 6,
            Nanaco2 = 7,
            Paseli = 8,
            Sapica = 9
        }

        public enum TcapPacketType
        {
            Handshake = 0x01,
            Farewell = 0x02,
            Error = 0x03,
            AppDataTransfer = 0x04,
            UpdateEntity = 0x05,
            OperateEntity = 0x06,
            EmptyPacket = 0xFF,
            Unknown = 0x00
        }

        public enum TcapPacketSubType
        {
            General_UnknownMessage = 0xFF,
            General_Message = 0x00,
            General_WarningMessage = 0x01,

            op23_HandshakeReq_FarewellGoodbye_ErrorUnex = 0x23,
            op24_HandshakeReq_FarewellDone = 0x24,
            op25_FarewellReturnCode_OpOperateDeviceMsg = 0x25,

            op81_HandshakeAccept_UpdateSetNetTimeout_OpPlaySoundMsg = 0x81,

            op101_ATFelicaCmd_UpdSetFelicaInterval_OpReqFelicaOpenRw = 0x101,

            //Handshake_op23 = 0x23,  //op23=RequestMessage
            //Handshake_RequestMessage = 0x24,
            //Handshake_AcceptMessage = 0x81,

            //Handshake_op23_Farewell_ServerGoodBye = 0x23,
            //Farewell_ServerGoodByeDone = 0x24,
            //Farewell_ReturnCode = 0x25,

            Error_PacketFormatError = 0x21,
            Error_IllegalStateError = 0x22,
            //Error_UnexpectedError = 0x23,

            //AT_FelicaCommand = 0x101,
            AT_FelicaPreCommand = 0x104,
            AT_FelicaExcommand = 0x105,
            AT_FelicaCommandThuruw = 0x106,
            AT_FelicaCommandPreCommandThuruw = 0x109,
            AT_FelicaCommandExCommandThuruw = 0x10A,

            Update_RequestID = 0x30,
            //Update_SetNetTimeout = 0x81,
            //Update_FelicaSelectInternal = 0x101,
            Update_FelicaSetTimeout = 0x181,
            Update_FelicaSetRetryCount = 0x182,

            //Op_OperateDeviceMessage = 0x25,
            //Op_PlaySoundMessage = 0x81,
            //Op_ReqFelicaOpenRw = 0x101,
            Op_ReqFelicaCloseRw = 0x105
        }

        public enum TcapRespMessageType //(a1+14)
        {
            op00_ResponseFinishedMessage = 0x00,
            op02_ResponseFelicaOpenRwStatusMessage_ResponseFelicaResponseMessage = 0x02,
            op03_ResponseFelicaSelectedDeviceMessage_ResponseFelicaErrorMessage = 0x03,
            op06_ResponseFelicaCloseRwStatusMessage = 0x06,
            op07_ResponseFelicaResponseThrurwMessage = 0x07,
            op08_ResponseFelicaErrorThrurwMessage = 0x08,
            op21_ResponseClientGoodByeMessage_ResponsePacketFormatErrorMessage_ResponseClientHelloMessage = 0x21,
            op22_ResponseClientGoodByeDoneMessage_ResponseIllegalStateErrorMessage_ResponseClientHelloDoneMessage = 0x22,
            op23_ResponseUnexpectedErrorMessage = 0x23,
            op25_ResponseDevicesMessage = 0x25,
            op26_ResponseDeviceResponseMessage_ResponseFeaturesMessage = 0x26,
            op30_ResponseRequestIdMessage = 0x30,
            EmptyPacket = 0xfe,
            Unknown = 0xff
        }

        public class TcapPacket
        {

            TcapPacketType currentType = TcapPacketType.Unknown;
            public List<TcapSubPacket> subPackets { get; } = new List<TcapSubPacket>();

            //暂时还不知道0201头有什么用途，不可传body?
            bool Header0201 = false;
            public TcapPacket(TcapPacketType pktType = TcapPacketType.Unknown, bool Use0201 = false) { currentType = pktType; Header0201 = Use0201; }

            public void AddSubType(TcapPacketSubType subType) => subPackets.Add(new TcapSubPacket(subType));
            public void AddSubType(TcapSubPacket subPacket) => subPackets.Add(subPacket);

            public byte[] Generate()
            {
                var header = new byte[] { 0x02, (Header0201 ? (byte)0x01 : (byte)0x05), (byte)currentType };

                byte[]
                    content = { }, //最后输出的Message Content包
                    length;                   //Message Content包的长度

                foreach (var packets in subPackets) content = content.Concat(packets.Generate()).ToArray();

                length = BitConverter.GetBytes((short)content.Length);
                Array.Reverse(length);
                return header.Concat(length).Concat(content).ToArray();
            }
        }

        public class TcapSubPacket
        {
            byte[] msgBody;
            byte[] paramA = { 0x00, 0x00 };
            byte[] bodyLength { get { var bodySizeByte = BitConverter.GetBytes((short)msgBody.Length); Array.Reverse(bodySizeByte); return bodySizeByte; } }
            TcapPacketSubType currentSubType = TcapPacketSubType.General_UnknownMessage;
            public TcapSubPacket(TcapPacketSubType subType, byte[] body = null) { currentSubType = subType; if (body == null) { msgBody = new byte[] { }; } else msgBody = body; }

            public void setParam(byte[] newParam) { paramA = newParam; }
            public byte[] Generate()
            {
                byte[]
                    content, //最后输出的Message Content包
                             //body = { },               //Body包
                             //bodylength,
                    op;      //Body Op包

                op = BitConverter.GetBytes((short)currentSubType);
                Array.Reverse(op);

                content = new byte[] { op[0], paramA[0], paramA[1], op[1] };
                content = content.Concat(bodyLength).ToArray();
                if (msgBody.Length > 0) content = content.Concat(msgBody).ToArray();

                return content;
            }
        }

        public class TcapMessageRequestBody
        {
            public TcapPacketType pktType = TcapPacketType.Unknown;
            public TcapRespMessageType msgType = TcapRespMessageType.Unknown;
            public byte[] MessageBody;
            public string MessageHex { get { return HexByteArrayExtensionMethods.ToHexString(MessageBody); } }
            public TcapMessageRequestBody(TcapPacketType pktType, TcapRespMessageType msgType, byte[] messageBody)
            {
                this.pktType = pktType;
                this.msgType = msgType;
                MessageBody = messageBody;
            }
        }

        public class TcapMessageRequest
        {
            byte[] data;

            public List<TcapMessageRequestBody> messages { get; private set; } = new List<TcapMessageRequestBody>();
            public TcapPacketType pktType { get; private set; } = TcapPacketType.Unknown;

            public TcapMessageRequest(byte[] data) { this.data = data; Parse(); }
            public void Parse()
            {
                if (data.Length <= 0)
                {
                    pktType = TcapPacketType.EmptyPacket;
                    return;
                }

                using (var memStream = new MemoryStream(data))
                using (var reader = new BinaryReader(memStream))
                {
                    if (reader.ReadByte() == 0x02 && reader.ReadByte() == 0x05)
                    {
                        pktType = (TcapPacketType)reader.ReadByte();
                        var MessageLength = reader.ReadBytes(2);
                        Array.Reverse(MessageLength);
                        var MessageLengthInt = BitConverter.ToInt16(MessageLength, 0);
                        var MessageBody = new byte[MessageLengthInt];
                        reader.Read(MessageBody, 0, MessageLengthInt);
                        //LogFile.PrintLog("ThincaParse", "Header Msg Size:" + MessageLengthInt.ToString());

                        ParseMessageBody(MessageBody);
                    }
                }
            }

            private void ParseMessageBody(byte[] body)
            {
                using (var memStream = new MemoryStream(body))
                using (var reader = new BinaryReader(memStream))
                {
                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        var MessageOp = reader.ReadBytes(4);
                        var msgType = (TcapRespMessageType)MessageOp[3];
                        
                        var MessageLength = reader.ReadBytes(2);
                        Array.Reverse(MessageLength);
                        var MsgLengthInt = BitConverter.ToInt16(MessageLength, 0);
                        //LogFile.PrintLog("ThincaParse", "Got MsgLength:" + MsgLengthInt.ToString());
                        if (MsgLengthInt > 0)
                        {
                            var MsgBody = new byte[MsgLengthInt];
                            reader.Read(MsgBody, 0, MsgLengthInt);

                            TcapMessageRequestBody newMessage = new TcapMessageRequestBody(pktType, msgType, MsgBody);
                            messages.Add(newMessage);
                        }
                    }
                }
            }
        }

        public class ReceiptInfo
        {
            public enum receiptTypeEnum : int
            {
                Payment = 0,            //2,3,4,5,6,7,8,9
                Charge = 1,             //2,5,6,7,9
                Alarm = 2,              //2,3,4,5,6,7
                CenterCommResult = 3,   //2
                ReceiptPayment = 4,     //2
                BalanceInquire = 5,     //2,7,8,9
                VoidPayment = 7,        //3,4,5,6,8,9
                IntermidiateSales = 8,  //4
                VoidCharge = 10,        //5,6,9
                PointChargeWaon = 11,   //6
                ReceiptRefuindWaon = 12 //6
            }


            public string ReceiptType = ((int)receiptTypeEnum.Payment).ToString();
            public long SettledAmount = 100;
            public long Balance = 101;

            public string PaseliDealNo = "1145141919810893";
            public string ApprovalCode = "1";
            public string UpperTerminalId = "11445514";
            public string DealYMD = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
            public string CardNo = "01391144551419198100";
            public long ReceiptDest = 1;

        }

        public static class Activation
        {
            public class endpointUriClass
            {
                public endpointUriClass(string URI) { uri = URI; }
                public string uri = "";
            }

            public class endpointClass
            {
                public endpointUriClass terminals = new endpointUriClass(miniThinca.config.terminalEndpoint);
                public endpointUriClass statuses = new endpointUriClass(miniThinca.config.statusesEndpoint);
                public endpointUriClass sales = new endpointUriClass(miniThinca.config.salesEndpoint);
                public endpointUriClass counters = new endpointUriClass(miniThinca.config.countersEndpoint);
            }

            public class intervalClass
            {
                public int checkSetting = 3600;
                public int sendStatus = 3600;
            }

            public class initSettingClass
            {
                public endpointClass endpoints = new endpointClass();
                public intervalClass intervals = new intervalClass();
                public string settigsType = "AmusementTerminalSettings";
                //00:Authed 10:Available 11:Pause 12:Maintainance 80:NotAvailable 90:Removed
                public string status = "10";
                //"111122223333444455556666777788"
                public string terminalId = "AliceNet_ThincaClient_00000001";
                public string version = "2023-01-01T12:34:56";
                public List<int> availableElectronicMoney = miniThinca.config.availableEMoney;
                public bool cashAvailability = true;
                public int productCode = 1001;
                //public int availableElectronicMoney = 0x5B; //AimePay

            }
            public class ActivateClass
            {
                public string certificate = "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";
                public initSettingClass initSettings = new initSettingClass();

            }
        }

        public static class ClientIoOperation
        {
            public class ClientIo
            {
                public enum deviceTypeEnum : int
                {
                    MessageEvent = 1,
                    SoundEvent = 2,
                    LedEvent = 3,
                    AmountEvent = 16, //deviceNumber == 1,screen == true
                    EnableCancelEvent = 32,
                    ClientIoSaveDealNumberEvent = 48,
                    ProgressEvent = 64 //actionType == 0(Start?),1(End?)
                }
                public deviceTypeEnum deviceType = deviceTypeEnum.AmountEvent;
                public int deviceNumber;
                public int actionType;
                public int[] sendData;
                public bool screen = false;

                public ClientIo(deviceTypeEnum deviceType, int deviceNumber, int actionType, int[] sendData, bool screen)
                {
                    this.deviceType = deviceType;
                    this.deviceNumber = deviceNumber;
                    this.actionType = actionType;
                    this.sendData = sendData;
                    this.screen = screen;
                }
            }
            public class MessageEventIo : ClientIo
            {
                public MessageEventIo(int deviceNumber, int actionType, short brandType, short messageId, short timeout, bool screen = false)
                    : base(deviceTypeEnum.MessageEvent, deviceNumber, actionType, null, screen)
                {
                    var brandTypeByte = returnReversedByte(brandType);
                    var messageIdByte = returnReversedByte(messageId);
                    var timeoutByte = returnReversedByte(timeout);

                    sendData = new int[] {
                    brandTypeByte[0], brandTypeByte[1],
                    messageIdByte[0], messageIdByte[1],
                    timeoutByte[0], timeoutByte[1]
                };
                }
            }

            public class SoundEventIo : ClientIo
            {
                public SoundEventIo(int actionType, short brandType, byte id, short timeout)
                    : base(deviceTypeEnum.SoundEvent, 0, actionType, null, false)
                {

                    var brandTypeByte = returnReversedByte(brandType);
                    var timeoutByte = returnReversedByte(timeout);

                    sendData = new int[]
                    {
                    brandTypeByte[0], brandTypeByte[1],
                    id,
                    timeoutByte[0], timeoutByte[1]
                    };
                }
            }

            public class LedEventIo : ClientIo
            {
                //deviceNumber:0(FFFF0000),1(FF00FF00),2(FF0000FF),3(0),4(FFFFFFFF)
                public LedEventIo(int deviceNumber, int actionType, short timeout, short param2, short param3, short param4)
                    : base(deviceTypeEnum.LedEvent, deviceNumber, actionType, null, false)
                {
                    var timeoutByte = returnReversedByte(timeout);
                    var param2Byte = returnReversedByte(param2);
                    var param3Byte = returnReversedByte(param3);
                    var param4Byte = returnReversedByte(param4);
                    sendData = new int[]
                    {
                    0,0,
                    timeoutByte[0],timeoutByte[1],
                    param2Byte[0],param2Byte[1],
                    param3Byte[0],param3Byte[1],
                    param4Byte[0],param4Byte[1]
                    };
                }
            }
            public class AmountEventIo : ClientIo
            {
                public AmountEventIo(int deviceNumber, int actionType, short brandType, byte code2, int payment, short code5)
                    : base(deviceTypeEnum.AmountEvent, deviceNumber, actionType, null, true)
                {
                    var brandTypeByte = returnReversedByte(brandType);
                    var paymentByte = returnReversedByte(payment);
                    var code5Byte = returnReversedByte(code5);
                    sendData = new int[]
                    {
                    brandTypeByte[0],brandTypeByte[1],
                    code2,
                    paymentByte[0],paymentByte[1],paymentByte[2],paymentByte[3],
                    paymentByte[0],paymentByte[1],paymentByte[2],paymentByte[3],
                    code5Byte[0],code5Byte[1]
                    };
                }
            }
            public class EnableCancelIo : ClientIo
            {
                public EnableCancelIo(int actionType) :
                    base(deviceTypeEnum.EnableCancelEvent, 0, actionType, new int[] { }, false)
                {

                }
            }
            public class SaveDealNumberIo : ClientIo
            {
                public SaveDealNumberIo(short brandID, string DealNumber) :
                    base(deviceTypeEnum.ClientIoSaveDealNumberEvent, 0, 0, null, false)
                {
                    var brandidByte = returnReversedByte(brandID);
                    sendData = new int[]
                    {
                    brandidByte[0],brandidByte[1]
                    };
                    var dealNumByte = Encoding.UTF8.GetBytes(DealNumber);
                    foreach (var deal in dealNumByte) sendData.Append(deal);
                }
            }
        }

        public static class SecurityMessage
        {
            public class AdditionalSecurityMessage
            {
                public string UniqueCode = "ACAE01A9999";
                public string PassPhrase = "ase114514";
                public string ServiceBranchNo = "14";
                public string GoodsCode = "0990";

                public List<int> EMoneyCode = miniThinca.config.availableEMoney;
                public List<int> EMoneyResultCode = miniThinca.config.availableEMoneyResultCode;

                public int FloorLimit = 10000;          //ThincaResult + 104
                public string AuthorizeErrorCode = "0"; //ThincaResult + 120
                public int AuthorizeStatus = 1;        //ThincaResult + 160
            }

            public class AdditionalSecurityMessage_em2
            {
                public string TermSerial = "ACAE01A9999";
                public string ServiceBranchNo = "2";

                public List<int> EMoneyCode = miniThinca.config.availableEMoney;
                public List<string> URL = miniThinca.config.availableEMoneyUrl;
            }

            public class AdditionalSecurityMessage_AuthorizeSales
            {
                public string ServiceBranchNo = "2";
                public string GoodsCode = "0990";

                //0:付款 5:余额查询
                public string ReceiptType = "0";
                public long SettledAmount = 100;
                public long Balance = 101;

                public string PaseliDealNo = "1145141919810893";
                public string ApprovalCode = "1";
                public string UpperTerminalId = "11445514";
                public string DealYMD = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                public string CardNo = "01391144551419198100";

                //必须为1才能成功，2好像是错误状态
                public int ReceiptDest = 1;


                public int TotalPointOfPastYear = 100;
                public int TotalPointOfPreviousYear = 101;
                public int BalanceLimit = 10000;
                public int ChargeLimitPerOnce = 10000;
            }

            public class AdditionalSecurityMessage_BalanceInquiry
            {
                public string ServiceBranchNo = "2";
                public string GoodsCode = "0990";

                //0:付款 5:余额查询
                public string ReceiptType = "5";
                public string UpperTerminalId = "11445514";
                public string DealYMD = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                public long Balance = 101;
                public string CardNo = "01391144551419198100";
                //必须为1才能成功，2好像是错误状态
                public int ReceiptDest = 1;


                public int TotalPointOfPastYear = 100;
                public int TotalPointOfPreviousYear = 101;
                public int BalanceLimit = 10000;
                public int ChargeLimitPerOnce = 10000;
            }

            public static string ReturnOperateEntityXml_initAuth(string serviceName, string AdditionalSecurityInformation)
            {
                System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                var root = doc.CreateElement("response");
                root.SetAttribute("service", serviceName);
                doc.AppendChild(root);

                var status = doc.CreateElement("status");
                status.SetAttribute("value", "1");
                root.AppendChild(status);

                var userdata = doc.CreateElement("userdata");
                root.AppendChild(userdata);

                var properties = doc.CreateElement("properties");
                userdata.AppendChild(properties);

                var ResultCode = doc.CreateElement("longValue");
                ResultCode.SetAttribute("name", "ResultCode");
                ResultCode.SetAttribute("value", "0");
                properties.AppendChild(ResultCode);

                var ResultCodeExtended = doc.CreateElement("longValue");
                ResultCodeExtended.SetAttribute("name", "ResultCodeExtended");
                ResultCodeExtended.SetAttribute("value", "0");
                properties.AppendChild(ResultCodeExtended);

                //StringValue
                var addSecNode = doc.CreateElement("stringValue");
                addSecNode.SetAttribute("name", "AdditionalSecurityInformation");
                addSecNode.SetAttribute("value", AdditionalSecurityInformation);
                properties.AppendChild(addSecNode);

                var CenterResultCode = doc.CreateElement("stringValue");
                CenterResultCode.SetAttribute("name", "CenterResultCode");
                CenterResultCode.SetAttribute("value", "0");
                properties.AppendChild(CenterResultCode);

                return doc.InnerXml;
            }

            public static string ReturnOperateEntityXml_AuthorizeSales(string serviceName, string AdditionalSecurityInformation, string SeqNumber = "1", string balance = "101", string setAmount = "100", string account = "12341234123412341234")
            {
                System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                var root = doc.CreateElement("response");
                root.SetAttribute("service", serviceName);
                doc.AppendChild(root);

                var status = doc.CreateElement("status");
                status.SetAttribute("value", "1");
                root.AppendChild(status);

                var userdata = doc.CreateElement("userdata");
                root.AppendChild(userdata);

                var properties = doc.CreateElement("properties");
                userdata.AppendChild(properties);

                //longValue
                var ResultCode = doc.CreateElement("longValue");
                ResultCode.SetAttribute("name", "ResultCode");
                ResultCode.SetAttribute("value", "0");
                properties.AppendChild(ResultCode);

                var ResultCodeExtended = doc.CreateElement("longValue");
                ResultCodeExtended.SetAttribute("name", "ResultCodeExtended");
                ResultCodeExtended.SetAttribute("value", "0");
                properties.AppendChild(ResultCodeExtended);

                var PaymentCondition = doc.CreateElement("longValue");
                PaymentCondition.SetAttribute("name", "PaymentCondition");
                PaymentCondition.SetAttribute("value", "0");
                properties.AppendChild(PaymentCondition);

                var SequenceNumber = doc.CreateElement("longValue");
                SequenceNumber.SetAttribute("name", "SequenceNumber");
                SequenceNumber.SetAttribute("value", SeqNumber);
                properties.AppendChild(SequenceNumber);

                var TransactionType = doc.CreateElement("longValue");
                TransactionType.SetAttribute("name", "TransactionType");
                TransactionType.SetAttribute("value", "1");
                properties.AppendChild(TransactionType);

                //currencyValue
                var Balance = doc.CreateElement("currencyValue");
                Balance.SetAttribute("name", "Balance");
                Balance.SetAttribute("value", balance);
                properties.AppendChild(Balance);

                var SettledAmount = doc.CreateElement("currencyValue");
                SettledAmount.SetAttribute("name", "SettledAmount");
                SettledAmount.SetAttribute("value", setAmount);
                properties.AppendChild(SettledAmount);

                //StringValue
                var addSecNode = doc.CreateElement("stringValue");
                addSecNode.SetAttribute("name", "AdditionalSecurityInformation");
                addSecNode.SetAttribute("value", AdditionalSecurityInformation);
                properties.AppendChild(addSecNode);

                var rand = new Random();

                var approvalCode = doc.CreateElement("stringValue");
                approvalCode.SetAttribute("name", "ApprovalCode");
                approvalCode.SetAttribute("value", rand.Next(0, int.MaxValue).ToString());
                properties.AppendChild(approvalCode);

                var accNumber = doc.CreateElement("stringValue");
                accNumber.SetAttribute("name", "AccountNumber");
                accNumber.SetAttribute("value", account);
                properties.AppendChild(accNumber);

                var CenterResultCode = doc.CreateElement("stringValue");
                CenterResultCode.SetAttribute("name", "CenterResultCode");
                CenterResultCode.SetAttribute("value", "0");
                properties.AppendChild(CenterResultCode);

                return doc.InnerXml;
            }

        }
    }
}

