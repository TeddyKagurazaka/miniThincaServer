using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Windows.Input;
using System.Xml;
using AliceNet_Core_Redesigned.Helper;
using Newtonsoft.Json;
using WebSocketSharp;
using static AliceNet_Core_Redesigned.Module.ThincaClient.TcapPacket;

namespace AliceNet_Core_Redesigned.Module
{
    internal class ThincaClient
    {
        public ThincaClient() { }

        public class TcapMessageBody
        {
            public TcapPacketType pktType = TcapPacketType.Unknown;
            public TcapParser.TcapRespMessageType msgType = TcapParser.TcapRespMessageType.Unknown;
            public byte[] MessageBody;
            public string MessageHex { get { return HexByteArrayExtensionMethods.ToHexString(MessageBody); } }  
            public TcapMessageBody(TcapPacketType pktType, TcapParser.TcapRespMessageType msgType, byte[] messageBody)
            {
                this.pktType = pktType;
                this.msgType = msgType;
                MessageBody = messageBody;
            }
        }
        public class TcapParser
        {
            //PacketFormatError(op+prm 4byte)-(2byte length)-(2byte errorType)
            //errorType 40221(42)
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
            byte[] data;
            //public byte[] MessageBody { get; private set; }
            //public byte[] InnerBody { get; private set; }
            public List<TcapMessageBody> messages { get; private set; } = new List<TcapMessageBody>();
            
            public TcapPacketType pktType { get; private set; } = TcapPacketType.Unknown;
            //publicTcapRespMessageType msgType = TcapRespMessageType.Unknown;
            public TcapParser(byte[] data) { this.data = data; Parse(); }
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
                        LogFile.PrintLog("ThincaParse", "Got Header Method:" + pktType.ToString());
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
                        LogFile.PrintLog("ThincaParse", "Got Message Op:" + msgType);
                        //LogFile.PrintLog("ThincaParse","Raw Message Type:" + HexByteArrayExtensionMethods.ToHexString(MessageOp));

                        var MessageLength = reader.ReadBytes(2);
                        Array.Reverse(MessageLength);
                        var MsgLengthInt = BitConverter.ToInt16(MessageLength, 0);
                        //LogFile.PrintLog("ThincaParse", "Got MsgLength:" + MsgLengthInt.ToString());
                        if (MsgLengthInt > 0)
                        {
                            var MsgBody = new byte[MsgLengthInt];
                            reader.Read(MsgBody, 0, MsgLengthInt);
                            if (msgType == TcapRespMessageType.op21_ResponseClientGoodByeMessage_ResponsePacketFormatErrorMessage_ResponseClientHelloMessage && pktType == TcapPacketType.Error)
                                LogFile.PrintLog("ThincaParse", "Got MsgBody:" + Encoding.UTF8.GetString(MsgBody));
                            else
                                LogFile.PrintLog("ThincaParse", "Got MsgBody:" + HexByteArrayExtensionMethods.ToHexString(MsgBody));

                            TcapMessageBody newMessage = new TcapMessageBody(pktType, msgType, MsgBody);
                            messages.Add(newMessage);
                        }
                    }
                }
            }
        }
        public class TcapPacket
        {
            //v22[2] = header?(0x02 0x05)
            //v22[3] = Method?

            //包格式
            //  header(3byte)+后面所有部分的Length(2byte)
            //  +mainCode(HI)+SubCode(HI)+SubCode(LO)+mainCode(Lo)+BodyLength(2byte)+Body(?)

            //Header第一位必然为0205，设置0201会引发报错4022280

            //Header第三位:
            //       0x01:Handshake(只能出现一次 除非0x201)
            //       0x02:Farewell(只能出现一次 除非0x201)
            //       0x03:Error
            //       0x04:ApplicationDataTransfer
            //       0x05:UpdateEntity
            //       0x06:OperateEntity
            //       其他:默认包


            //HandShake包要求: Content部分第四位必须为0x81(AccpetPacket?) 而且Content第二第三位(作为a2+12赋值)为0x00 否则回落默认包检测(必然4002144)
            //默认包要求: Content第一第四为组成0x01 第二第三位为0x00 否则4002144错误

            //Content包 (a2+8) (a2+10) (a2+12) (a2+14) 例如*(unsigned __int8 *)(a2 + 14) | (unsigned __int16)(*(_WORD *)(a2 + 8) << 8);相当于用第一位第四位组成数字

            //Content: 第一第四位组成MessageType
            //      Default(其他Type 或该MessageType parse失败时回落):
            //          0x00(0x00 0x00):RequestMessage  (a3 == 0 否则4002145)
            //          0x01(0x00 0x01):RequestWarningMessage   (不Parse包)
            //          其他组合:RequestUnknownMessage  (不Parse包)
            //      Handshake(0x01):
            //          0x23(0x00 0x23):RequestMessage (a3 == 0 否则4002145) (不可传body)
            //          0x24(0x00 0x24):RequestMessage (a3 == 0 否则4002145) (不可传body)    (paramA必须为0x00否则无法验证)
            //          0x81(0x00 0x81):RequestAcceptMessage (a2 > 0 , a3 >= 3 否则4002145) (body最少3byte) (头必须为0205否则无法验证导致4002144) (body只能是02 05 00 增加entry会4022147)
            //      Farewell(0x02):
            //          0x23(0x00 0x23):RequestServerGoodByeMessage (a3 == 0 否则4022145) (不可传body)
            //          0x24(0x00 0x24):RequestServerGoodByeDoneMessage (a3 == 0 否则4022145) (不可传body)
            //          0x25(0x00 0x25):RequestReturnCodeMessage (a2 > 0 , a3 == 4 否则4002145) (body必须为4byte)
            //      Error(0x03):    (格式没看懂)
            //          0x21(0x00 0x21):RequestPacketFormatErrorMessage
            //          0x22(0x00 0x22):RequestIllegalStateErrorMessage
            //          0x23(0x00 0x23):RequestUnexpectedErrorMessage
            //      AppDataTransfer(0x04):   
            //          0x101(0x01 0x01):RequestFelicaCommandMessage   (a2 > 0 , 0 < a3 < 0xFF 否则4022145) (body必须在255byte内)
            //          0x104(0x01 0x04):RequestFelicaPrecommandMessage (a2 > 0 , 0 < a3 < 0xFF 否则4022145) (body必须在255byte内)
            //          0x105(0x01 0x05):RequestFelicaExcommandMessage  (a2 > 0 , 3 < a3 < 0xFF 否则4022145) a2长度2位byte (body必须在3~255byte内)
            //          0x106(0x01 0x06):RequestFelicaCommandThrurwMessage (a2 > 0 , 3 < a3 < 0xFF 否则4022145) a2长度2位byte (body必须在3~255byte内)
            //          0x109(0x01 0x09):RequestFelicaPrecommandThrurwMessage  (a2 > 0 , 4 < a3 < 0xFF 否则4022145) a2长度3位byte (body必须在4~255byte内)
            //          0x10A(0x01 0x0A):RequestFelicaExcommandThrurwMessage    (a2 > 0 , 4 < a3 < 0xFF 否则4022145) a2长度3位byte (body必须在4~255byte内)
            //      UpdateEntity(0x05): (发送后会随下一个请求返回）
            //          0x30(0x00 0x30):RequestRequestIdMessage (a2 > 0, a3 = 2 否则4002145） (body必须2byte 2byte作为int32直接parse) -> (他只会回传你设置的a2)
            //          0x81(0x00 0x81):RequestSetNetworkTimeoutMessage (a2 > 0 , a3 == 4 否则4002145)    (body必须4byte)
            //          0x101(0x01 0x01):RequestFelicaSelectInternalMessage (a3 == 0 否则4022145) (不可传body)
            //          0x181(0x01 0x81):RequestFelicaSetTimeoutMessage (a2 > 0 , a3 == 4 否则4002145)    (body必须4byte)
            //          0x182(0x01 0x82):RequestFelicaSetRetryCountMessage (a2 > 0 , a3 == 4 否则4002145) (body必须4byte)
            //      OperateEntity(0x06): (发送后会随下一个请求返回）
            //          0x25(0x00 0x25):RequestOperateDeviceMessage body格式参考下面: 需要设置SubCode = 00 01否则报错 (统一用GenerateOpCmdPacket(COMMAND,package)生成body)
            //              (Subcode的设置参照Handshake的op25_ResponseDevicesMessage \
            //                  00 01->Generic Client
            //                  00 02->Generic Status
            //                  00 03->Generic Option
            //                  00 04->Felica R/W
            //                  00 05->Generic R/W Event  :
            //                      OpCmd(7+ byte) (4byte ?)(2byte size)(content)
            //                  00 06->Generic R/W Status :
            //                      OpCmd得有东西，但是不Handle
            //                  00 07->Generic R/W Option :
            //                      OpCmd(6byte) (code1 2byte)(code2 2byte)(code3 2byte) (code3可选0(获取?),1(需要code1 code2 ->FelicaCommand 有输出),2(需要code1 code2 -> FelicaCommand 无输出?))
            //                  00 08->Generic NFC RW (参照下面)
            //              RequestOperateDeviceMessage 1byte CMD长度 + COMMAND + 00 00 + 2byte (Payload长度+2) + 2byte Payload长度 + Payload 如果没有Payload设置4byte为00 00 00 00
            //                  Generic Client(00 01)可用COMMAND:
            //                      REQUEST:返回当前参数XML
            //                      CURRENT:设置参数，设置内容参照Current的Payload使用ReturnOperateEntityXml() 无Payload会返回 00 00 00 02 00 01
            //                      RESULT:和CURRENT一致
            //                      CANCEL:?
            //                      TIMESTAMP: 不吃参数,返回TIMESTAMP
            //                      WAIT:  Sleep指定时间 (8byte)(8byte Sleep MillSec)
            //                      UNIXTIME: 不吃参数,返回UNIXTIME
            //                      UNIXTIMEWAIT: Sleep指定时间 (8byte(Int64?) 开始wait时间 unix timestamp)(2byte wait长度) 
            //                      STATUS: 会引发 02 Illegal generic client
            //                          STATUS时(SubCode)0x01->illegal general client
            //                                          0x02->(general status?)0x378e0 (length)(STATUS)(00 00)(00 02)(00 01)
            //                                          0x03->(general option?)0x375a0 (length)(STATUS) -> thincapayment::ThincaPaymentImple::OnClientIoEventOccurred
            //                                                  0x03时,Payload前加8个00 byte，后跟ClientIo类转Json
            //
            //                  Generic NFC RW(00 08)可用COMMAND: (参照aime_rw_adapterMD.dll)
            //                      OPEN_RW:3byte (code1 0or1)(code2 2byte)
            //                      CLOSE_RW:(不要求body)
            //                      TARGET_DETECT:4byte (code1 4byte) (MaxWaitTime?)
            //                      APDU_COMMAND:3+byte (code1)(code2 2byte) (code2 + 3 <= length)
            //                      FELICA_COMMAND:7+byte (code1 4byte)(code2 1byte)(code3 3byte) (code3 + 7 <= length)
            //                      OPTION:6byte (code1 4byte)(code2 2byte) (code2 > 0时触发flag)
            //
            //
            //          0x81(0x00 0x81):RequestPlaySoundMessage(不Check包)    (subcode -> sound id ?)
            //          0x101(0x01 0x01):RequestFelicaOpenRwRequestMessage(a3 == 0)     (不可传body)   SubCode必须为00 04
            //          0x105(0x01 0x05):RequestFelicaCloseRwRequestMessage(a3 == 0)    (不可传body)   SubCode必须为00 04
            //          

            //          第二第三位 会在初始化Message的时候用于赋值
            //          第五第六为必须大于0x00(否则不触发长度赋值)
            //          第七位 RequestAccpetMessage验证时作为a2带参，必须不为0

            /* RequestOperateDeviceMessage->CURRENT payload (你妈的 居然能用)
             * 默认AdditionalSecurity
                    //stage2:{"UniqueCode":"ACAE01A9999","PassPhrase":"ase114514","ServiceBranchNo":14,"GoodsCode":"0990"} -> EMoneyCode,EMoneyResultCode
                    //emstage2:{"ServiceBranchNo":2,"TermSerial":"ACAE01A9999"} -> EMoneyCode,URL

            var opCmdParam =
                "<response service=\"DirectIO\">" +
                    "<status value=\"0\" />" +
                    "<userdata>" +
                        "<properties>" +
                                                "<boolValue name=\"TrainingMode\" value=\"true\" />" +

                            "<longValue name=\"ResultCode\" value=\"0\" />" +
                            "<longValue name=\"ResultCodeExtended\" value=\"0\" />" +
                                                 "<longValue name=\"PaymentCondition\" value=\"0\" />" +
                                                 "<longValue name=\"SequenceNumber\" value=\"0\" />" +
                                                  "<longValue name=\"TransactionType\" value=\"0\" />" +

                                                  "<currencyValue name=\"Balance\" value=\"0\" />" +
                                                  "<currencyValue name=\"SettledAmount\" value=\"0\" />" +

                                                  "<stringValue name=\"AccountNumber\" value=\"114514\" />" +
                              "<stringValue name=\"AdditionalSecurityInformation\" value=\"{0}\" />" +
                                            "<stringValue name=\"ApprovalCode\" value=\"0\" />" +
                                            "<stringValue name=\"CardCompanyID\" value=\"0\" />" +
                                            "<stringValue name=\"CenterResultCode\" value=\"0\" />" +
                                            "<stringValue name=\"DailyLog\" value=\"0\" />" +
                                            "<stringValue name=\"PaymentDetail\" value=\"0\" />" +
                                            "<stringValue name=\"SlipNumber\" value=\"0\" />" +
                                            "<stringValue name=\"TransactionNumber\" value=\"0\" />" +

                        "</properties>" +
                    "</userdata>" +
                "</response>";
             */

            //  a3应该是Param3长度,a2是Param3
            //  第一个包不是0x23(Farewell/UnexpectedError?)

            //  Error包:
            //      4040001 TCAP packet overflow
            //      4040002 TCAP message overflow
            //      4050002 UnexpectedError
            //      4022304~4050002 UnknownError
            //
            //      4022140 Header有问题(0x02 0x05) or Param1不能大于6
            //      4022142 Parse失败(header不符合要求/重复Handshake Farewell)
            //      4022143 Header有问题
            //      4022144 body解析失败(op/bodylength?) | op不属于该PacketType类别 | 回落GeneralPacket检测的时候op不为0x01且paramLo不为0x00
            //      4022145 body格式不符合要求
            //      4022146 body顺序有问题
            //          (Handshake必须0205 0x23->0x81->0x24->0x00)
            //          (Farewell必须0205 0x23 -> 0x25 -> 0x24)
            //          (Error必须 0205 0x21/0x22/0x23)
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

            TcapPacketType currentType = TcapPacketType.Unknown;
            public List<TcapSubPacket> subPackets { get; } = new List<TcapSubPacket>();

            //暂时还不知道0201头有什么用途，不可传body?
            bool Header0201 = false;
            public TcapPacket(TcapPacketType pktType = TcapPacketType.Unknown, bool Use0201 = false) { currentType = pktType; Header0201 = Use0201; }

            public void AddSubType(TcapSubPacket.TcapPacketSubType subType) => subPackets.Add(new TcapSubPacket(subType));
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
            byte[] msgBody;
            byte[] paramA = { 0x00, 0x00 };
            byte[] bodyLength { get { var bodySizeByte = BitConverter.GetBytes((short)msgBody.Length); Array.Reverse(bodySizeByte); return bodySizeByte; } }
            TcapPacketSubType currentSubType = TcapPacketSubType.General_UnknownMessage;
            public TcapSubPacket(TcapPacketSubType subType, byte[] body = null) { currentSubType = subType; if (body == null) { msgBody = new byte[] { }; } else msgBody = body; }

            public void setParam(byte[] newParam) { paramA = newParam; }
            public byte[] Generate()
            {
                //ParamB: ParamC的长度?
                //0201的时候只允许一个包存在（也可能必须是最后一个包 PacketHeaderCheck会强制包头为0x205 否则4022140
                //他一个大包允许串n个小包(00 00 00 21 00 00 接 00 00 00 00 25 00 00)

                //if (paramB[1] == 0x00) { content = new byte[] { op[1], paramA[1], paramA[0], op[0], paramB[0], paramB[1] }; }
                //else
                //{
                //    content = new byte[] { op[1], paramA[0], paramA[1], op[0], paramB[0], paramB[1] };
                //    content = content.Concat(paramC).ToArray();
                //}

                byte[]
                    content, //最后输出的Message Content包
                             //body = { },               //Body包
                             //bodylength,
                    op;      //Body Op包

                op = BitConverter.GetBytes((short)currentSubType);
                Array.Reverse(op);

                switch (currentSubType)
                {
                    case TcapPacketSubType.General_Message:
                        //paramA[0] = 0x01;
                        //paramA[1] = 0x02;
                        break;
                    default:
                        break;
                }

                //bodylength = BitConverter.GetBytes((short)body.Length);
                //Array.Reverse(bodylength);

                content = new byte[] { op[0], paramA[0], paramA[1], op[1] };
                content = content.Concat(bodyLength).ToArray();
                if (msgBody.Length > 0) content = content.Concat(msgBody).ToArray();

                return content;
            }
        }

        public class ReceiptInfo
        {
            public enum brandType
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
        public class AdditionalSecurityMessage
        {
            ////type 1=bool 2=str 3=int 0=null

            public string UniqueCode = "ACAE01A9999";
            public string PassPhrase = "ase114514";
            public string ServiceBranchNo = "14";
            public string GoodsCode = "0990";

            public List<int> EMoneyCode = new List<int>() { 8 };
            public List<int> EMoneyResultCode = new List<int>() { 1 };
            public List<string> URL = new List<string>() { "http://192.168.31.201/thinca/emoney/paseli/" };

            public int TotalPointOfPastYear = 100;
            public int TotalPointOfPreviousYear = 101;
            public int BalanceLimit = 10000;
            public int ChargeLimitPerOnce = 10000;
            public string CardType = "1";
            public string IDm = "1111";

            public int FloorLimit = 10000;          //ThincaResult + 104
            public string AuthorizeErrorCode = "0"; //ThincaResult + 120
            public int AuthorizeStatus = 1;        //ThincaResult + 160

            //加了会把amd搞死
            //public string KyouzanFlg = "0";
            //public string SuspensionProcCode = "0";


            //public ReceiptInfo ReceiptInfo = new ReceiptInfo();
            //amd:投币成功需要DealResult=4(this->m_status)
            //TerminalStatusCode == 2

            //Xml(a1+208):
            //TrainingMode: a1 + 44
            //ResultCode: a1 + 12
            //ResultCodeExtended: a1 + 13
            //PaymentCondition: a1 + 14
            //SequenceNumber: a1 + 15
            //TransactionType: a1 + 16
            //Balance: a1 + 18
            //SettledAmount: a1 + 17
            //AccountNumber: a1 + 80
            //AdditionalSecurityInformation: a1 + 120
            //ApprovalCode: a1 + 160
            //CardCompanyID: a1 + 200
            //CenterResultCode: a1 + 240
            //DailyLog: a1 + 280
            //PaymentDetail: a1 + 320
            //SlipNumber: a1 + 360
            //TransactionNumber: a1 + 400

            /*enum Thincacloud::ThincaPayment::ThincaStatus, copyof_1358, width 4 bytes
            FFFFFFFF THINCA_STATE_UNINITIALIZED  = 0
            FFFFFFFF THINCA_STATE_IDLE  = 1
            FFFFFFFF THINCA_STATE_RUNNING  = 2
            FFFFFFFF THINCA_STATE_SELECT_CARD  = 3
            FFFFFFFF THINCA_STATE_SELECT_BUTTON  = 4
            FFFFFFFF THINCA_STATE_INPUT_PIN  = 5
            FFFFFFFF THINCA_STATE_NOTIFY_WAON_ISSUER_ID  = 6
            FFFFFFFF THINCA_STATE_DUMB_FELICA  = 7
            FFFFFFFF THINCA_STATE_APDO  = 8
            */

            /* enum Thincacloud::ThincaMethod
            FFFFFFFF THINCA_METHOD_INIT_AUTH_TERM  = 1010001h
            FFFFFFFF THINCA_METHOD_REMOVE_TERM  = 1020001h
            FFFFFFFF THINCA_METHOD_CHECK_BRANDS  = 1030001h
            FFFFFFFF THINCA_METHOD_CHECK_DEAL  = 1040001h
            FFFFFFFF THINCA_METHOD_CHECK_LAST_TRAN  = 1040001h
            FFFFFFFF THINCA_METHOD_AUTH_CHARGE_TERM  = 1050001h
            FFFFFFFF THINCA_METHOD_AUTH_DEPOSIT_TERM  = 1050001h
            FFFFFFFF THINCA_METHOD_CLOSE_SALES  = 1060001h
            FFFFFFFF THINCA_METHOD_DAILY_CLOSE_SALES  = 1060002h
            FFFFFFFF THINCA_METHOD_TOTALIZE_SALES  = 1070001h
            FFFFFFFF THINCA_METHOD_INTERMEDIATE_SALES  = 1070001h
            FFFFFFFF THINCA_METHOD_PAYMENT  = 2010001h
            FFFFFFFF THINCA_METHOD_AUTHORIZE_SALES  = 2010001h
            FFFFFFFF THINCA_METHOD_PAYMENT_IN_FULL_BALANCE  = 2010002h
            FFFFFFFF THINCA_METHOD_AUTHORIZE_SALES_BALANCE  = 2010002h
            FFFFFFFF THINCA_METHOD_PAYMENT_SPECIFIED_CARD_NUMBER  = 2010003h
            FFFFFFFF THINCA_METHOD_PAYMENT_POINT_ADDITION  = 2010004h
            FFFFFFFF THINCA_METHOD_REFUEL_PAYMENT  = 2010005h
            FFFFFFFF THINCA_METHOD_PAYMENT_JUST_CHARGE  = 2010006h
            FFFFFFFF THINCA_METHOD_REFUEL_SPECIFIED_AMOUNT  = 2010007h
            FFFFFFFF THINCA_METHOD_CHARGE  = 2020001h
            FFFFFFFF THINCA_METHOD_CASH_DEPOSIT  = 2020001h
            FFFFFFFF THINCA_METHOD_CHARGE_SPECIFIED_CARD_NUMBER  = 2020002h
            FFFFFFFF THINCA_METHOD_CHARGE_BACK  = 2020003h
            FFFFFFFF THINCA_METHOD_POINT_CHARGE  = 2020004h
            FFFFFFFF THINCA_METHOD_VOID_PAYMENT  = 2030001h
            FFFFFFFF THINCA_METHOD_VOID_SALES_PREPAID  = 2030001h
            FFFFFFFF THINCA_METHOD_VOID_SALES_POSTPAID  = 2030002h
            FFFFFFFF THINCA_METHOD_VOID_PAYMENT_SPECIFIED_AMOUNT  = 2030003h
            FFFFFFFF THINCA_METHOD_VOID_PAYMENT_SPECIFIED_AMOUNT_WITHOUT_CARD  = 2030004h
            FFFFFFFF THINCA_METHOD_REFUND  = 2030005h
            FFFFFFFF THINCA_METHOD_REFUND_SPECIFIED_AMOUNT  = 2030006h
            FFFFFFFF THINCA_METHOD_VOID_CHARGE  = 2040001h
            FFFFFFFF THINCA_METHOD_VOID_DEPOSIT  = 2040001h
            FFFFFFFF THINCA_METHOD_BALANCE_INQUIRY  = 2050001h
            FFFFFFFF THINCA_METHOD_CHECK_CARD  = 2050001h
            FFFFFFFF THINCA_METHOD_BALANCE_INQUIRY_WITH_CARD_NUMBER  = 2050002h
            FFFFFFFF THINCA_METHOD_REFUEL_CHECK_CARD  = 2050003h
            FFFFFFFF THINCA_METHOD_CARD_HISTORY  = 2060001h
            FFFFFFFF THINCA_METHOD_CHECK_CARD_HISTORY  = 2060001h
            FFFFFFFF THINCA_METHOD_TRAINING_PAYMENT  = 3010001h
            FFFFFFFF THINCA_METHOD_TRAINING_SALES  = 3010001h
            FFFFFFFF THINCA_METHOD_TRAINING_PAYMENT_IN_FULL_BALANCE  = 3010002h
            FFFFFFFF THINCA_METHOD_TRAINING_SALES_BALANCE  = 3010002h
            FFFFFFFF THINCA_METHOD_TRAINING_PAYMENT_POINT_ADDITION  = 3010004h
            FFFFFFFF THINCA_METHOD_TRAINING_PAYMENT_JUST_CHARGE  = 3010006h
            FFFFFFFF THINCA_METHOD_TRAINING_CHARGE  = 3020001h
            FFFFFFFF THINCA_METHOD_TRAINING_DEPOSIT  = 3020001h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_PAYMENT  = 3030001h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_SALES_PREPAID  = 3030001h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_SALES_POSTPAID  = 3030002h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_PAYMENT_SPECIFIED_AMOUNT  = 3030003h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_PAYMENT_SPECIFIED_AMOUNT_WITHOUT_CARD  = 3030004h
            FFFFFFFF THINCA_METHOD_TRAINING_REFUND  = 3030005h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_CHARGE  = 3040001h
            FFFFFFFF THINCA_METHOD_TRAINING_VOID_DEPOSIT  = 3040001h
            FFFFFFFF THINCA_METHOD_TRAINING_BALANCE_INQUIRY  = 3050001h
            FFFFFFFF THINCA_METHOD_TRAINING_CARD  = 3050001h
            FFFFFFFF THINCA_METHOD_TRAINING_CARD_HISTORY  = 3060001h

            //DirectIO/initauth.jsp 0x1010001 / 0x07
            //DirectIO/remove.jsp 0x1020001 / 0x07
            //DirectIO/emlist.jsp 0x1030001 / 0x00
            //DirectIO/justBeforeRequest.jsp 0x1040001 / 0x0B
            //DirectIO/cashDepositAuth.jsp 0x1050001 / 0x09
            //DirectIO/closing.jsp 0x1060001 / 0x06
            //DirectIO/totalize.jsp 0x1070001 / 0x12
            //AuthorizeSales/payment.jsp tlamAuthorizeSales.jsp 0x2010001 / 0x02 
            //AuthorizeSales/fullPayment.jsp 0x2010002 / 0x14
            //AuthorizeSales/paymentCardNumber.jsp 0x2010003 / 0x1c
            //AuthorizeSales/payment.jsp tlamAuthorizeSales.jsp 0x2010004 / 0x02
            //AuthorizeSales/refuelPayment.jsp 0x2010005 / 0x22
            //AuthorizeSales/justCharge.jsp 0x2010006 / 0x23
            //AuthorizeSales/refuelSpecifiedAmount.jsp 0x2010007 / 0x25
            //CashDeposit/cashDeposit.jsp 0x2020001 / 0x0A
            //CashDeposit/cashDepositCardNumber.jsp 0x2020002 / 0x1D
            //CashDeposit/chargeBack.jsp 0x2020003 / 0x1E
            //CashDeposit/pointCharge.jsp 0x2020004 / 0x1F
            //AuthorizeVoid/paymentCancel.jsp 0x2030001 / 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x2030003 / 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x2030004 / 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x2030005 / 0x0C
            //AuthorizeVoid/returnedGoods.jsp 0x2030006 / 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x2030002 / 0x0C
            //AuthorizeVoid/depositCancel.jsp 0x2040001 / 0x13
            //CheckCard/balanceInquiry.jsp tlamBalanceInquiry.jsp 0x2050001 / 0x01
            //CheckCard/balanceInquiryCardNumber.jsp 0x2050002 / 0x1B
            //CheckCard/refuelCheckCard.jsp 0x2050003 / 0x21
            //DircetIO/history.jsp 0x2060001 / 0x04
            //AuthorizeSales/fullPayment.jsp 0x3010001 / 0x05 0x02
            //AuthorizeSales/payment.jsp tlamTraining.jsp training.jsp 0x3010004 / 0x05 0x02
            //AuthorizeSales/justCharge.jsp 0x1A03010002 / 0x14
            //CashDeposit/cashDeposit.jsp 0x3020001 / 0x15 0x0A
            //AuthorizeVoid/paymentCancel.jsp 0x3030001 / 0x18 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x3030003 / 0x18 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x3030004 / 0x18 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x3030005 / 0x18 0x0C
            //AuthorizeVoid/paymentCancel.jsp 0x3030002 / 0x18 0x0C
            //AuthorizeVoid/depositCancel.jsp 0x3040001 / 0x18 0x0C
            //CheckCard/balanceInquiry.jsp tlamBalanceInquiry.jsp 0x3050001 / 0x16 0x01
            //DircetIO/history.jsp 0x3060001 / 0x17 0x04
            */

            /* enum Thincacloud::ThincaError, copyof_1329, width 4 bytes
                FFFFFFFF THINCA_S_SUCCESS  = 0
                FFFFFFFF THINCA_E_CANCEL  = 65h
                FFFFFFFF THINCA_E_RETURN_CARD_DATA  = 67h
                FFFFFFFF THINCA_E_INVALID_VALUE  = 0C9h
                FFFFFFFF THINCA_E_BUSY    = 0CAh
                FFFFFFFF THINCA_E_CAN_NOT_CANCEL  = 0CAh
                FFFFFFFF THINCA_E_NETWORK_ERROR  = 0CBh
                FFFFFFFF THINCA_E_NETWORK_TIMEOUT  = 0CCh
                FFFFFFFF THINCA_E_RW_ERROR  = 0CDh
                FFFFFFFF THINCA_E_ILLEGAL_STATE  = 0CEh
                FFFFFFFF THINCA_E_INVALID_CONFIG  = 0CFh
                FFFFFFFF THINCA_E_RW_CLAIMED_TIMEOUT  = 0D0h
                FFFFFFFF THINCA_E_RW_NOT_AVAILABLE  = 0D1h
                FFFFFFFF THINCA_E_RW_NOT_IC_CHIP_FORMATTING  = 0D2h
                FFFFFFFF THINCA_E_RW_UNSUPPORTED_VERSION  = 0D3h
                FFFFFFFF THINCA_E_INVALID_TERMINAL  = 12Dh
                FFFFFFFF THINCA_E_INVALID_MERCHANT  = 12Eh
                FFFFFFFF THINCA_E_INVALID_REQUEST  = 12Fh
                FFFFFFFF THINCA_E_INVALID_SERVICE  = 130h
                FFFFFFFF THINCA_E_FAIL_INIT_AUTH_TERM  = 131h
                FFFFFFFF THINCA_E_FAIL_REMOVE_TERM  = 132h
                FFFFFFFF THINCA_E_FAIL_CLOSE_SALES  = 133h
                FFFFFFFF THINCA_E_FAIL_AUTH_CHARGE_TERM  = 134h
                FFFFFFFF THINCA_E_FAIL_AUTH_DEPOSIT_TERM  = 134h
                FFFFFFFF THINCA_E_LOG_FULL  = 135h
                FFFFFFFF THINCA_E_FAILED_DEAL  = 136h
                FFFFFFFF THINCA_E_FAILED_TRANSACTION  = 136h
                FFFFFFFF THINCA_E_BEFORE_TERMINAL_USE_START_DATE  = 137h
                FFFFFFFF THINCA_E_AFTER_TERMINAL_USE_END_DATE  = 138h
                FFFFFFFF THINCA_E_INSUFFICIENT_BALANCE  = 191h
                FFFFFFFF THINCA_E_DEFICIENCY_BALANCE  = 191h
                FFFFFFFF THINCA_E_DISCOVER_MULTIPLE_CARDS  = 192h
                FFFFFFFF THINCA_E_UNKNOWN_CARD  = 193h
                FFFFFFFF THINCA_E_CARD_TIMEOUT  = 194h
                FFFFFFFF THINCA_E_CARD_COMMAND_ERROR  = 195h
                FFFFFFFF THINCA_E_PAYMENT_LIMIT  = 196h
                FFFFFFFF THINCA_E_POSSESSION_LIMIT  = 197h
                FFFFFFFF THINCA_E_CHARGE_LIMIT  = 198h
                FFFFFFFF THINCA_E_DEPOSIT_LIMIT  = 198h
                FFFFFFFF THINCA_E_NOT_TRANSACTABLE_CARD_STATUS  = 199h
                FFFFFFFF THINCA_E_REQUIRE_PIN_AUTHORIZATION  = 19Ah
                FFFFFFFF THINCA_E_RETRY_PIN_AUTHORIZATION  = 19Bh
                FFFFFFFF THINCA_E_FAIL_CARD_AUTHORIZATION  = 19Ch
                FFFFFFFF THINCA_E_DIFFERENT_CARD  = 19Dh
                FFFFFFFF THINCA_E_FAIL_VOID  = 19Eh
                FFFFFFFF THINCA_E_INSUFFICIENT_POINT_BALANCE  = 19Fh
                FFFFFFFF THINCA_E_POINT_UNAVAILABLE_CARD  = 1A0h
                FFFFFFFF THINCA_E_ILLEGAL_CARD  = 1F5h
                FFFFFFFF THINCA_E_INVALID_CARD  = 1F6h
                FFFFFFFF THINCA_E_NEGATIVE_CARD  = 1F7h
                FFFFFFFF THINCA_E_EXPIRED_CARD  = 1F8h
                FFFFFFFF THINCA_E_MOBILE_PIN_LOCK  = 1F9h
                FFFFFFFF THINCA_E_INVALID_SESSION  = 259h
                FFFFFFFF THINCA_E_UNAUTHENTICATED_USER  = 25Ah
                FFFFFFFF THINCA_E_UNAUTHENTICATED_POSITION  = 25Bh
                FFFFFFFF THINCA_E_AUTHENTICATED_USER  = 25Ch
                FFFFFFFF THINCA_E_AUTHENTICATED_POSITION  = 25Dh
                FFFFFFFF THINCA_E_FAIL_TERMINAL_AUTH  = 25Eh
                FFFFFFFF THINCA_E_FAIL_USERL_AUTH  = 25Fh
                FFFFFFFF THINCA_E_FAIL_USERL_AUTH_UNREGISTERED  = 260h
                FFFFFFFF THINCA_E_FAIL_POSITION_AUTH  = 261h
                FFFFFFFF THINCA_E_FAIL_POSITION_AUTH_UNREGISTERED  = 262h
                FFFFFFFF THINCA_E_INVALID_POSITION  = 263h
                FFFFFFFF THINCA_E_FATAL_AUTH  = 2BBh
                FFFFFFFF THINCA_E_CARD_WITHDRAWAL  = 2BCh
                FFFFFFFF THINCA_E_UNCONFIRMED_STATUS  = 321h
                FFFFFFFF THINCA_E_CARD_UNCONFIRMED_STATUS  = 322h
                FFFFFFFF THINCA_E_TRANSACTION_UNCONFIRMED_STATUS  = 323h
                FFFFFFFF THINCA_E_FATAL   = 384h
                FFFFFFFF THINCA_E_SESSION_TIMEOUT  = 385h
                FFFFFFFF THINCA_E_ICAS_ERROR  = 386h
                FFFFFFFF THINCA_E_BRAND_CENTER_ERROR  = 387h
             */

            /*enum Thincacloud::ThincaNfcType, copyof_1371, width 4 bytes
                FFFFFFFF THINCA_NFC_TYPE_NO  = 0
                FFFFFFFF THINCA_NFC_TYPE_ISO_14443_A  = 1
                FFFFFFFF THINCA_NFC_TYPE_ISO_14443_B  = 2
                FFFFFFFF THINCA_NFC_TYPE_ISO_15693  = 3
                FFFFFFFF THINCA_NFC_TYPE_FELICA  = 4
            */

            #region 草稿纸(errorCode)
            //0x2010001 = THINCA_METHOD_PAYMENT
            //0x2050001 = THINCA_METHOD_BALANCE_INQUIRY

            //v11 = ThincaResultImple_Assign

            //public string NearFullFlg = "0"; //56 触发Resource/nearFull(Edy) //xmlvalue -> v11 + 64


            //记得设置result_code = 0,result_code_ext = 0,可以避免下面的error_code
            //error_code:
            //101:result_code? = 18,120
            //202:result_code = 113
            //203:result_code = 106~111 , resultCodeExt == 0x3000001,0x4010001,0x6000000,0x7000000
            //204:result_code = 106~111 , resultCodeExt == 0x6300001
            //205:result_code = 105
            //301:result_code? = 5,119,164,179
            //302:result_code? = 6,132
            //303:result_code? = 8,48,105,118,122,123,139,142,153
            //304:result_code? = 107,128,131,133,134
            //305:result_code? = 137,138,178,186
            //306:result_code? = 145
            //307:result_code? = 146
            //308:result_code? = 129,143,144,147
            //309:result_code? = 125,130
            //310:result_code? = 185
            //401:result_code? = 15,109
            //402:result_code? = 28
            //403:result_code? = 42,181
            //404:result_code? = 100,101
            //405:result_code? = 9,10,16,111,161
            //406:result_code? = 110
            //407:result_code? = 150
            //408:result_code? = 151
            //409:result_code? = 148,152
            //502:result_code? = 11,12,14,113,114,149,154,180,182,184,188
            //503:result_code? = 13,117
            //801:result_code? = 187
            //802:result_code? = 102,104,121,124,127
            //900:result_code? = 50,108,135,136,183
            //900:result_code > 115 || result_code = 106~111 , (resultCodeExt & 0xFF000000) == 0x3000000,0x4000000,0x6000000,0x7000000


            //310:DealStatusCode == "00" 或 存在DealStatusCode 时 不存在 ReceiptType
            //801:deal status is unconfirmed (businessStatus == 2)
            //801:deal status is illegal(businessStatus == 3 , result_code != 114, a1+504 > 0)
            //801:businessStatus == 3 时 errCode == 900,901,902 且 (Inst + 504) > 0
            //802:DealStatusCode == "20" 时 *(Inst + 268) == 2
            //803:DealStatusCode == "20" 时 *(Inst + 268) != 2
            //900:deal status is illegal(businessStatus == 3 , result_code != 114, a1+504 == 0)
            //(resultCodeExt):result_code = 114

            //public string DealStatusCode = "00";
            //如果memcmp(DealStatusCode,"00",2) == 0
            // -> public string ReceiptType = "0";
            //如果memcmp(DealStatusCode,"20",2) == 0
            //  *(Inst + 268) == 2 时 -> 802,否则803
            //其他情况 -> 310 (结论：不应该存在）
            #endregion

            //v6 = a3 = error_code
            //v6=401(THINCA_E_INSUFFICIENT_BALANCE),406(THINCA_E_PAYMENT_LIMIT),407(THINCA_E_POSSESSION_LIMIT),408(THINCA_E_CHARGE_LIMIT),
            //413(THINCA_E_DIFFERENT_CARD),415(THINCA_E_INSUFFICIENT_POINT_BALANCE),416(THINCA_E_POINT_UNAVAILABLE_CARD),802(THINCA_E_CARD_UNCONFIRMED_STATUS),803(THINCA_E_TRANSACTION_UNCONFIRMED_STATUS)时
            //public string UpperTerminalId = "9999";

            //UpperTerminalId存在时
            //v52 = CardInformation_new(ThincaResult, AccountNumber);
            //  (ThincaResult + 208) = CardInformation
            //      CardInformationImple[2] = CardInformationImple
            //          CardInformationImple + 8 = AccountNumber
            //*(v52 + 48) = thincapayment::ThincaClient::GetBalance(*(v8 + 5));
            //public int CumulativePoint = 0;
            //public int PointType = 0;
            //v52 + 64
            //public int TotalPointOfPastYear = 100;
            //v52 + 72 
            //public int TotalPointOfPreviousYear = 101;
            //v52 + 80
            //public int BalanceLimit = 10000;
            //v52 + 88
            //public int ChargeLimitPerOnce = 10000;
            //v52 + 96
            //public string CardType = "8888";
            //v52 + 136
            //public string IDm = "7777";
            //*(v52 + 176) = thincapayment::ThincaClient::GetLastNfcType
            //(v52 + 184) = thincapayment::ThincaClient::GetLastNfcId

            //v6=401(InsufficientBalanceResult)时
            //public bool ValidPayFullFlg = false;
            //public bool ValidJustChargeFlg = false;
            //(ThincaResult + 224) = InsufficientBalanceResult
            //  InsufficientBalanceResult[2] = InsufficientBalanceResult_Body
            //      InsufficientBalanceResult_Body + 8 = accNumber
            //                                     + 48 = settledAmount
            //                                     + 52 = Balance
            //                                     + 56 = ValidPayFullFlg
            //                                     + 57 = ValidJustChargeFlg




            //(ThincaResult + 168) = GetApprovalCode

            //(v8 + 624) = doSpeedTest -> (ThincaResult + 320) = speed test: time=%d

            //ThincaResult[35] ThincaResult[36]
            //public ReceiptInfo receiptInfo = {receiptType:... , blahblahblah};
            //public string ReceiptType = "0";
            /* ReceiptType:
             *  (brandType=2 Edy 情况会有点特殊)
             *      0(ThincaReceiptPaymentEdy): EMoneyDealNo(88) EdyDealNo(88) DealAftRemainder(104) DealBefERemainder(104) UPPER_TERMINAL_ID(72) DealYMD(72)
             *      1(ThincaReceiptChargeEdy): EMoneyDealNo(88) EdyDealNo(88) UPPER_TERMINAL_ID(72) DealYMD(72) DealAftERemainder(104) DealBefERemainder(104) GetSettledAmount(Xml) GetAccountNumber(Xml)
             *      2(ThincaReceiptAlarmEdy): EMoneyDealNo(88) EdyDealNo(88) DealBefERemainder(104) UPPER_TERMINAL_ID(72) DealYMD(72) GetSettledAmount(Xml) GetAccountNumber(Xml)
             *      3(ThincaReceiptEdyCenterCommResult): ClosingTime(72) UPPER_TERMINAL_ID(72) ChargeUnknownDealTotalAmount(112) ChargeUnknownDealCnt(112)
             *          ChargeDealTotalAmount(112) ChargeDealCnt(112) UseUnknownDealTotalAmount(112) UseUnknownDealCnt(112) UseDealTotalAmount(112) UseDealCnt(112)
             *      4(ThincaReceiptPaymentEdy): DealYMD(72) GetBalance(Xml) GetAccountNumber(Xml)
             *      5(ThincaReceiptBalanceInquiryBase): DealYMD(72) GetBalance(Xml) GetBalance(Xml) GetAccountNumber(Xml)
             *      6(ThincaReceiptCardHistoryEdy): DealYMD(72) GetSequenceNumber(Xml) GetAccountNumber(Xml) LogType(JsonArr) DealDate(JsonArr),DealNumber(JsonArr),DealAmount(JsonArr),Remainder(JsonArr)
             * 
             *  Get_ProcType_WaonDealType_NanacoDealType_SapicaDealType:
             *      ProcType  (不存在时跳过) 0 = 0x2010000 || 1 = 0x2020000 || 2 = 0x2030000 || 3 = 0x2040000   (之后检查a2) 
             *      WaonDealType  (不存在时跳过) 1,7,8,12,13 = 0x2010000 || 2,10 = 0x2030000 || 3,4 = 0x2020000 || 5 = 0x2040000    (之后检查a2) 
             *      NanacoDealType  (不存在时跳过) 71 = 0x2010000 || 111,112 = 0x2020000
             *      SapicaDealType (不存在时跳过) 0~10 = 0x2010000 || 11 = 0x2030000 || 20 = 0x2020000 || 21 == 0x2040000 (之后检查a2) 
             *      a2 0x2040000 = 0x2040000 || 0x2040001 = 0x2010000 || 0x2030001~0x2030006 = 0x2030000 || 0x2030000 = 0x2010000 || 0x2020001~0x2020004 = 0x2020000
             *  
             *  0 ThincaReceiptPayment brandType = 3,4,5,6,7,8,9(Id,QuicPay,Suica,WAON,nanaco,PASELI,Sapica)
             *      3(ThincaReceiptPaymentId): CardCompanyId(对应ResourceXml/companyName[id]),UsableLimit,ValidDate,GoodsCode,ApprovalNumber,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      4(ThincaReceiptPaymentQuicpay): CardCompanyId(对应ResourceXml/companyName[id]),QPGoodsCode,UsableLimit,QppreBalanceInfo,ValidDate,GoodsCode,ApprovalNumber,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      5(ThincaReceiptPaymentTransport): DealAftRemainder,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      6(ThincaReceiptPaymentWaon): PointMessageID(对应ResourceXml/pointMessage[id]),PreviousYearPoint(触发ResourceXml/pointExpirationMessage),CumulativePoint,
             *          Point,AutoChargeFlg,DealAftRemainder,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest,MerchantPoint(触发ResourceXml/pointBonus),Powered(触发ResourceXml/pointPowered),PoweredMerchantPoint,
             *          PreviousYearPointExpiration(PreviousYearPoint存在时)
             *      7(ThincaReceiptPaymentNanaco2): DealAftRemainder,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      8(ThincaReceiptPaymentPaseli): SettledAmount,Balance,PaseliDealNo,ApprovalCode,UpperTerminalId,DealYMD,CardNo,ReceiptDest
             *      9(ThincaReceiptPaymentSapica): DealDetailId,CumulativePoint,Point,PointType,JustChargeAmount,DealAftRemainder,DealBefRemainder,ApprovalCode,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *  
             *  1 ThincaReceiptCharge brandType=5,6,7,9(Suica,WAON,nanco,Sapica)
             *      5(ThincaReceiptChargeTransport): UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *      6(ThincaReceiptChargeWaon): UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *      7(ThincaReceiptChargeNanaco2): UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *      9(ThincaReceiptChargeSapica): DealDetailId,CumulativePoint,UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *  
             *  2 ThincaReceiptAlarm brandType=3,4,5,6,7(Id,QuicPay,Suica,WAON,nanaco)
             *      (会判断Get_ProcType_WaonDealType_NanacoDealType_SapicaDealType)
             *      3(ThincaReceiptAlarmId): UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      4(ThincaReceiptAlarmQuicpay): UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      5(ThincaReceiptAlarmTransport): DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      6(ThincaReceiptAlarmWaon): WaonDealType(对应ResourceXml/history[id]),DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      7(ThincaReceiptAlarmNanaco2): NanacoDealType(对应ResourceXml/history[id]), DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      (不引用)9(ThincaReceiptAlarmSapica): SapicaDealType(对应ResourceXml/history[id]),DealDetailId,CumulativePoint,Point,PointType,JustChargeAmount,SapicaDealType,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *  
             *  5 ThincaReceiptBalanceInquiry brandType=7,8,9(nanaco,PASELI,sapica)
             *      7(ThincaReceiptBalanceInquiryNanaco2): UpperTerminalId,DealYMD,AccountNumber,ReceiptDest
             *      8(ThincaReceiptBalanceInquiryPaseli): UpperTerminalId,DealYMD,Balance,CardNo,ReceiptDest
             *      9(ThincaReceiptBalanceInquirySapica): DealDetailId,CumulativePoint,UpperTerminalId,DealYMD,AccountNumber,ReceiptDest
             *  
             *  6 ? (brandTyoe - 3) <= 5(8以下)
             *  
             *  7  ThincaReceiptVoidPayment brandType=3,4,5,6,8,9(Id,QuicPay,Suica,WAON,PASELI,Sapica)
             *      3(ThincaReceiptVoidPaymentId): CardCompanyId(对应ResourceXml/companyName[id]),ValidDate,GoodsCode,ApprovalNumber,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      4(ThincaReceiptVoidPaymentQuicpay): CardCompanyId(对应ResourceXml/companyName[id]),QPGoodsCode,QppreBalanceInfo,ValidDate,GoodsCode,ApprovalNumber,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      5(ThincaReceiptVoidPaymentTransport): DealAftRemainder,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      6(ThincaReceiptVoidPaymentWaon): PointMessageID(对应ResourceXml/pointMessage [id]),PreviousYearPoint(触发ResourceXml/pointExpirationMessage),CumulativePoint,Point,AutoChargeFlg,DealAftRemainder,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *      8(ThincaReceiptVoidPaymentPaseli): SettledAmount,Balance,PaseliDealNo,ApprovalCode,UpperTerminalId,DealYMD,CardNom,ReceiptDest
             *      9(ThincaReceiptVoidPaymentSapica): DealDetailId,CumulativePoint,Point,PointType,JustChargeAvailable,DealAftRemainder,DealBefRemainder,ApprovalCode,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest
             *  
             *  8  ThincaReceiptIntermediateSalesQuicpay 要求brandType=4(QuicPay/JCB)
             *      DealYMD,LastDealYMD,EndSequenceNumber,BeginSequenceNumber,ReceiptDest,
             *      CardCompanyItem(Json?):{CardCompanyCode(对应ResourceXml/companyName[id]),CardCompanyVoidAmount,CardCompanyVoidCount,CardCompanySalesAmount,CardCompanySalesCount}
             *      
             *  10 ThincaReceiptVoidCharge 要求brandType=5,6,9 (Suica,WAON,Sapica)
             *      5(ThincaReceiptVoidChargeTransport): UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *      6(ThincaReceiptVoidChargeWaon):  UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *      9(ThincaReceiptVoidChargeSapica):  DealDetailId,CumulativePoint,UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest
             *      
             *  11 ThincaReceiptPointChargeWaon 要求brandType=6(WAON)
             *      CumulativePoint,UpperTerminalId,DealYMD,DealAftRemainder,DealBefRemainder,SettledAmount,AccountNumber,ReceiptDest,PreviousYearPoint(触发ResourceXml/pointExpiration),PreviousYearPointExpiration
             *      
             *  12 ThincaReceiptRefundWaon 要求brandType=6(WAON)
             *      PointMessageID(对应ResourceXml/pointMessage [id]),CumulativePoint,Point,DealAftRemainder,DealBefRemainder,UpperTerminalId,DealYMD,SettledAmount,AccountNumber,ReceiptDest, PreviousYearPoint(触发ResourceXml/pointExpiration),PreviousYearPointExpiration
             *      
             *  ?   ThincaReceiptCardHistory(未触发 但可能是6)
             *      3(ThincaReceiptCardHistoryId): DealYMD,AccountNumber,ReceiptDest,HistoryItem(Json块?):{DealType(触发Resource/History[id],DealAmount,DealDate,DealNumber)}
             *      4(ThincaReceiptCardHistoryQuicpay): DealYMD,AccountNumber,ReceiptDest,HistoryItem(Json块?):{DealType(触发Resource/History[id],DealTerminalId,DealAmount,DealDate,DealNumber)}
             *      5(ThincaReceiptCardHistoryTransport): DealYMD,AccountNumber,ReceiptDest,LogType(Json块?):,DealDate(Json块?),Remainder(Json块?)
             *          (可能是三个Json Array然后对应读取)
             *      6(ThincaReceiptCardHistoryWaon): DealYMD,AccountNumber,ReceiptDest,Histories(json块?):{WaonDealType(触发Resource/History[id]),ChargeMethodType(触发Resource/History[option]),Balance,ChargeAmount,PaymentAmount,UpperTerminalId,DealYMD}
             *      7(ThincaReceiptCardHistoryNanaco2): UpperTerminalId,DealYMD,AccountNumber,ReceiptDest,Histories(json块?):{DealType(触发Resource/History[id]),Balance,DealAmount,DealYMD}
             *      8(ThincaReceiptCardHistoryPaseli): UpperTerminalId,Balance,DealYMD,CardNo,ReceiptDest,Histories(json块?):{DealType(CHARGE/PAYMENT/CHARGE_CANCEL/REFUND),SettledAmount(120),UpperTerminalId(72),DealYMD(72)}
             */

            /* 64:bool 104:int 72:str 144:json array
             * 
             * a1 + 16 确认node是否存在
             * a1 + 80 带默认值Fallback的读取string?
             * a1 + 128 InnerJson?
             * 
             * Format:
             *  NanacoDealType(104):int
             *  DealBefRemainder(104):int
             *  
             *  UpperTerminalId(72):str
             *  DealYMD(72):str
             *  AccountNumber(72):str
             *  DealDetailId(72):str
             *  
             *  CumulativePoint(104):int
             *  ReceiptDest(120):?
             *  DealAftRemainder(104):int
             *  DealBefRemainder(104):int
             *  SettledAmount(104):int
             *  
             *  CardCompanyId(120):?
             *  UsableLimit(112):?
             *  ValidDate(80):str?
             *  GoodsCode(80):str?
             *  QPGoodsCode(80):str?
             *  ApprovalNumber(80):str?
             *  QppreBalanceInfo(80):str?
             *  
             *  PointMessageID(104):int
             *  
             *  PreviousYearPoint(104):int
             *  (Waon,Sapica时)CumulativePoint(112):?
             *  (Waon,Sapica时)Point(112):? (会检查Node是否存在)
             *  AutoChargeFlg(48):bool?
             *  MerchantPoint(104):int
             *  Powered(88):float?
             *  PoweredMerchantPoint(104):int
             *  PreviousYearPointExpiration(72):str (需要PreviousYearPoint存在,有字符串分割 {pointExpYear} {pointExpMonth} {pointExpDay})
             *  
             *  Balance(104):int
             *  PaseliDealNo(72):str
             *  ApprovalCode(72):str
             *  CardNo(72):str
             *  
             *  Point(104):int
             *  PointType(104):int
             *  JustChargeAmount(112):?
             *  
             *  CardCompanyItem(144):Json Block?
             *  CardCompanyCode(120):?
             *  CardCompanyVoidAmount(104):int
             *  CardCompanyVoidCount(104):int
             *  CardCompanySalesAmount(104):int
             *  CardCompanySalesCount(104):int
             *  EndSequenceNumber(104):int
             *  BeginSequenceNumber(104):int
             *  LastDealYMD(72):str
             *  
             *  SapicaDealType(104):int
             *  PointType(104):int
             *  
             *  HistoryItem(144):Json Block?
             *  DealType(120):
             *  DealAmount(104):int
             *  DealDate(72):str
             *  DealNumber(104):int
             *  DealTerminalId(72):str
             *  LogType(144):Json Block?
             *  DealDate(144):Json Block?
             *  Remainder(144):Json Block?
             *  Histories(144):
             *  WaonDealType(104):
             *  ChargeMethodType(104):
             *  ChargeAmount(104):
             *  PaymentAmount(104):
             *  (History nanaco时)DealType(104):
             *  (History nanaco时)Balance(120):
             *  (History nanaco时)DealAmount(120):
             *  (History nanaco时)DealYMD(72):
             *  (History PASELI时)DealType(72):
             */

            #region 草稿纸
            ///* case 3 iD */
            //public int UpperTerminalId = 1000;  //3,4,5,6,7,8,9,1-0,1-?
            //public string CardCompanyId = "1";
            //public string companyName = "name";
            //public string UsableLimit = "10000";
            //public string ValidDate = "1000";
            //public string ApprovalCode = "100"; //8,9
            //public string ApprovalNumber = "100";
            //public string DealYMD = "20230101"; //3,4,5,6,7,8,9,?8
            //public string SettledAmount = "6666";//3,4,5,6,7,8,9,?8
            //public string AccountNumber = "1002";//3,4,5,6,9,?8
            //public string ReceiptDest = "1003";//3,4,5,6,7,8,9,?8

            ///* case 4 ? */
            //public string QPGoodsCode = "1004";
            //public string QppreBalanceInfo = "1005";
            ////public string GoodsCode = "901";

            ///* case 5 Transmit */
            //public string DealAftRemainder = "1";   //5,6,7,9,?8
            //public string DealBefRemainder = "1";   //5,6,7,9,?9

            ///* case 6 WAON */
            //public string PointMessageID = "50001";
            //public string PreviousYearPoint = "100";
            //public string CumulativePoint = "100"; // 9,?8,
            //public string Point = "101";    //9
            //public string AutoChargeFlg = "1";
            //public string AutoChargeAmount = "100";

            ///* case 7 nanaco */

            ///* case 8 paseli */
            //public string Balance = "100";
            //public string PaseliDealNo = "100";
            //public string CardNo = "01391000000000000000";

            ///* case 9 sapica */
            //public string DealDetailId = "101";  //?8,
            //public string PointType = "1";
            //public string JustChargeAmount = "100";



            //public string KyouzanFlg = "0";
            //public string SuspensionProcCode = "0";
            //public string ButtonType = "1";
            //public int CardType = 1;
            //public int IDm = 5555;
            #endregion

        }

        public class AdditionalSecurityMessage_em2 : AdditionalSecurityMessage
        {
            public string TermSerial = "ACAE01A9999";
            new public string ServiceBranchNo = "2";


            //public List<int> EMoneyCode = new List<int>() { 1, 2 };
            //public List<string> URL = new List<string>() { "http://192.168.31.201/thinca/emoney/1/", "http://192.168.31.201/thinca/emoney/2/" };

            //public int TotalPointOfPastYear = 100;
            //public int TotalPointOfPreviousYear = 101;
            //public int BalanceLimit = 10000;
            //public int ChargeLimitPerOnce = 10000;

            //public string CardType = "4444";
            //public string IDm = "2222";

            //public int FloorLimit = 10000;
            //public string AuthorizeErrorCode = "0";
            //public int AuthorizeStatus = 1;

            //public int UpperTerminalId = 1000;
            //public string DealStatusCode = "00";

            //public string KyouzanFlg = "0";
            //public string SuspensionProcCode = "0";
            //public string ButtonType = "1";
            ////public int CardType = 1;
            ////public List<int> CardType = new List<int>() { 1, 2, 3, 4 };
            ////public List<int> IDm = new List<int>() { 1, 2, 3, 4 };
            ////public int IDm = 2211;
            ////public int EMoneyCode = 1;
            ////public string URL = "http://192.168.31.201/thinca/emoney";
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

            //public List<ReceiptInfo> ReceiptInfo = new List<ReceiptInfo>();

            //public void AddReceipt(ReceiptInfo receiptInfo) => ReceiptInfo.Add(receiptInfo);
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

            //public List<ReceiptInfo> ReceiptInfo = new List<ReceiptInfo>();

            //public void AddReceipt(ReceiptInfo receiptInfo) => ReceiptInfo.Add(receiptInfo);
        }

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

        public class endpointUriClass
        {
            public endpointUriClass(string URI) { uri = URI; }
            public string uri = "";
        }
        public class endpointClass
        {
            public endpointUriClass terminals = new endpointUriClass("http://192.168.31.201/thinca/terminals");
            public endpointUriClass statuses = new endpointUriClass("http://192.168.31.201/thinca/statuses");
            public endpointUriClass sales = new endpointUriClass("http://192.168.31.201/thinca/sales");
            public endpointUriClass counters = new endpointUriClass("http://192.168.31.201/thinca/counters");
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
            public string terminalId = "111122223333444455556666777788";
            public string version = "2023-01-01T12:34:56";
            public int[] availableElectronicMoney = { 8 };
            public bool cashAvailability = true;
            public int productCode = 1001;
            //public int availableElectronicMoney = 0x5B; //AimePay

        }
        public class ActivateClass
        {
            public string certificate = "MIII+QIBAzCCCL8GCSqGSIb3DQEHAaCCCLAEggisMIIIqDCCA18GCSqGSIb3DQEHBqCCA1AwggNMAgEAMIIDRQYJKoZIhvcNAQcBMBwGCiqGSIb3DQEMAQYwDgQIfvAleH1QuzICAggAgIIDGEhb4D58cJLbcRQVMrPz3zgA1VY+dqJrNEI2piMByBk7jrjMg4RfFXpEI50Ya0C/odvVRDlv5j2yLIe9Nuu+AwBAqp2OvP/TolCc0Vm4iS+16l1Uq8Vf8Lfuxnyy6if//KaP0nkoH+hUJsl0CsRLvg6hWL2cEnvQhDBNdMQRQYeJlLk8BIiMM4E9/fhlVVrnpOpUyJtzmxRSKKHWdKDePwHXs+XksuFIyfsVq5ii7UlhEAseSp6+oTbI1W+yRKU9W+fosgv0/A4yE1MGkHssfg3PulPYvgeEXIj3EL3+OtlIkIluXN2fDIM2M56ve2HSKzbTcq38WvQRgiRs3wiC3VA40u9HXiYQRoK8Hrw2GvXUeWDXV1Z26DR3azjbkuARo8gv4mdTqW1woWEtxpHbHfMCR8rmwN6vGFYxxe1J1tUktw3EiX+hnhTlhkEg3pM3VkWQ+I3BUZcHuhq4W4hgvGMLPWvojAKLFV8LilSbWVdjnEJwsTBd1yp9Ha+Ab50mV6JfspbR03AV2BOcaSODm9G1bs+AdQG1XxBaM0rxOyr0AmcPjQ6kRbWRHMBWwljxqHhLaxVTa0Up+yPKl2R4nLxT2M36xeHpJxN9H42kT6vYiWZDA/XxrOWvlL8KK0GZz1iYmpv5aoIgq8aBy4gkkuANjI1V+Hcd+KT57VlY5Ktb2g3sfPjTqklkzYRmzuasQb08fqbHp0RL8qrRgafjEzRgCn1CW/Krt6eiQw+GYPI54egCpBSoxB3Euuzo5EyF8hx9Qn09uzIbjI5WvL/JhimbVsyPuO+uJXBhvpMMqw+nufge8+yGi98Oh4Y0jIyKCMtThRG4CzdCk1mXatzdqZt00dReUzV67KXiAj7k7fDhusoqCPS74YWFfSPi1LVaNy1AzrvhGOhcBYdGXTGYC5iso8YhYcAQ6xcToJCpOzg67XPklm62C/pv8y85jk4swnYTNSjw/Kh3C7/v1by3yBensNmn/kGfs+tllQsgSh7M8+8WttjKtfW6wD2HJpDPGrhgls/itu9hajNURaSWsIvVVYh/BOkYOTCCBUEGCSqGSIb3DQEHAaCCBTIEggUuMIIFKjCCBSYGCyqGSIb3DQEMCgECoIIE7jCCBOowHAYKKoZIhvcNAQwBAzAOBAgmKG/vlAxNWwICCAAEggTIHu3Xn1D1lmxlvzX66lSybPWAhA7B4akXQH0jY2JFtmWHnDxBmc/b3TyS09YIiQi6oUvSPenR2g10ChspHIxNEIXd0GflKok1/P6ict5K62oph7Y/yeqN9OObSmy9pqOHFrvhO21qZnig4ldKgfgXYFNhNX/w5muAWxOGFEov9Zwnj3sUp5u8Ag4g7g0BmDg+hcXtw/7f159eljfkm+cMQSTWm0/YwErnyI42dsYG+/mNtbDSFhHcVcqaKgean/u8qzWANz0JAcLut+QyW2c5x9M672azZls7nfaw8Z1w7iujeVz+VuW9hZOhty6MOzMLeLGJow+t/5Fj7D9SdI6g11h8DUpfsTCct3KLyi3XgbXP7tT4Z0ncTIM65oe21negHMwr0ZjdQlEE6xnrHDix8F+nPrIFbr9ABf26zq2MiIfCljoU1ZTbf5QUsLM7daGncSPAk2ORfH9H70HLYcXsGRQPIY9HrQn6HawHguHQfAirefKqbv+g7rHB5++tayKZof0GBdTVVBUZrnFu2exknYCmAnZuOXrfN85/9gjITL8dFI7bCb47Pg9D9EYfbBkMIKOmF1yuFShvZ7Y8AiZ66Sy8hiJnPVCp4KLjaWrVbi9QQ5sJhmjPr59G/0eXLQ8fpQiAt2YAWs7fEyGHSOcQixXBs8rA8Q6iP9SMbQzC8xS3GQP/8D3jFrYqyhJoUYeqvAr1q0BiFhAjfRuXoZU/H/fpv9JnJLIb3bgP3JAr0jOw/BmETqRXPGqtnHesPjKbwifGjbMH3LIdyQPeSbH/5uhTohVV37WskfUv2WESNdDfBy5w4xRO8IWe7SUJdh80vpLKXOlHLtSW4mWmrxJiX/JhCdAwsRVxgzyX5h7oW7tZ9JD8m2spIRFpfVtEj9xpdHTdPgSGRQCpZAyrTKOJz0VA9wQxPdl48uDVVHo325oqyACiBlZmZ8Qliesp/D4mS6IIiReyDkelBOmjiPGI3SthhQWnR/oRA7NVkfdkR1Zl+4wZAY+6JJhNqUqhw+tb4u5NiHtAv73NU3bd+KjYO8IMSTil8daUl5tQVYEHp1vEFlxx4jXnjhZ+K/+fKzJ7OB0w7ZFf89V+q2osOkDIVtDTWW5GsEYhfArMl1nwngDpru99Y0vhxywtRWjfW4PB/lobo86Bf79Ig/jwfBGLod4WEXeY371r9pA+y+v8Rc7OyCkBnExrzbvVqykzXDcyzHA+eYsnokJuHNifeutvlaBWAwEl8KREp7DI9ytYSWbe4hiIA9wZXs3OMiM+5Q6oHvcrnWi8gV1R77ZkS/E88Tyftl95MtafakapAo9Kz7xarxIYIpbdrkH8mLUjpneszyofitmhjp4hlFlF5y4vCF3hO1nIfANQxlCcc+sOWj4WdYk/kf+ImgIXO1rXTdgJ7+FzUguusU8DCE9UOR+Y7OHGoVv1WsCKxkrAfsobl5E0M6DwjC471v0VDlRYDiG3Ow6e9OBWnrYUspI2PAlFdkVLcuuatY5tJfQIxe1/NXoxXJCkYRfriZR2vTmEiOaatTzhr1Zk2kHRQfOEM8ZH5i73or1SPJzgK1RtFI4mAUnBG/E1hP39+lRADhkMBgjHSPrNszPWe9e1KCv40+d2llUwdywYXL6OMSUwIwYJKoZIhvcNAQkVMRYEFKUjZTDFr9BXZAxZGBrQOJIqkX1oMDEwITAJBgUrDgMCGgUABBSSJpwFjvH7WUNAzGCnQdDkqM2anQQIXFySWW8XZ7YCAggA";

            public initSettingClass initSettings = new initSettingClass();

        }

        //public class InitAuthClass
        //{
        //    public int UpperTerminalId = 100;
        //    public int EMoneyCode = 100;
        //    public int EMoneyResultCode = 100;
        //}

        public string ReturnOperateEntityXml(string serviceName, string AdditionalSecurityInformation)
        {
            /* 你妈的 居然能用
                    var opCmdParam =
                        "<response service=\"DirectIO\">" +
                            "<status value=\"0\" />" +
                            "<userdata>" +
                                "<properties>" +
                                                        "<boolValue name=\"TrainingMode\" value=\"true\" />" +

                                    "<longValue name=\"ResultCode\" value=\"0\" />" +
                                    "<longValue name=\"ResultCodeExtended\" value=\"0\" />" +
                                                         "<longValue name=\"PaymentCondition\" value=\"0\" />" +
                                                         "<longValue name=\"SequenceNumber\" value=\"0\" />" +
                                                          "<longValue name=\"TransactionType\" value=\"0\" />" +

                                                          "<currencyValue name=\"Balance\" value=\"0\" />" +
                                                          "<currencyValue name=\"SettledAmount\" value=\"0\" />" +

                                                    "<stringValue name=\"AccountNumber\" value=\"114514\" />" +
                                                    "<stringValue name=\"AdditionalSecurityInformation\" value=\"{0}\" />" +
                                                    "<stringValue name=\"ApprovalCode\" value=\"0\" />" +
                                                    "<stringValue name=\"CardCompanyID\" value=\"0\" />" +
                                                    "<stringValue name=\"CenterResultCode\" value=\"0\" />" +
                                                    "<stringValue name=\"DailyLog\" value=\"0\" />" +
                                                    "<stringValue name=\"PaymentDetail\" value=\"0\" />" +
                                                    "<stringValue name=\"SlipNumber\" value=\"0\" />" +
                                                    "<stringValue name=\"TransactionNumber\" value=\"0\" />" +

                                "</properties>" +
                            "</userdata>" +
                        "</response>";
                    */

            //TrainingMode: a1 + 44
            //ResultCode: a1 + 12
            //ResultCodeExtended: a1 + 13
            //PaymentCondition: a1 + 14
            //SequenceNumber: a1 + 15
            //TransactionType: a1 + 16
            //Balance: a1 + 18
            //SettledAmount: a1 + 17
            //AccountNumber: a1 + 80
            //AdditionalSecurityInformation: a1 + 120
            //ApprovalCode: a1 + 160
            //CardCompanyID: a1 + 200
            //CenterResultCode: a1 + 240
            //DailyLog: a1 + 280
            //PaymentDetail: a1 + 320
            //SlipNumber: a1 + 360
            //TransactionNumber: a1 + 400

            //servicecode:
            //AuthorizeSales:8
            //DirectIO:9998

            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            var root = doc.CreateElement("response");
            //root.SetAttribute("service", "DirectIO");
            root.SetAttribute("service", serviceName);
            //root.SetAttribute("status", "1");
            doc.AppendChild(root);

            var status = doc.CreateElement("status");
            status.SetAttribute("value", "1");
            //status.InnerText = "1";
            root.AppendChild(status);

            var userdata = doc.CreateElement("userdata");
            root.AppendChild(userdata);

            var properties = doc.CreateElement("properties");
            userdata.AppendChild(properties);

            //BoolValue
            //var TrainingMode = doc.CreateElement("boolValue");
            //TrainingMode.SetAttribute("name", "TrainingMode");
            //TrainingMode.SetAttribute("value", "false");
            //properties.AppendChild(TrainingMode);

            //longValue
            var ResultCode = doc.CreateElement("longValue");
            ResultCode.SetAttribute("name", "ResultCode");
            ResultCode.SetAttribute("value", "0");
            properties.AppendChild(ResultCode);

            var ResultCodeExtended = doc.CreateElement("longValue");
            ResultCodeExtended.SetAttribute("name", "ResultCodeExtended");
            ResultCodeExtended.SetAttribute("value", "0");
            properties.AppendChild(ResultCodeExtended);

            //var PaymentCondition = doc.CreateElement("longValue");
            //PaymentCondition.SetAttribute("name", "PaymentCondition");
            //PaymentCondition.SetAttribute("value", "0");
            //properties.AppendChild(PaymentCondition);

            //var SequenceNumber = doc.CreateElement("longValue");
            //SequenceNumber.SetAttribute("name", "SequenceNumber");
            //SequenceNumber.SetAttribute("value", "51");
            //properties.AppendChild(SequenceNumber);

            //var TransactionType = doc.CreateElement("longValue");
            //TransactionType.SetAttribute("name", "TransactionType");
            //TransactionType.SetAttribute("value", "0");
            //properties.AppendChild(TransactionType);

            ////currencyValue
            //var Balance = doc.CreateElement("currencyValue");
            //Balance.SetAttribute("name", "Balance");
            //Balance.SetAttribute("value", "0");
            //properties.AppendChild(Balance);

            var SettledAmount = doc.CreateElement("currencyValue");
            SettledAmount.SetAttribute("name", "SettledAmount");
            SettledAmount.SetAttribute("value", "1");
            properties.AppendChild(SettledAmount);

            //StringValue
            var addSecNode = doc.CreateElement("stringValue");
            addSecNode.SetAttribute("name", "AdditionalSecurityInformation");
            addSecNode.SetAttribute("value", AdditionalSecurityInformation);
            properties.AppendChild(addSecNode);

            var approvalCode = doc.CreateElement("stringValue");
            approvalCode.SetAttribute("name", "ApprovalCode");
            approvalCode.SetAttribute("value", "1");
            properties.AppendChild(approvalCode);

            var accNumber = doc.CreateElement("stringValue");
            accNumber.SetAttribute("name", "AccountNumber");
            accNumber.SetAttribute("value", "3456");
            properties.AppendChild(accNumber);

            //var CardCompanyID = doc.CreateElement("stringValue");
            //CardCompanyID.SetAttribute("name", "CardCompanyID");
            //CardCompanyID.SetAttribute("value", "101");
            //properties.AppendChild(CardCompanyID);

            var CenterResultCode = doc.CreateElement("stringValue");
            CenterResultCode.SetAttribute("name", "CenterResultCode");
            CenterResultCode.SetAttribute("value", "0");
            properties.AppendChild(CenterResultCode);

            //var DailyLog = doc.CreateElement("stringValue");
            //DailyLog.SetAttribute("name", "DailyLog");
            //DailyLog.SetAttribute("value", "101");
            //properties.AppendChild(DailyLog);

            //var PaymentDetail = doc.CreateElement("stringValue");
            //PaymentDetail.SetAttribute("name", "PaymentDetail");
            //PaymentDetail.SetAttribute("value", "101");
            //properties.AppendChild(PaymentDetail);

            //var SlipNumber = doc.CreateElement("stringValue");
            //SlipNumber.SetAttribute("name", "SlipNumber");
            //SlipNumber.SetAttribute("value", "101");
            //properties.AppendChild(SlipNumber);

            //var TransactionNumber = doc.CreateElement("stringValue");
            //TransactionNumber.SetAttribute("name", "TransactionNumber");
            //TransactionNumber.SetAttribute("value", "101");
            //properties.AppendChild(TransactionNumber); 

            return doc.InnerXml;
        }

        public string ReturnOperateEntityXml_AuthorizeSales(string serviceName, string AdditionalSecurityInformation,string SeqNumber = "1",string balance="101",string setAmount="100",string account="12341234123412341234")
        {
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            var root = doc.CreateElement("response");
            //root.SetAttribute("service", "DirectIO");
            root.SetAttribute("service", serviceName);
            //root.SetAttribute("status", "1");
            doc.AppendChild(root);

            var status = doc.CreateElement("status");
            status.SetAttribute("value", "1");
            //status.InnerText = "1";
            root.AppendChild(status);

            var userdata = doc.CreateElement("userdata");
            root.AppendChild(userdata);

            var properties = doc.CreateElement("properties");
            userdata.AppendChild(properties);

            //BoolValue
            //var TrainingMode = doc.CreateElement("boolValue");
            //TrainingMode.SetAttribute("name", "TrainingMode");
            //TrainingMode.SetAttribute("value", "false");
            //properties.AppendChild(TrainingMode);

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
            SettledAmount.SetAttribute("value",setAmount);
            properties.AppendChild(SettledAmount);

            //StringValue
            var addSecNode = doc.CreateElement("stringValue");
            addSecNode.SetAttribute("name", "AdditionalSecurityInformation");
            addSecNode.SetAttribute("value", AdditionalSecurityInformation);
            properties.AppendChild(addSecNode);

            var rand = new Random();

            var approvalCode = doc.CreateElement("stringValue");
            approvalCode.SetAttribute("name", "ApprovalCode");
            approvalCode.SetAttribute("value", rand.Next(0,int.MaxValue).ToString());
            properties.AppendChild(approvalCode);

            var accNumber = doc.CreateElement("stringValue");
            accNumber.SetAttribute("name", "AccountNumber");
            accNumber.SetAttribute("value", account);
            properties.AppendChild(accNumber);

            //var CardCompanyID = doc.CreateElement("stringValue");
            //CardCompanyID.SetAttribute("name", "CardCompanyID");
            //CardCompanyID.SetAttribute("value", "101");
            //properties.AppendChild(CardCompanyID);

            var CenterResultCode = doc.CreateElement("stringValue");
            CenterResultCode.SetAttribute("name", "CenterResultCode");
            CenterResultCode.SetAttribute("value", "0");
            properties.AppendChild(CenterResultCode);

            //var DailyLog = doc.CreateElement("stringValue");
            //DailyLog.SetAttribute("name", "DailyLog");
            //DailyLog.SetAttribute("value", "101");
            //properties.AppendChild(DailyLog);

            //var PaymentDetail = doc.CreateElement("stringValue");
            //PaymentDetail.SetAttribute("name", "PaymentDetail");
            //PaymentDetail.SetAttribute("value", "101");
            //properties.AppendChild(PaymentDetail);

            //var SlipNumber = doc.CreateElement("stringValue");
            //SlipNumber.SetAttribute("name", "SlipNumber");
            //SlipNumber.SetAttribute("value", "101");
            //properties.AppendChild(SlipNumber);

            //var TransactionNumber = doc.CreateElement("stringValue");
            //TransactionNumber.SetAttribute("name", "TransactionNumber");
            //TransactionNumber.SetAttribute("value", "101");
            //properties.AppendChild(TransactionNumber); 

            return doc.InnerXml;
        }

        public byte[] GenerateOpCmdPacket(byte[] command, byte[] opCmdPacket = null, bool rawPacket = false)
        {
            if (opCmdPacket == null)
            {
                return new byte[] { (byte)command.Length }
                    .Concat(command)
                    .Concat(new byte[] { 0x00, 0x00 })
                    .Concat(new byte[] { 0x00, 0x00 })
                    .ToArray();
            }else if(rawPacket == true)
            {
                return new byte[] { (byte)command.Length }
                    .Concat(command)
                    .Concat(new byte[] { 0x00, 0x00 })
                    .Concat(returnReversedByte((short)(opCmdPacket.Length)))
                    .Concat(opCmdPacket)
                    .ToArray();
            }
            else
            {
                return new byte[] { (byte)command.Length }
                    .Concat(command)
                    .Concat(new byte[] { 0x00, 0x00 })
                    .Concat(returnReversedByte((short)(opCmdPacket.Length + 2)))
                    .Concat(returnReversedByte((short)(opCmdPacket.Length)))
                    .Concat(opCmdPacket)
                    .ToArray();
            }
        }

        public byte[] GenerateOpCmdPacket(string command,byte[] opCmdPacket = null, bool rawPacket = false) => GenerateOpCmdPacket(Encoding.UTF8.GetBytes(command),opCmdPacket, rawPacket);

        public static byte[] returnReversedByte(short Input)
        {
            var output = BitConverter.GetBytes(Input);
            Array.Reverse(output);
            return output;
        }
        public static byte[] returnReversedByte(int Input)
        {
            var output = BitConverter.GetBytes(Input);
            Array.Reverse(output);
            return output;
        }

        static int GetAimeCardResultPktCount = 0;
        public byte[] BuildGetAimeCardResult()
        {
            var json = JsonConvert.SerializeObject(new MessageEventIo(0, 0, 8, 30, 20000)); //-> PASELI支払 カードをタッチしてください
            var PaseliMsgParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
            var PaseliCmdPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: PaseliMsgParam);
            var PaseliMessageCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, PaseliCmdPacket);
            PaseliMessageCmd.setParam(new byte[] { 0x00, 0x03 }); //Generic OPTION

            var ledjson = JsonConvert.SerializeObject(new LedEventIo(3, 0, 20000, 0, 0, 0));
            var ledJsonByte = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(ledjson)).ToArray();
            var ledJsonPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: ledJsonByte);
            var ledMessageCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, ledJsonPacket);
            ledMessageCmd.setParam(new byte[] { 0x00, 0x03 }); //Generic OPTION

            var openRwPacket = GenerateOpCmdPacket("OPEN_RW",  new byte[] { 0x00, 0x00, 0x09 },true); //(code1)(2byte code2) (code1==0)(code2 = 1(mifare only?),8(felica only?),9(both?))
                                                                                                      //9 AND 1 = 1
                                                                                                      //9 >> 2 = 2
                                                                                                      //-> 1 = 1,8 = 2,9 = 3
                                                                                                      //var openRwPacket = GenerateOpCmdPacket(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
            var openRw = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, openRwPacket);
            openRw.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW

            //(他会在这个包塞死 所以回传的时候要么没数据要么带卡号)
            var detectPacket = GenerateOpCmdPacket("TARGET_DETECT", opCmdPacket: new byte[] { 0x00, 0x00, 0x13, 0x88 },true);   //(00 00)(2byte timeout millsec)
            var detect = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, detectPacket);
            detect.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW


            var RequestPacket = GenerateOpCmdPacket("REQUEST");
            var RequestCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, RequestPacket);
            RequestCmd.setParam(new byte[] { 0x00, 0x01 }); //Generic CLIENT

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            OperatePkt.AddSubType(PaseliMessageCmd);
            OperatePkt.AddSubType(ledMessageCmd);
            OperatePkt.AddSubType(openRw);
            OperatePkt.AddSubType(detect);
            OperatePkt.AddSubType(RequestCmd);
            GetAimeCardResultPktCount = OperatePkt.subPackets.Count;
            return OperatePkt.Generate();
        }

        static int SuccessPaymentPacketCount = 0;
        public byte[] BuildSuccessPaymentResult(string cardNo = "01391144551419198100",string seqNumber = "1",int amount = 100)
        {

            //var json = JsonConvert.SerializeObject(new MessageEventIo(0,0,8,30,20000)); -> PASELI支払 カードをタッチしてください
            var json = JsonConvert.SerializeObject(new SoundEventIo(0, 8, 0, 20000)); //-> paseli//
                                                                                      //var json = JsonConvert.SerializeObject(new LedEventIo(0,0,20000,127,127,127)); // -> ?
                                                                                      //var json = JsonConvert.SerializeObject(new AmountEventIo(1,0,8,1,1000,20000));
            var opioCmdParam = Encoding.UTF8.GetBytes(json);
            opioCmdParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(opioCmdParam).ToArray();
            var opCmdPacket = GenerateOpCmdPacket("STATUS",opCmdPacket: opioCmdParam);
            var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
            opCommand.setParam(new byte[] { 0x00, 0x03 });


            //发送支付成功状态回服务器
            var returnSalesJson = new AdditionalSecurityMessage_AuthorizeSales();
            returnSalesJson.CardNo = cardNo;
            returnSalesJson.SettledAmount = amount;
            returnSalesJson.Balance = amount + 1;

            string paymentPacket = JsonConvert.SerializeObject(returnSalesJson);
            string paymentXml = ReturnOperateEntityXml_AuthorizeSales(
                serviceName:"AuthorizeSales",
                AdditionalSecurityInformation:paymentPacket,
                SeqNumber:seqNumber,
                balance: (amount + 1).ToString(),
                setAmount: amount.ToString(),
                account: cardNo);

            var CurrentBody = GenerateOpCmdPacket("CURRENT", opCmdPacket: Encoding.UTF8.GetBytes(paymentXml));
            var CurrentPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, CurrentBody);
            CurrentPacket.setParam(new byte[] { 0x00, 0x01 });

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            OperatePkt.AddSubType(opCommand);
            OperatePkt.AddSubType(CurrentPacket);
            SuccessPaymentPacketCount = OperatePkt.subPackets.Count;
            return OperatePkt.Generate();
        }
        public byte[] BuildHandshakeResult()
        {
            var HandshakePacket = new TcapPacket(TcapPacketType.Handshake);
            var Op23SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
            var Op81SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op81_HandshakeAccept_UpdateSetNetTimeout_OpPlaySoundMsg,
                new byte[] { 0x02, 0x05, 0x00 });
            var Op24SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op24_HandshakeReq_FarewellDone);

            HandshakePacket.AddSubType(Op23SubPacket);
            HandshakePacket.AddSubType(Op81SubPacket);
            HandshakePacket.AddSubType(Op24SubPacket);
            return HandshakePacket.Generate();
        }

        public byte[] BuildFarewellResult()
        {
            var Farewell = new TcapPacket(TcapPacketType.Farewell);
            var op23 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
            var op25 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, new byte[] { 0x12, 0x34, 0x56, 0x78 });
            var op24 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op24_HandshakeReq_FarewellDone);
            //var generalPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.General_WarningMessage, Encoding.UTF8.GetBytes("{\"EmoneyCode\":08,\"URL\":\"http://192.168.31.201/thinca/paseli\"}"));

            //Farewell.AddSubType(generalPacket);
            Farewell.AddSubType(op23);
            Farewell.AddSubType(op25);
            Farewell.AddSubType(op24);
            return Farewell.Generate();
        }

        public byte[] BuildInitAuthOperateMsgResult(bool emstage2)
        {
            var additionalSecurity = JsonConvert.SerializeObject(new AdditionalSecurityMessage());
            if (emstage2)
                additionalSecurity = JsonConvert.SerializeObject(new AdditionalSecurityMessage_em2());
            #region 手搓办法 现在简化了
            //string opCmdParam = ReturnOperateEntityXml("DirectIO", additionalSecurity);
            ////opCmdParam = String.Format(opCmdParam,additionalSecurity);
            //var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(opCmdParam)).ToArray();
            ////opCmdParamBytes = opCmdParamBytes.Concat(new byte[] {0xEF,0xBB,0xBF}).ToArray();

            //var opCmdParamLength = BitConverter.GetBytes((short)(opCmdParamBytes.Length + 2));
            //Array.Reverse(opCmdParamLength);
            //var length2 = BitConverter.GetBytes((short)opCmdParamBytes.Length);
            //Array.Reverse(length2);

            /* 居然能用 */
            //var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            //var opCmdContent = "CURRENT";
            //var opCmdContentByte = Encoding.UTF8.GetBytes(opCmdContent);

            //var opCmdPacket =
            //        new byte[] { (byte)opCmdContentByte.Length }
            //            .Concat(opCmdContentByte)
            //            //Package长度包(2byte?)
            //            .Concat(new byte[] { 0x00, 0x00 })
            //            .Concat(opCmdParamLength)
            //            .Concat(length2)
            //            //Package内容
            //            //.Concat(new byte[] { 0x31, 0x32, 0x33, 0x34 })
            //            .Concat(opCmdParamBytes)
            //            .ToArray();
            #endregion

            var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
            var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(ReturnOperateEntityXml("DirectIO", additionalSecurity))).ToArray();
            var opCmdPacket = GenerateOpCmdPacket("CURRENT", opCmdParamBytes);
            var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
            opCommand.setParam(new byte[] { 0x00, 0x01 }); //必须0x01 否则4002144
            OperatePkt.AddSubType(opCommand);

            return OperatePkt.Generate();
        }
        public void Dispatcher(HttpListenerContext Context)
        {
            byte[] Input = null;
            LogFile.PrintLog("Thinca", Context.Request.RawUrl, LogFile.LogType.Verbose);
            try
            {
                using (var memstream = new MemoryStream())
                {
                    Context.Request.InputStream.CopyTo(memstream);
                    Input = memstream.ToArray();
                }
                //


                //LogFile.PrintLog("Thinca(Hdr)->", Context.Request.ContentType + " " + Context.Request.UserAgent, LogFile.LogType.Verbose);
                foreach(var hdrName in Context.Request.Headers.AllKeys)
                {
                    LogFile.PrintLog("Thinca(Hdr)->", string.Format("{0}:{1}", hdrName, Context.Request.Headers[hdrName]), LogFile.LogType.Verbose);
                }

                if (!((Context.Request.RawUrl == "/thinca/stage2" || 
                    Context.Request.RawUrl == "/thinca/emstage2") ||
                    Context.Request.RawUrl == "/thinca/paseli/stage2"))
                    LogFile.PrintLog("Thinca", Encoding.UTF8.GetString(Input), LogFile.LogType.Verbose);
            }
            catch (Exception e) { }


            string output = "";
            byte[] Output = new byte[] { };
            bool UseBinary = false;

            #region 认证部分
            if (Context.Request.RawUrl == "/thinca")
            {
                output = JsonConvert.SerializeObject(new ActivateClass());
                Context.Response.AddHeader("x-certificate-md5", "757cffc53b98fc903476de6a672a1000");

            }
            else if (Context.Request.RawUrl.StartsWith("/thinca/terminals")) {
                //var eTag = (Context.Request.Headers["ETag"] != null ? Context.Request.Headers["ETag"] : "");
                //var ifNoneMatch = (Context.Request.Headers["If-None-Match"] != null ? Context.Request.Headers["If-None-Match"] : "");
                //if (!string.IsNullOrEmpty(eTag)) { Context.Response.Headers["ETag"] = eTag; }
                //LogFile.PrintLog("Thinca_Hdr","ETag:" +eTag, LogFile.LogType.Verbose);
                output = JsonConvert.SerializeObject(new initSettingClass());
            }
            else if (Context.Request.RawUrl == "/thinca/stage2" || Context.Request.RawUrl == "/thinca/emstage2")
            {
                var hexstr = Helper.HexByteArrayExtensionMethods.ToHexString(Input);
                //File.WriteAllBytes("stage2.bin", Input);

                var tcapRequestPacket = new TcapParser(Input);

                UseBinary = true;
                if (hexstr.StartsWith("02050100"))
                {
                    //Handshake包 先Handshake

                    Output = BuildHandshakeResult();
                    #region 超级草稿纸
                    //var HandshakePacket = new TcapPacket(TcapPacketType.Handshake);
                    //var Op23SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
                    ////var op81DummyData = new byte[] { 0x02, 0x05 };
                    ////var op81DummyDataCount = 0x10;
                    ////op81DummyData =op81DummyData.Concat(new byte[] {(byte)op81DummyDataCount}).ToArray();
                    ////for(int i = 0;i < op81DummyDataCount; i++)
                    ////{
                    ////    string dummyData = i.ToString().PadLeft(4,'0');
                    ////    op81DummyData = op81DummyData.Concat(new byte[] {(byte)dummyData.Length}).Concat(Encoding.UTF8.GetBytes(dummyData)).ToArray();
                    ////}

                    //var Op81SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op81_HandshakeAccept_UpdateSetNetTimeout_OpPlaySoundMsg,
                    //    new byte[] { 0x02, 0x05, 0x00 } //确定能过
                    //    //new byte[] {0x02,0x05,0x02,
                    //    //    0x08,0x41,0x40,0x30,0x31,0x41,0x40,0x41,0x40,
                    //    //    0x01,0x31}
                    //);
                    ////op81DummyData);
                    ////new byte[] {0x02,0x05,
                    ////        0x04, 0x04,0x30,0x30,0x30,0x31,
                    ////              0x04,0x30,0x30,0x30,0x32,
                    ////              0x04,0x30,0x30,0x30,0x33,
                    ////              0x04,0x30,0x30,0x30,0x34});
                    ////new byte[] { 0x02, 0x05, 0x01,
                    ////            0x3D,0x7B, 0x22, 0x45, 0x6D, 0x6F, 0x6E, 0x65, 0x79, 0x43, 0x6F, 0x64, 0x65, 0x22, 0x3A, 0x30, 0x38,
                    ////                    0x2C, 0x22, 0x55, 0x52, 0x4C, 0x22, 0x3A, 0x22, 0x68, 0x74, 0x74, 0x70, 0x3A, 0x2F, 0x2F, 0x31,
                    ////                    0x39, 0x32, 0x2E, 0x31, 0x36, 0x38, 0x2E, 0x33, 0x31, 0x2E, 0x32, 0x30, 0x31, 0x2F, 0x74, 0x68,
                    ////                    0x69, 0x6E, 0x63, 0x61, 0x2F, 0x70, 0x61, 0x73, 0x65, 0x6C, 0x69, 0x22, 0x7D});
                    ////p81SubPacket.setParam(new byte[] { 0x00, 0x04 });
                    //var Op24SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op24_HandshakeReq_FarewellDone);
                    ////Op24SubPacket.setParam(new byte[] { 0x00, 0x01 });

                    //HandshakePacket.AddSubType(Op23SubPacket);
                    //HandshakePacket.AddSubType(Op81SubPacket);
                    //HandshakePacket.AddSubType(Op24SubPacket);
                    //Output = HandshakePacket.Generate();
                    ////Output[0x06] = 0x01;

                    //HandshakeAccpetPacket Body:
                    //2byte size - 1byte entry count - 1byte strsize - string

                    //List<TcapPacket> packets = new List<TcapPacket>
                    //{
                    //    new TcapPacket(TcapPacket.TcapPacketType.Handshake, TcapPacket.TcapPacketSubType.Handshake_op23,true),
                    //    new TcapPacket(TcapPacket.TcapPacketType.Handshake, TcapPacket.TcapPacketSubType.Handshake_RequestMessage,false),
                    //    //new TcapPacket(TcapPacket.TcapPacketType.Handshake, TcapPacket.TcapPacketSubType.Handshake_RequestMessage)
                    //};
                    //Output = new byte[] { };
                    //foreach (var cmds in packets) Output = Output.Concat(cmds.Generate()).ToArray();

                    //var packet1 = new TcapPacket(TcapPacket.TcapPacketType.Handshake);
                    //Output = packet1.Generate();



                    //Output = header.Concat(outputLength).Concat(new byte[] {0xff,0x00,0x00}).Concat(outputXmlByte).ToArray();
                    //会和最后两位对比      //比第一位大就会出问题
                    //Output = header.Concat(new byte[] { 0x07 }) //长度
                    //    .Concat(new byte[] { 0x00, 0x00, 0x00, 0x81, 0x00, 0x01, 0x22 })
                    //    .ToArray();
                    //第一位必须为0x00(否则会变成Unknown Message)
                    //第二第三位 会在初始化Message的时候用于赋值
                    //第四位 0x81 和第一位组成0x0081 -> RequestAcceptMessage
                    //0x01:RequestWarningMessage
                    //0x00:RequestMessage(但是不设置v8+14)
                    //0x24:RequestMessage(v8+14 -> p4)  (此时a3 == 0)
                    //0x81:RequestAccpetMessage         (此时a3 > 3)
                    //第四位在后面会要求必须为0x01 第三位要求0x00 否则4022144 (但是0x81也可以)
                    //第五第六为必须大于0x00(否则不触发长度)
                    //第七位 RequestAccpetMessage验证时作为a2带参，必须不为0

                    //new byte[] { 0x00, 0x00, 0x00, 0x81, 0x00,0x01, 0x22 } -> 触发RequestAccpetMessage的验证 因a3<3导致4022145

                    //第一/四位:4022144
                    //第二/三位:4022143
                    //第五/六位:42

                    //4022141:包头错误(off:0xFE30) 要求header必须0x02,0x05或0x02,0x01 

                    //Content包 (a2+8) (a2+10) (a2+12) (a2+14) 例如*(unsigned __int8 *)(a2 + 14) | (unsigned __int16)(*(_WORD *)(a2 + 8) << 8);相当于用第一位第四位组成数字

                    //var msg1 = header.Concat(new byte[] { 0x07 })
                    //    .Concat(new byte[] { 0x00, 0x00, 0x00, 0x81, 0x00, 0x01, 0x12 })
                    //    .ToArray();
                    //var msg2 = header.Concat(new byte[] { 0x07 })
                    //    .Concat(new byte[] { 0x00, 0x00, 0x00, 0x81, 0x00, 0x01, 0x12 })
                    //    .ToArray();
                    //var msg3 = header.Concat(new byte[] { 0x07 })
                    //    .Concat(new byte[] { 0x00, 0x00, 0x00, 0x81, 0x00, 0x01, 0x12 })
                    //    .ToArray();
                    //Output = msg1.Concat(msg2).Concat(msg3).ToArray();  

                    //Output = new byte[] { 0x02, 0x05, 0x01, 0x00, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 };
                    //var output2 = JsonConvert.SerializeObject(new ActivateClass());
                    //Output = Output.Concat(Encoding.UTF8.GetBytes(outputXml)).ToArray();
                    //Output[0x05] = (byte)Output.Length;
                    #endregion
                }
                //Output = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb };
                //else if(Input.Length == 0)
                //{
                //    UseBinary = false;
                //    output = "{\"EMoneyCode\":08,\"URL\":\"http://192.168.31.201/thinca/paseli\"}";
                //}
                else if (hexstr.StartsWith("02050600"))
                {
                    /* 完成OperateEntity写入之后使用Farewell包完成TCAP流程 */
                    /* 能用  */
                    Output = BuildFarewellResult();

                    #region 草稿纸
                    //var Farewell = new TcapPacket(TcapPacketType.Farewell);
                    //var op23 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
                    //var op25 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg,new byte[] {0x12,0x34,0x56,0x78});
                    //var op24 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op24_HandshakeReq_FarewellDone);
                    ////var generalPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.General_WarningMessage, Encoding.UTF8.GetBytes("{\"EmoneyCode\":08,\"URL\":\"http://192.168.31.201/thinca/paseli\"}"));

                    ////Farewell.AddSubType(generalPacket);
                    //Farewell.AddSubType(op23);
                    //Farewell.AddSubType(op25);
                    //Farewell.AddSubType(op24);
                    //Output = Farewell.Generate();
                    #endregion
                }
                else
                {
                    //空包，用OperateEntity写入参数
                    Output = BuildInitAuthOperateMsgResult(Context.Request.RawUrl == "/thinca/emstage2");
                    #region 草稿纸
                    //var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
                    //OpOperateDeviceMsg
                    //1byte 长度 + COMMAND + 4byte (Payload长度+2) + 2byte Payload长度 + Payload
                    //COMMAND:
                    //  REQUEST:返回当前参数XML
                    //  CURRENT:00 00 00 02 00 01 (需要Payload)
                    //  RESULT(和CURRENT一致)
                    //  CANCEL
                    //  TIMESTAMP
                    //  WAIT
                    //  UNIXTIME
                    //  UNIXTIMEWAIT

                    //Current的Payload使用ReturnOperateEntityXml,用于写入AdditionalSecurityInformation(EMoneyCode)


                    //var opCmdContent = @"RESULT <response><userdata><properties><TrainingMode>true</TrainingMode><ResultCode>0</ResultCode></properties></userdata></response>";\




                    //var additionalSecurity = JsonConvert.SerializeObject(new AdditionalSecurityMessage());
                    //if (Context.Request.RawUrl == "/thinca/emstage2")
                    //    additionalSecurity = JsonConvert.SerializeObject(new AdditionalSecurityMessage_em2());

                    //string opCmdParam = ReturnOperateEntityXml("DirectIO",additionalSecurity);
                    ////opCmdParam = String.Format(opCmdParam,additionalSecurity);
                    //var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(opCmdParam)).ToArray();
                    ////opCmdParamBytes = opCmdParamBytes.Concat(new byte[] {0xEF,0xBB,0xBF}).ToArray();

                    //var opCmdParamLength = BitConverter.GetBytes((short)(opCmdParamBytes.Length + 2));
                    //Array.Reverse(opCmdParamLength);
                    //var length2 = BitConverter.GetBytes((short)opCmdParamBytes.Length);
                    //Array.Reverse(length2);

                    ///* 居然能用 */
                    //var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
                    //var opCmdContent = "CURRENT";
                    //var opCmdContentByte = Encoding.UTF8.GetBytes(opCmdContent);

                    //var opCmdPacket =
                    //        new byte[] { (byte)opCmdContentByte.Length }
                    //            .Concat(opCmdContentByte)
                    //            //Package长度包(2byte?)
                    //            .Concat(new byte[] { 0x00, 0x00 })
                    //            .Concat(opCmdParamLength)
                    //            .Concat(length2)
                    //            //Package内容
                    //            //.Concat(new byte[] { 0x31, 0x32, 0x33, 0x34 })
                    //            .Concat(opCmdParamBytes)
                    //            .ToArray();

                    //var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg,opCmdPacket);
                    //opCommand.setParam(new byte[] { 0x00, 0x01 }); //必须0x01 否则4002144
                    //OperatePkt.AddSubType(opCommand);

                    //Output = OperatePkt.Generate();
                    /* 以上能用 */
                    #endregion

                    #region 草稿纸
                    //var cmd = "<request></request>";
                    //var cmdPacket = new byte[] {(byte) cmd.Length};
                    //cmdPacket = cmdPacket.Concat(Encoding.UTF8.GetBytes(cmd)).Concat(new byte[] {0x00,0x00,0x00,0x00}).ToArray();
                    //var subPacket2 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, cmdPacket);
                    //subPacket2.setParam(new byte[] { 0x00, 0x01 });
                    //OperatePkt.AddSubType(subPacket2);
                    //Output[0x07] = 0x02;    //必须得有


                    //new byte[] {0x0A,0x00,
                    //           //|----------------(v9=&body[off0+3])-------------------v-(v10=v9+2)-v
                    //            0x00,0x00,0x01,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0x31 }

                    //OpOperateDeviceMsg (长度byte)+(content xml(thincapayment.dll))+ (4* 0x00)
                    //new byte[] {0x30, //x+1 x+3 x+5 < byte.length 这个必须0x01否则不触发isprint()

                    //            0x30,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x3a,0x3b,0x3c,0x3d,0x3e,0x3f,
                    //            0x40,0x41,0x42,0x43,0x44,0x45,0x46,0x47,0x48,0x49,0x4a,0x4b,0x4c,0x4d,0x4e,0x4f,
                    //            0x50,0x51,0x52,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x5b,0x5c,0x5d,0x5e,0x5f,

                    //            0x00,0x00,0x00,0x00   //2位会和包长度相加得到长度 必须等于总长度
                    //            }
                    //var opContent2 = "<response><userdata><properties><TrainingMode>true</TrainingMode><ResultCode>0</ResultCode></properties></userdata></response>";
                    //var opCmdPacket2 = new byte[] { (byte)opCmdContent.Length };
                    //opCmdPacket2 = opCmdPacket2.Concat(Encoding.UTF8.GetBytes(opContent2)).Concat(new byte[] { 0x00, 0x00, 0x00, 0x00 }).ToArray();
                    //var op2Command2 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket2);



                    //opCmd可能要用param1设置length
                    //opCommand.setParam(new byte[] { 0x00, 0x00 });
                    //OperatePkt.AddSubType(opCommand);

                    //var UpdateEntity = new TcapPacket(TcapPacketType.UpdateEntity);
                    //var RequestIdMsg = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.Update_RequestID, new byte[] { 0x01,0x01 });
                    //UpdateEntity.AddSubType(RequestIdMsg);
                    //Output = UpdateEntity.Generate();
                    #endregion



                    //var GeneralSubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.General_Message);
                    //ErrorPacket.AddSubType(GeneralSubPacket);
                    //Output = new byte[] { 0x02, 0x05, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 };
                    //Array.Resize(ref Output, 0x205);
                }
                //02 05 (response type) 00 (size?) 00
                //mininum size 0x06

                //Array.Resize(ref Output, 0x205);
                //Output = new byte[] { Input[0], Input[1], 0x00,0x00 };
                //Array.Resize(ref Output, 48);

                //var json = JsonConvert.SerializeObject(new InitAuthClass());

                //Output = Output.Concat(Encoding.UTF8.GetBytes(json)).ToArray()
                if (UseBinary)
                    LogFile.PrintLog("Thinca<-", Helper.HexByteArrayExtensionMethods.ToHexString(Output), LogFile.LogType.Verbose);
                //Output[4] = (byte)Output.Length;
                Context.Response.AddHeader("Content-Type", "application/x-tcap");
            }
            else if (Context.Request.RawUrl == "/thinca/common-shop/initauth.jsp")
            {
                // {"UniqueCode":"ACAE01A9999","PassPhrase":"ase114514","ServiceBranchNo":14,"GoodsCode":"0990"}

                //output = JsonConvert.SerializeObject(new InitAuthClass());
                //output = "SERV=http://192.168.31.201/thinca/stage2\nCOMM=<root><item><EMoneyCode>08</EMoneyCode><URL>http://192.168.31.201/thinca/paseli</URL></item></root>\n<root><item><EMoneyCode>08</EMoneyCode><URL>http://192.168.31.201/thinca/paseli</URL></item></root>";
                //output = "COMM=<root><item><EMoneyCode>08</EMoneyCode><URL>http://192.168.31.201/thinca/paseli</URL></item></root>\n<root><item><EMoneyCode>08</EMoneyCode><URL>http://192.168.31.201/thinca/paseli</URL></item></root>";
                output = "SERV=http://192.168.31.201/thinca/stage2\r\nCOMM=http://192.168.31.201/thinca/stage3";

                //走COMM通讯需要设置包头为0201

                Context.Response.AddHeader("Content-Type", "application/x-tlam");
            }else if(Context.Request.RawUrl == "/thinca/common-shop/emlist.jsp")
            {
                //{"ServiceBranchNo":2,"TermSerial":"ACAE01A9999"}

                //output = "SERV=http://192.168.31.201/thinca/emstage2\nCOMM=<root><item><EMoneyCode>08</EMoneyCode><URL>http://192.168.31.201/thinca/paseli</URL></item></root>\n<root><item><EMoneyCode>08</EMoneyCode><URL>http://192.168.31.201/thinca/paseli</URL></item></root>";
                output = "SERV=http://192.168.31.201/thinca/emstage2\r\nCOMM=http://192.168.31.201/thinca/emstage3";

                Context.Response.AddHeader("Content-Type", "application/x-tlam");
            }else if(Context.Request.RawUrl == "/thinca/counters/ACAE01A9999" || Context.Request.RawUrl == "/thinca/statuses/ACAE01A9999")
            {
                //这俩不用回东西
            }
            #endregion

            #region PASELI
            if(Context.Request.RawUrl == "/thinca/emoney/paseli/payment.jsp")
            {
                //TLAM Metadata->下发接口
                output = "SERV=http://192.168.31.201/thinca/paseli/stage2\r\nCOMM=http://192.168.31.201/thinca/paseli/stage2";
                Context.Response.AddHeader("Content-Type", "application/x-tlam");
            }
            else if (Context.Request.RawUrl == "/thinca/emoney/paseli/balanceInquiry.jsp")
            {
                //余额查询接口
                output = "SERV=http://192.168.31.201/thinca/paseli/query_stage2\r\nCOMM=http://192.168.31.201/thinca/paseli/query_stage3";
                Context.Response.AddHeader("Content-Type", "application/x-tlam");
            }
            else if(Context.Request.RawUrl == "/thinca/paseli/stage2" || Context.Request.RawUrl == "/thinca/paseli/stage3" ||
                Context.Request.RawUrl == "/thinca/paseli/query_stage2" || Context.Request.RawUrl == "/thinca/paseli/query_stage3")
            {
                bool QueryBalance = false;
                if (Context.Request.RawUrl == "/thinca/paseli/query_stage2" || Context.Request.RawUrl == "/thinca/paseli/query_stage3") QueryBalance = true;
                //TCAP包
                //var reqHex = Helper.HexByteArrayExtensionMethods.ToHexString(Input);
                var tcapRequestPacket = new TcapParser(Input);
                UseBinary = true;

                if (tcapRequestPacket.pktType == TcapPacketType.Handshake) //reqHex.StartsWith("020501")
                {
                    #region 草稿纸
                    //var HandshakePacket = new TcapPacket(TcapPacketType.Handshake);
                    //var Op23SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
                    //var Op81SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op81_HandshakeAccept_UpdateSetNetTimeout_OpPlaySoundMsg,
                    //    new byte[] { 0x02, 0x05, 0x00 });
                    //var Op24SubPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op24_HandshakeReq_FarewellDone);

                    //HandshakePacket.AddSubType(Op23SubPacket);
                    //HandshakePacket.AddSubType(Op81SubPacket);
                    //HandshakePacket.AddSubType(Op24SubPacket);
                    //Output = HandshakePacket.Generate();
                    #endregion

                    Output = BuildHandshakeResult();
                }
                else if (tcapRequestPacket.pktType == TcapPacketType.OperateEntity) //reqHex.StartsWith("020506")
                {
                    //完成OperateEntity写入之后使用Farewell包完成TCAP流程
                    //因为发多少OperateDeviceMsg就会按指定顺序返回多少Packet,所以直接按发包顺序找对应的包
                    if (tcapRequestPacket.messages.Count == GetAimeCardResultPktCount) // -> BuildGetAimeCardResult();
                    {
                        //MsgParam LEDParam OpenRW Detect(这个包找卡号) REQUEST(这个包找SeqNumber)
                        var DetectPkt = tcapRequestPacket.messages[3].MessageHex;
                        var RequestPkt = tcapRequestPacket.messages[4].MessageBody;
                        if (DetectPkt.Length == 54)
                        {
                            var cardId = DetectPkt.Substring(18, 20);
                            var reqXmlStr = RequestPkt.SubArray(6, RequestPkt.Length - 6);
                            var reqXml = new XmlDocument();
                            reqXml.LoadXml(Encoding.UTF8.GetString(reqXmlStr));

                            var nsmgr = new XmlNamespaceManager(reqXml.NameTable);
                            nsmgr.AddNamespace("ICAS", "http://www.hp.com/jp/FeliCa/ICASClient");


                            var SequenceNumber = (reqXml.SelectSingleNode("//ICAS:longValue[@name='SequenceNumber']", nsmgr) as XmlElement).GetAttribute("value");
                            var Amount = (reqXml.SelectSingleNode("//ICAS:currencyValue[@name='Amount']", nsmgr) as XmlElement).GetAttribute("value");
                            var AmountInt = int.Parse(Amount);

                            LogFile.PrintLog("Thinca", cardId + " ThincaPayment: " + Amount, LogFile.LogType.Success);
                            Output = BuildSuccessPaymentResult(cardId, SequenceNumber, AmountInt);
                        }
                        else
                        {
                            //没读到卡号 直接退
                            //否则连续发的话会进入死循环
                            Output = BuildFarewellResult();
                        }


                    }
                    else if (tcapRequestPacket.messages.Count == SuccessPaymentPacketCount)
                    {
                        //完事 结束
                        Output = BuildFarewellResult();
                    }
                    else
                    {
                        //继续让他请求卡号
                        Output = BuildGetAimeCardResult();
                    }
                    //Output = BuildFarewellResult();
                    //Output = BuildGetAimeCardResult();
                    #region Caogaozhi
                    //var Farewell = new TcapPacket(TcapPacketType.Farewell);
                    //var op23 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op23_HandshakeReq_FarewellGoodbye_ErrorUnex);
                    //var op25 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, new byte[] { 0x12, 0x34, 0x56, 0x78 });
                    //var op24 = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op24_HandshakeReq_FarewellDone);
                    ////var generalPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.General_WarningMessage, Encoding.UTF8.GetBytes("{\"EmoneyCode\":08,\"URL\":\"http://192.168.31.201/thinca/paseli\"}"));

                    ////Farewell.AddSubType(generalPacket);
                    //Farewell.AddSubType(op23);
                    //Farewell.AddSubType(op25);
                    //Farewell.AddSubType(op24);
                    //Output = Farewell.Generate();
                    #endregion
                }
                else if(tcapRequestPacket.pktType == TcapPacketType.Error)
                {
                    //没得回了 强制结束
                    Output = BuildFarewellResult();
                }
                else
                {
                    //空包
                    Output = BuildGetAimeCardResult();
                    #region 草稿纸
                    //先走REQUEST包获取当前参数 /request/userdata/properties/longValue[name='PaymentMedia'] -> 支付渠道 比如 8 = Paseli
                    /*
                     *  <request service="AuthorizeSales"
                            xmlns="http://www.hp.com/jp/FeliCa/ICASClient">
                            <userdata>
                                <properties>
                                    <longValue name="ServiceObjectVersion" value="240"/>
                                    <longValue name="PaymentMedia" value="8"/>              -> 8 = Paseli
                                    <boolValue name="AsyncMode" value="true"/>
                                    <boolValue name="TrainingMode" value="false"/>
                                    <stringValue name="AdditionalSecurityInformation" value="{&quot;ServiceBranchNo&quot;:2,&quot;GoodsCode&quot;:&quot;0990&quot;}"/>
                                </properties>
                                <parameters>
                                    <longValue name="SequenceNumber" value="51"/>           ->  ?
                                    <currencyValue name="Amount" value="100"/>              ->  要付款100yen
                                    <currencyValue name="TaxOthers" value="0"/>
                                    <longValue name="Timeout" value="1000"/>
                                </parameters>
                            </userdata>
                        </request>
                    */

                    //var RequestIdSubPkt = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.Update_RequestID, new byte[] { 0x00, 0x01 });
                    //var UpdatePkt = new TcapPacket(TcapPacketType.UpdateEntity);
                    //UpdatePkt.AddSubType(RequestIdSubPkt);
                    //Output = UpdatePkt.Generate();


                    //直接发回账单，如果能成的话说明在Thinca层完成支付
                    //所以: Thinca完成读卡->卡号->请求扣款->回传账单
                    /*
                    string paymentPacket = QueryBalance ? JsonConvert.SerializeObject(new AdditionalSecurityMessage_BalanceInquiry()) : JsonConvert.SerializeObject(new AdditionalSecurityMessage_AuthorizeSales());

                    string opCmdParam = ReturnOperateEntityXml_AuthorizeSales("AuthorizeSales", paymentPacket);
                    var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(opCmdParam)).ToArray();

                    var opCmdParamLength = BitConverter.GetBytes(opCmdParamBytes.Length + 2);
                    Array.Reverse(opCmdParamLength);
                    var length2 = BitConverter.GetBytes((short)opCmdParamBytes.Length);
                    Array.Reverse(length2);

                    var opCmdContent = "CURRENT";
                    var opCmdContentByte = Encoding.UTF8.GetBytes(opCmdContent);

                    var opCmdPacket =
                            new byte[] { (byte)opCmdContentByte.Length }
                                .Concat(opCmdContentByte)
                                //Package长度包(2byte?)
                                //.Concat(new byte[] { 0x00, 0x00 })
                                .Concat(opCmdParamLength)
                                .Concat(length2)
                                //Package内容
                                //.Concat(new byte[] { 0x31, 0x32, 0x33, 0x34 })
                                .Concat(opCmdParamBytes)
                                .ToArray();

                    var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
                    opCommand.setParam(new byte[] { 0x00, 0x01 }); //必须0x01 否则4002144

                    var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
                    OperatePkt.AddSubType(opCommand);
                    //让他叫一声(没用)
                    //var soundCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op81_HandshakeAccept_UpdateSetNetTimeout_OpPlaySoundMsg);
                    //soundCmd.setParam(new byte[] { 0x00, 0x00 });
                    //OperatePkt.AddSubType(soundCmd);

                    Output = OperatePkt.Generate();
                    */
                    #region 以前的手搓办法
                    // string paymentPacket = QueryBalance ? JsonConvert.SerializeObject(new AdditionalSecurityMessage_BalanceInquiry()) : JsonConvert.SerializeObject(new AdditionalSecurityMessage_AuthorizeSales());

                    //string opCmdParam = ReturnOperateEntityXml_AuthorizeSales("AuthorizeSales", paymentPacket);
                    //var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(opCmdParam)).ToArray();

                    //var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("{}")).ToArray();
                    //var opCmdParamLength = BitConverter.GetBytes((short)(opCmdParamBytes.Length + 2));
                    //Array.Reverse(opCmdParamLength);
                    //var length2 = BitConverter.GetBytes((short)(opCmdParamBytes.Length));
                    //Array.Reverse(length2);

                    //AmountEvent的时候 -> deviceNumber = 1, sendData = {brandCode(2byte) (code2) (code3 4byte) (code4 4byte) (code5 4byte)
                    //sendData -> actinParam

                    //var ioStruct = new ClientIo(
                    //    deviceType: ClientIo.deviceTypeEnum.MessageEvent,
                    //    deviceNumber: 1,
                    //    actionType: 0,  //0,1?
                    //    //写byte进去会导致他傻逼
                    //    sendData: new int[]
                    //    {
                    //        0,8,    //brandCode -> resource.xml/brand[code]
                    //        0,8,    //code2 -> resource.xml/message[id]
                    //        0,0     //code3
                    //    },
                    //    screen: true
                    //);

                    /* 能用的AmountEvent
                    var ioStruct = new ClientIo(
                            deviceType: ClientIo.deviceTypeEnum.AmountEvent,
                            deviceNumber: 1,
                            actionType: 0,
                            //写byte进去会导致他傻逼
                            sendData: new int[]
                            {
                                0,8,    //brandCode
                                1,      //code2 -> (1,4 -> 1(Payment) 3,5 -> 2(Charge) 6,7-> 4(PaymentCancel) 8,9->5(ChargeCancel) 11 -> 7(WaonAutocharge))
                                0,0,27,10,  //code3 (code2 = 2,4,5,7,9 -> 3(balance)
                                0,0,27,10,  //code4 (payment?)
                                0,0,0,0     //code5 (timeout?)
                            },
                            screen: true
                        );
                    */

                    /* 原始办法 现在改用GenerateOpCmdPacket(string command,byte[] opCmdPacket)
                    //var opioCmdLength2 = BitConverter.GetBytes((short)(opioCmdParam.Length + 2));
                    //Array.Reverse(opioCmdLength2);
                    //var opioCmdLength = BitConverter.GetBytes((short)opioCmdParam.Length);
                    //Array.Reverse(opioCmdLength);

                    //var opCmdContent = "STATUS";
                    //var opCmdContentByte = Encoding.UTF8.GetBytes(opCmdContent);

                    //var opCmdPacket =
                    //        new byte[] { (byte)opCmdContentByte.Length }
                    //            .Concat(opCmdContentByte)
                    //            //Package长度包(2byte?)
                    //            .Concat(new byte[] { 0x00,0x00 })
                    //            .Concat(opioCmdLength2) //下方长度
                    //            .Concat(opioCmdLength) //下方长度
                    //            .Concat(opioCmdParam)//*(a1 + 8) = i;
                    //            .ToArray();
                    */
                    #endregion

                    /* 完成支付 叫一声 返回
                    
                    //var json = JsonConvert.SerializeObject(new MessageEventIo(0,0,8,30,20000)); -> PASELI支払 カードをタッチしてください
                    var json = JsonConvert.SerializeObject(new SoundEventIo(0,8,0,20000)); //-> paseli//
                    //var json = JsonConvert.SerializeObject(new LedEventIo(0,0,20000,127,127,127)); // -> ?
                    //var json = JsonConvert.SerializeObject(new AmountEventIo(1,0,8,1,1000,20000));
                    var opioCmdParam = Encoding.UTF8.GetBytes(json);
                    opioCmdParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(opioCmdParam).ToArray();
                    var opCmdPacket = GenerateOpCmdPacket("STATUS", opioCmdParam);
                    var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
                    opCommand.setParam(new byte[] { 0x00, 0x03 });

                    string paymentPacket = QueryBalance ? JsonConvert.SerializeObject(new AdditionalSecurityMessage_BalanceInquiry()) : JsonConvert.SerializeObject(new AdditionalSecurityMessage_AuthorizeSales());
                    string paymentXml = ReturnOperateEntityXml_AuthorizeSales("AuthorizeSales", paymentPacket);
                    var CurrentBody = GenerateOpCmdPacket("CURRENT", Encoding.UTF8.GetBytes(paymentXml));
                    var CurrentPacket = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, CurrentBody);
                    CurrentPacket.setParam(new byte[] { 0x00, 0x01 });
                    
                    var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
                    OperatePkt.AddSubType(opCommand);
                    OperatePkt.AddSubType(CurrentPacket);
                    Output = OperatePkt.Generate();
                    */

                    /*
                     *  可以用的情况
                    //var json = JsonConvert.SerializeObject(new SoundEventIo(0, 8, 0, 20000)); //-> paseli//
                    //var json = JsonConvert.SerializeObject(new LedEventIo(0,0,20000,127,127,127)); // -> ?
                    //var json = JsonConvert.SerializeObject(new AmountEventIo(1,0,8,1,1000,20000));
                     */

                    //

                    /* 测试->读卡
                    */
                    //var json = JsonConvert.SerializeObject(new MessageEventIo(0, 0, 8, 30, 20000)); //-> PASELI支払 カードをタッチしてください
                    //var PaseliMsgParam = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
                    //var PaseliCmdPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: PaseliMsgParam);
                    //var PaseliMessageCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, PaseliCmdPacket);
                    //PaseliMessageCmd.setParam(new byte[] { 0x00, 0x03 }); //Generic OPTION

                    //var ledjson = JsonConvert.SerializeObject(new LedEventIo(3, 0, 20000, 0, 0, 0));
                    //var ledJsonByte = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }.Concat(Encoding.UTF8.GetBytes(ledjson)).ToArray();
                    //var ledJsonPacket = GenerateOpCmdPacket("STATUS", opCmdPacket: ledJsonByte);
                    //var ledMessageCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, ledJsonPacket);
                    //ledMessageCmd.setParam(new byte[] { 0x00, 0x03 }); //Generic OPTION

                    //var openRwPacket = GenerateOpCmdPacket("OPEN_RW",true, new byte[] { 0x00, 0x00, 0x09}); //(code1)(2byte code2) (code1==0)(code2 = 1(mifare only?),8(felica only?),9(both?))
                    //                                                                                    //9 AND 1 = 1
                    //                                                                                    //9 >> 2 = 2
                    //                                                                                    //-> 1 = 1,8 = 2,9 = 3
                    ////var openRwPacket = GenerateOpCmdPacket(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
                    //var openRw = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, openRwPacket);
                    //openRw.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW

                    ////(他会在这个包塞死 所以回传的时候要么没数据要么带卡号)
                    //var detectPacket = GenerateOpCmdPacket("TARGET_DETECT", opCmdPacket:new byte[] { 0x00, 0x00, 0x01,0x01 });
                    //var detect = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, detectPacket);
                    //detect.setParam(new byte[] { 0x00, 0x08 }); //GENERIC NFCRW


                    ////var RequestPacket = GenerateOpCmdPacket("REQUEST");
                    ////var RequestCmd = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, RequestPacket);
                    ////RequestCmd.setParam(new byte[] { 0x00, 0x01 }); //Generic CLIENT

                    //var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
                    //OperatePkt.AddSubType(PaseliMessageCmd);
                    //OperatePkt.AddSubType(ledMessageCmd);
                    //OperatePkt.AddSubType(openRw);
                    //OperatePkt.AddSubType(detect);
                    ////OperatePkt.AddSubType(RequestCmd);
                    //Output = OperatePkt.Generate();

                    //STATUS时  0x01->illegal general client
                    //          0x02->(general status?)0x378e0 (length)(STATUS)(00 00)(00 02)(00 01)
                    //          0x03->(general option?)0x375a0 (length)(STATUS) -> thincapayment::ThincaPaymentImple::OnClientIoEventOccurred

                    //string paymentPacket = QueryBalance ? JsonConvert.SerializeObject(new AdditionalSecurityMessage_BalanceInquiry()) : JsonConvert.SerializeObject(new AdditionalSecurityMessage_AuthorizeSales());
                    //string opCmdParam = ReturnOperateEntityXml_AuthorizeSales("AuthorizeSales", paymentPacket);
                    //var opCmdParamBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(opCmdParam)).ToArray();

                    //var opCmdPacket = GenerateOpCmdPacket("CURRENT", opCmdParamBytes);
                    //var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
                    //opCommand.setParam(new byte[] { 0x00, 0x01 }); 



                    //REQUEST包
                    //var OperatePkt = new TcapPacket(TcapPacketType.OperateEntity);
                    //var opCmdContent = "REQUEST";
                    //var opCmdContentByte = Encoding.UTF8.GetBytes(opCmdContent);

                    //var opCmdPacket =
                    //        new byte[] { (byte)opCmdContentByte.Length }
                    //            .Concat(opCmdContentByte)
                    //            .Concat(new byte[] { 0x00, 0x00, 0x00, 0x00 })
                    //            .ToArray();
                    //var opCommand = new TcapSubPacket(TcapSubPacket.TcapPacketSubType.op25_FarewellReturnCode_OpOperateDeviceMsg, opCmdPacket);
                    //opCommand.setParam(new byte[] { 0x00, 0x01 });
                    //OperatePkt.AddSubType(opCommand);
                    ////OperatePkt.AddSubType(OpenFelicaRwPkt);
                    //Output = OperatePkt.Generate();
                    #endregion
                }
                if (UseBinary)
                    LogFile.PrintLog("Thinca<-", Helper.HexByteArrayExtensionMethods.ToHexString(Output), LogFile.LogType.Verbose);

                Context.Response.AddHeader("Content-Type", "application/x-tcap");
            }

            #endregion

                if (!UseBinary && output.Length > 0)
                LogFile.PrintLog("Thinca", output, LogFile.LogType.Verbose);

            if(!UseBinary)
                Output = Encoding.UTF8.GetBytes(output);

            try
            {
                Context.Response.StatusCode = 200;
                Context.Response.OutputStream.Flush();
                Context.Response.OutputStream.Write(Output, 0, Output.Length);
                Context.Response.Close();
            }catch(Exception ex)
            {
                LogFile.PrintLog("Thinca<-","Exception",ex, LogFile.LogType.Error);
            }
            return;
        }
    }
}
