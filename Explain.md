# ����
����ᾡ���ܵĳ��Խ���ThincaPayment��ԭ�������������Щ������

## ע������
�˴��Դ�δ����EMoney�Ļ���Ϊ��

�ڻ���ѡ�� ��Ϸ-����֧����Ϣ-��̨��֤-��ʼ��֤ �󣬻�̨���ȼ��VFD���ӣ�����������ʼ����Ȩ������

��Ȩ���Ĳ�������:

	��Ȩ������ΪMifare Classic���ͣ�Ҳ����ˢ����Ž��������������ſ���
	��Ƭ����Ϊ thinca ת16���� (74 68 69 6E 63 61)
	���а����������ݣ�
		storeCardId(offset+0 16byte ����)
		merchantCode(offset+16 20byte ����)
		storeBranchNumber(offset+36 5byte ����)
		֤������(offset+41 16byte passphrase��ͷ������)
		֮�����00��128 byte

	��һ�����ݻ���м��ܣ�
		Key:E8ADB54FBE8D2DC44A16C6B339A06457C3A1253ED8910D6BA666A77F5A3E1FFC
		IV:2B0C9071661C20AFEC7061CC639E7BAB
		Method:AES CBC

	���ܺ�õ�128byte ���ģ�����д�뿨Ƭ��Sector 1 �ڶ��ŵ� Sector 3 �ĵ�����
	����Sector 1�ĵ�һ��Ϊ 54 43 02 01(�޸���һ�ź���ᴥ������proxySetting֮���������ᵼ����֤��ͨ����һ�㲻��)

	�깤��

������õ���Repo���thincamod4.mct������ļ��Դ��Ĳ�����������

	storeCardId:1144551419198100
	merchantCode:11451419191145141919
	storeBranchNumber:11514
	֤������:passphrase114514
	������Proxy����

��ɶ����Ժ󣬻����Ὣ�������� tfps-res-pro/env.json �� tasms.root_endpoint��������и�env.json�Ļ���������ʱ��ͻ��������ˡ�������������£�
	
	User-Proxy:SEGA AMUSEMENT
	{
		"modelName":"ACA",			//ALLS HX2
		"serialNumber":"ACAE01A9999",		//Mainboard ID
		"merchantCode":"11451419191145141919",	//������
		"storeBranchNumber":11514,
		"storeCardId":"1144551419198100"
	}

�������յ�������·���ʼ���ã���������

	Header: "x-certificate-md5" = "֤��Base64ǰ��MD5"
	{
		certificate : "",	//�ͻ�������֤��,pkcs#7��ʽ,��Ȩ������������Ҫ�ܽ⿪���֤��
					//�˴�Ҫ����֤���base64�����ݣ�ԭʼ���ݼ���MD5�Ժ����õ�header��
		initSettings :{
			endpoints : {
				terminals:{	//ͬ�����õ�ַ������������õ�ַ����initSettings
					uri: ""	//��ַ
				},
				statuses:{ ͬterminals },	//״̬�ϱ���ַ��������ǰ�ᷢһ��
				sales:{ ͬterminals },		//�����ϱ���ַ����ʱ����
				counters:{ ͬterminals }	//Ͷ���ϱ���ַ��������ǰ��ÿ����Ϸ����ʱ����
			},
			intervals : {
				checkSetting : 3600,	//ͬ�����ü��(��)
				sendStatus : 3600	//�ϱ�״̬���(��)
			},
			settigsType : "AmusementTerminalSettings", //���������ܸ�
			status : "00",	//����룬����ʹ���������
					//00:�����֤(��ʼ��֤ʱ��)
					//10:����(ˢ������ʱ�ã���ʱ���ܴ���ˢǮ��)
					//11:��ͣ
					//12:ά��
					//80:�ݲ�����
					//90:�ѳ���(����ʱ��)
			terminalId : "111122223333444455556666777788",	//�ն�ID����ʾ�����ò˵�������Ϊ����
			version : "2023-01-01T12:34:56",	//�汾�ţ����԰��������ʽ�Ҹģ�Ӧ������statuses�ش�
			availableElectronicMoney : [ 8 ],	//����ʹ�õĵ���Ǯ����BrandID����������
			cashAvailability : true,	//�����ܲ��ܽ���Ͷ��?(���Ǻܶ�)
			productCode : 1001	//��Ʒ����?
		}
	}

�������û�����⣬�ͻ����Ὣ֤���ļ��� {terminalId}.p12 ��ʽ������ AMFS:\\emoney �ļ����£�Ȼ���Thinca������֤�飬������ Thinca->initAuth()

<b>ע��:���е�Thinca���� ����ͨ������TCAP��ͨѶ���ܻ��ʶ������Ĳ���������λ��(header body)���޲�����</b>

�ͻ�����ʱ�Ὺʼ���� \{commonPrimaryUri\}/initAuth.jsp (TLAM Metadata) <br/> \{commonPrimaryUri\} Ϊ tfps-res-pro/resource.xml �� /thincaResource/common/commonPrimaryUri

��ʱ��Ҫ�ظ�TCAPͨѶ��ַ���Ա���ĿΪ�����������£�
	
	SERV=http://127.0.0.1/thinca/common-shop/stage2
	//��ʱҲ�ɽ�SERV����COMM������������Ļ���Ҫ���е������Header����Ϊ0201
	//��Ϸ�Ե�һ�������ĵ�ַΪ׼��ͬʱ����������Ч
	//�������Ҫ����Content-Type : "application/x-tlam" ������

�ͻ�������õ�ַ����TCAPͨѶ��<br /><i>���е�TCAP����Ҫ���� Content-Type : "application/x-tcap" �����ϡ�</i>

	��һ�����ȻΪHandshake������HandshakeAccept����
	���Handshake�󣬿ͻ���ת��Idle״̬����ʱ��Ȼ���Ϳգ���ʱ����OperateDeviceMessage����ȡ�������������û���״̬��
	OperateDeviceMessage����Ȼ���з��أ�������������Ժ󼴿ɷ���Farewell�����TCAPͨѶ��

	ע��:ֻ������(ͨ��OperateDeviceMessage REQUEST��)���Ի�û���������

��˴�����ʽ���ִ����޷����������������淶�ȣ�����Ϸ�ᷢ��Error����ͬʱ��������������ʾע��ʧ�ܡ�

��δ��ȷͨ��OperateDeviceMessage���û���״̬��ͬ��������Thinca����nullptr access�������޷�����ɹ�״̬���¶����������ƣ��ص���ͷ����

���initAuth.jsp�󣬻�̨������ \{commonPrimaryUri\}/emlist.jsp ���ڻ�ȡ����Api�����̺�initAuth.jspһ����<br/><b>ע�⣺������ں����֧�����ڻ��ʶ���̨�ķ�ʽ����ʱ�·���URL���������ʶ���̨�Ĳ������������������֤û�������취���Ի�ȡ��</b>

������ϲ���󣬻�̨��������ز���д��NVRAM(sysfile.dat 0x2000λ��)���ص�����֧����Ϣ�˵�����ʱ�ܿ���ID��֧��Brand�����ע�ᡣ

<i>TCAP�����̽���ֱ�ӿ����룬��Ҫ��ÿ������д����������Ҫ����</i>

## ֧������
��Ϸ����Ͷ����ʾ��ͻῪʼ����֧�����̡�<br /><i>�������������ֻ����Ͷ�ң������terminals�ش���json��û������statusΪ10</i>

��ʱ����ѡ���Brand���ͣ���Ϸ��ʼ������֤ʱ��ȡ�ĵ�ַ(TLAM Metadata ����������ContentType)��һ���Ȼظ�TCAPͨѶ��ַ��

Ȼ�󴥷�TCAPͨѶ��<b>���е�֧�����̶���TCAP�����Ȼ���Receipt������Ϸ������Receipt����ֻ������Ϸ���¿�ʼ֧�����̡�</b>

	��һ���ȻHandshake������HandshakeAccept
	�ڶ����Ϊ�գ�ʹ��OperateDeviceMessage����ʼ���ö�����/VFD��ʾˢ����Ϣ
	OperateDeviceMessage��Ȼ���з��أ��������ؽ������ȡ����/SeqNumber/֧���ͨ��OperateDeviceMessage����֧��״̬��(�൱��Receipt)
	�����Ժ󷵻�Farewell�����TCAPͨѶ

Aime������������Ȼ��������Ҫô�����ŷ��أ�Ҫô��Ϊ��ʱ���Ϳհ���ע�������ʱʱ������������Ϸ��amd���ص������

TCAPͨѶ����ʱ��Ϸ����ݽ���������������� or ������һ�� or ��¼֧����Ϣ��Ͷ�ҡ�

<i>��һ����Ҳ����ֱ�ӿ�����</i>

## ����BrandID
Thinca��SEGAYʹ�õ�Brand ID����һЩ��ͬ

		(SEGA)	(THINCA)
	Nanaco:	1	1
	Edy:	2	2
	Id:	3	3
	��ͨ��:	4	5
	WAON:	5	6
	PASELI:	6	8
	SAPICA:	7	7(NANACO2)
	NUM:	8	9(SAPICA)

	QuicPay	(��֧��)	4

## ����TCAP��
TCAP���ĸ�ʽ���£�<br/><i>�����漰�����ֵ�λ�þ�Ϊ����� </i>

	(02 05)(���� 1byte)(body���ܳ��� 2byte)(body ?byte)
	//�������TLAM����ʱ ���ص���COMM������SERV����ʱ��ͷӦ��Ϊ02 01

	���õ��������£�
	01 : Handshake
	02 : Farewell
	03 : Error
	04 : AppDataTransfer
	05 : UpdateEntity
	06 : OperateEntity

	TCAP����һ����������body��ֻҪ������ͬһ������

Body���ĸ�ʽ���£�
	
	(methodLo 1byte) (paramLo 1byte) (paramHi 1byte) (methodHi 1byte) (messageBodyLength 2byte) (message ?byte)

	methodLo | methodHi ��� Int16 ��Ϊ method�����õ�method����:
	00 Default:(������Parseʧ��ʱ����)
		00 00 : RequestMessage (��������message)
		00 01 : RequestWarningMessage
		����  : RequestUnknownMessage 

	01 Handshake:	(���밴 23 -> 81 -> 24 ��˳�򣬷��򱨴�)
		00 23 : RequestMessage	(��������message)
		00 24 : RequestMessage	(��������message)
		00 81 : RequestAcceptMessage (message����Ϊ 02 05 00)

	02 Farewell:	(���밴 23 -> 25 -> 24 ��˳�򣬷��򱨴�)
		00 23 : RequestServerGoodByeMessage (��������message)
		00 24 : RequestServerGoodByeDoneMessage (��������message)
		00 25 : RequestReturnCodeMessage (message�̶�Ϊ4λbyte ���return code)

	03 Error:
		00 21 : RequestPacketFormatErrorMessage
		00 22 : RequestIllegalStateErrorMessage
		00 23 : RequestUnexpectedErrorMessage

	04 AppDataTransfer:
		01 01 : RequestFelicaCommandMessage (��Ҫ��1~255byte�ڵ�message)
		01 04 : RequestFelicaPrecommandMessage (��Ҫ��1~255byte�ڵ�message)
		01 05 : RequestFelicaExcommandMessage (��Ҫ��3~255byte�ڵ�message)
		01 06 : RequestFelicaCommandThrurwMessage  (��Ҫ��3~255byte�ڵ�message)
		01 09 : RequestFelicaPrecommandThrurwMessage (��Ҫ��4~255byte�ڵ�message)
		01 0A : RequestFelicaExcommandThrurwMessage (��Ҫ��4~255byte�ڵ�message)

	05 UpdateEntity: (���ܺ�����һ��������)
		00 30 : RequestRequestIdMessage (2byte message���ش����ݺ�messageһ��)
		00 81 : RequestSetNetworkTimeoutMessage (4byte message)
		01 01 : RequestFelicaSelectInternalMessage (���ɴ�message)
		01 81 : RequestFelicaSetTimeoutMessage (4byte message)
		01 82 : RequestFelicaSetRetryCountMessage (4byte message)

	06 OperateEntity: (���ܺ�����һ��������) (��Ҫ paramLo | paramHi = param)
		00 25 : RequestOperateDeviceMessage (��������潲��
		00 81 : RequestPlaySoundMessage (���������)
		01 01 : RequestFelicaOpenRwRequestMessage (���ɴ�message��param ����Ϊ00 04)
		01 05 : RequestFelicaCloseRwRequestMessage (���ɴ�message��param ����Ϊ00 04)

�ͻ�������������İ���Body������ֵ�method�⣬���������������
	
	00 Default:
		00 00 : ResponseFinishedMessage
	01 Handshake:
		00 21 : ResponseClientHelloMessage
		00 22 : ResponseClientHelloDoneMessage
		00 25 : ResponseDevicesMessage
		00 26 : ResponseDeviceResponseMessage/ResponseFeaturesMessage
	02 Farewell:
		00 21 : ResponseClientGoodByeMessage
		00 22 : ResponseClientGoodByeDoneMessage 
	?? (������û�ҳ������ĸ����͵�):
		00 02 : ResponseFelicaOpenRwStatusMessage/ResponseFelicaResponseMessage
		00 03 : ResponseFelicaSelectedDeviceMessage/ResponseFelicaErrorMessage
		00 06 : ResponseFelicaCloseRwStatusMessage
		00 07 : ResponseFelicaResponseThrurwMessage
		00 08 : ResponseFelicaErrorThrurwMessage

��Ϊ�ͻ����������ʽ�ͷ������ش�����ʽһ�£�����ֱ��Parse���ÿͻ�����������Ϣ�������� ResponseDevicesMessage �����������OperateDeviceMessage�Ĳ��������������Դ���һ�¡�		

## ����OperateEntity:RequestOperateDeviceMessage
OperateDeviceMessage����ֱ�Ӳ���ResponseDevicesMessage���оٵ��豸����ʱ���������豸��������������̡�

��ʱmessage�ĸ�ʽΪ (COMMAND ���� 1byte)(COMMAND �ַ���)(00 00)(Payload ����+2 2byte)(Payload ���� 2byte)(Payload ?byte) <br />
���û��Payload��Command��������(00 00 00 00)���ɡ�<br />
��ЩCommand��Ҫ��ʡ��(Payload���� + 2)���֣������ע����

��SEGAYΪ����һ��ResponseDevicesMessage�ᷢ�������豸��Ϣ��

	00 01 -> General Client
	00 02 -> General Status
	00 03 -> General Option
	00 04 -> Felica R/W
	00 05 -> Generic R/W Event
	00 06 -> Generic R/W Status
	00 07 -> Generic R/W Option
	00 08 -> Gemeroc NFC RW

�ڹ���OperateDeviceMessage����ʱ�������õ�param���������ָ���豸�Ĳ�����<br />
<i>����RequestFelicaOpenRwRequestMessage ��������paramΪ00 04,��Ϊֻ���ܶ�Felica R/W����</i>

�����ǲ��Կ��õ����

	00 01 -> General Client
		REQUEST: ���ص�ǰϵͳ��Ϣ,XML��ʽ.(���л����AdditionalJson����Ϣ������Ҫ)����Ҫ��Payload
		CURRENT: ����ϵͳ��Ϣ,XML��ʽ.(���ڸ���AdditionalJson�ͻش�״̬��ȣ�����Ҫ��
			(initAuthʱ��PayloadҪ��ǰ��(EF BB BF) 3 byte��Paymentʱ��Ҫ��
		RESULT: Ч����CURRENTһ��
		CANSEL: δ֪
		TIMESTAMP: �����豸�൱ǰʱ��
		WAIT: �ȴ�ָ������ʱ�䣬PayloadΪ8byte Int64��
		UNIXTIME: �����豸��UNIXʱ���
		UNIXTIMEWAIT: ����UNIXʱ����ȴ���PayloadΪ10byte(8byte Int64)(2byte)
		STATUS: ���� illegal general client

	00 02 -> General Status
		STATUS: Ч��δ֪��PayloadΪ6byte(00 00)(00 02)(00 01)

	00 03 -> General Option
		STATUS: ����ioEvent��PayloadΪ(00 00 00 00 00 00 00 00)(����Json)

	00 08 -> Generic NFC RW (����Aime������)
		OPEN_RW: ����������(PayloadΪ3byte, (00 00)(01:Mifare Only?,08:Felica Only?,09:Both?))
		CLOSE_RW: �رն�����(��Ҫ��Payload)
		TARGET_DETECT: ��⿨Ƭ(Payload 4byte ��ȴ�ʱ��(����))
			�˷�����������Ҫô��ʱҪô������Ƭ��Ϣ
		APDU_COMMAND:? (Payload ����3byte (code1)(code2 2byte)(data?) (����code2 + 3 <= length))
		FELICA_COMMAND:? (Payload ����7byte (code1 4byte)(code2 1byte)(code3 3byte) (code3 + 7 <= length)
		OPTION:? (Payload 6byte (code1 4byte)(code2 2byte) (code2 > 0 ʱ�������ᷢ��ʲô))

## ����CURRENT ���õ�Xml��AdditionalSecurityInformation
�����ﷵ��ResultCode,ResultCodeExtended,Balance,���г�ʼ����ʱ����ȡAdditionalSecurityInformation�����BrandUrlMap֮��ġ�

������ɣ�˵�����鷳��

## ����ioEvent
ioEvent�Ǹ�Json,��Լ��ʽ������

	{
		deviceType : 0,	//ioEvent���ͣ���ѡ: 
				//1 : MessageEvent	(��VFD����ʾ��Ϣ��
				//2 : SoundEvent		(����������
				//3 : LedEvent		(����LEDӦ�ã����Ƕ�Aimeû�ã�
				//16 : AmountEvent	(��ʾҪ֧���Ľ�����û������)
				//32 : EnableCancelEvent
				//48 : ClientIoSaveDealNumberEvent
				//64 : ProgressEvent
		deviceNumber : 0,
		actionType : 0,
		sendData : [],	//ioEvent������Ҫ��byte��λתΪsigned int�����������:
				//MessageEvent {(brandType 2byte),(messageId 2byte),(timeout? 2byte)}
				//(��ʱϵͳ��Resource.xml�Ҷ�Ӧbrand��id��message��ʾ��vfd��)
				//
				//SoundEvent {(brandType 2byte),(soundId 1byte),(timeout 2byte)}
				//(����ָ��brand��id��sound�ļ�)
				//
				//LedEvent {0,0,timeOut(2byte),param2(2byte),param3(2byte),param4(2byte)}
				//(��Ϊû�������Բ�֪����ɶ�ã�����deviceNumber 0=FFFF0000 1=FF00FF00 2=FF0000FF 3=00000000 4=FFFFFFFF)
				//
				//AmountEvent {(brandType 2byte)(id 1byte)(amount 4byte)(amountBalance 4byte)(timeout 2byte)}
				//(��ָ��brand��id��amount�ַ��� ������ʾ֧�����)
				//
				//EnableCancelInfo ����Ҫ����
				//
				//SaveDealNumberIo {(brandID 2byte)(dealNumber string)}
		screen : false
	}

## Thinca����
��IDA��ʱ�򿴴����ֻ����

    /* enum Thincacloud::ThincaMethod
		THINCA_METHOD_INIT_AUTH_TERM  = 1010001h
		THINCA_METHOD_REMOVE_TERM  = 1020001h
		THINCA_METHOD_CHECK_BRANDS  = 1030001h
		THINCA_METHOD_CHECK_DEAL  = 1040001h
		THINCA_METHOD_CHECK_LAST_TRAN  = 1040001h
		THINCA_METHOD_AUTH_CHARGE_TERM  = 1050001h
		THINCA_METHOD_AUTH_DEPOSIT_TERM  = 1050001h
		THINCA_METHOD_CLOSE_SALES  = 1060001h
		THINCA_METHOD_DAILY_CLOSE_SALES  = 1060002h
		THINCA_METHOD_TOTALIZE_SALES  = 1070001h
		THINCA_METHOD_INTERMEDIATE_SALES  = 1070001h
		THINCA_METHOD_PAYMENT  = 2010001h
		THINCA_METHOD_AUTHORIZE_SALES  = 2010001h
		THINCA_METHOD_PAYMENT_IN_FULL_BALANCE  = 2010002h
		THINCA_METHOD_AUTHORIZE_SALES_BALANCE  = 2010002h
		THINCA_METHOD_PAYMENT_SPECIFIED_CARD_NUMBER  = 2010003h
		THINCA_METHOD_PAYMENT_POINT_ADDITION  = 2010004h
		THINCA_METHOD_REFUEL_PAYMENT  = 2010005h
		THINCA_METHOD_PAYMENT_JUST_CHARGE  = 2010006h
		THINCA_METHOD_REFUEL_SPECIFIED_AMOUNT  = 2010007h
		THINCA_METHOD_CHARGE  = 2020001h
		THINCA_METHOD_CASH_DEPOSIT  = 2020001h
		THINCA_METHOD_CHARGE_SPECIFIED_CARD_NUMBER  = 2020002h
		THINCA_METHOD_CHARGE_BACK  = 2020003h
		THINCA_METHOD_POINT_CHARGE  = 2020004h
		THINCA_METHOD_VOID_PAYMENT  = 2030001h
		THINCA_METHOD_VOID_SALES_PREPAID  = 2030001h
		THINCA_METHOD_VOID_SALES_POSTPAID  = 2030002h
		THINCA_METHOD_VOID_PAYMENT_SPECIFIED_AMOUNT  = 2030003h
		THINCA_METHOD_VOID_PAYMENT_SPECIFIED_AMOUNT_WITHOUT_CARD  = 2030004h
		THINCA_METHOD_REFUND  = 2030005h
		THINCA_METHOD_REFUND_SPECIFIED_AMOUNT  = 2030006h
		THINCA_METHOD_VOID_CHARGE  = 2040001h
		THINCA_METHOD_VOID_DEPOSIT  = 2040001h
		THINCA_METHOD_BALANCE_INQUIRY  = 2050001h
		THINCA_METHOD_CHECK_CARD  = 2050001h
		THINCA_METHOD_BALANCE_INQUIRY_WITH_CARD_NUMBER  = 2050002h
		THINCA_METHOD_REFUEL_CHECK_CARD  = 2050003h
		THINCA_METHOD_CARD_HISTORY  = 2060001h
		THINCA_METHOD_CHECK_CARD_HISTORY  = 2060001h
		THINCA_METHOD_TRAINING_PAYMENT  = 3010001h
		THINCA_METHOD_TRAINING_SALES  = 3010001h
		THINCA_METHOD_TRAINING_PAYMENT_IN_FULL_BALANCE  = 3010002h
		THINCA_METHOD_TRAINING_SALES_BALANCE  = 3010002h
		THINCA_METHOD_TRAINING_PAYMENT_POINT_ADDITION  = 3010004h
		THINCA_METHOD_TRAINING_PAYMENT_JUST_CHARGE  = 3010006h
		THINCA_METHOD_TRAINING_CHARGE  = 3020001h
		THINCA_METHOD_TRAINING_DEPOSIT  = 3020001h
		THINCA_METHOD_TRAINING_VOID_PAYMENT  = 3030001h
		THINCA_METHOD_TRAINING_VOID_SALES_PREPAID  = 3030001h
		THINCA_METHOD_TRAINING_VOID_SALES_POSTPAID  = 3030002h
		THINCA_METHOD_TRAINING_VOID_PAYMENT_SPECIFIED_AMOUNT  = 3030003h
		THINCA_METHOD_TRAINING_VOID_PAYMENT_SPECIFIED_AMOUNT_WITHOUT_CARD  = 3030004h
		THINCA_METHOD_TRAINING_REFUND  = 3030005h
		THINCA_METHOD_TRAINING_VOID_CHARGE  = 3040001h
		THINCA_METHOD_TRAINING_VOID_DEPOSIT  = 3040001h
		THINCA_METHOD_TRAINING_BALANCE_INQUIRY  = 3050001h
		THINCA_METHOD_TRAINING_CARD  = 3050001h
		THINCA_METHOD_TRAINING_CARD_HISTORY  = 3060001h

        DirectIO/initauth.jsp 0x1010001 / 0x07
        DirectIO/remove.jsp 0x1020001 / 0x07
        DirectIO/emlist.jsp 0x1030001 / 0x00
        DirectIO/justBeforeRequest.jsp 0x1040001 / 0x0B
        DirectIO/cashDepositAuth.jsp 0x1050001 / 0x09
        DirectIO/closing.jsp 0x1060001 / 0x06
        DirectIO/totalize.jsp 0x1070001 / 0x12
        AuthorizeSales/payment.jsp tlamAuthorizeSales.jsp 0x2010001 / 0x02 
        AuthorizeSales/fullPayment.jsp 0x2010002 / 0x14
        AuthorizeSales/paymentCardNumber.jsp 0x2010003 / 0x1c
        AuthorizeSales/payment.jsp tlamAuthorizeSales.jsp 0x2010004 / 0x02
        AuthorizeSales/refuelPayment.jsp 0x2010005 / 0x22
        AuthorizeSales/justCharge.jsp 0x2010006 / 0x23
        AuthorizeSales/refuelSpecifiedAmount.jsp 0x2010007 / 0x25
        CashDeposit/cashDeposit.jsp 0x2020001 / 0x0A
        CashDeposit/cashDepositCardNumber.jsp 0x2020002 / 0x1D
        CashDeposit/chargeBack.jsp 0x2020003 / 0x1E
        CashDeposit/pointCharge.jsp 0x2020004 / 0x1F
        AuthorizeVoid/paymentCancel.jsp 0x2030001 / 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x2030003 / 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x2030004 / 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x2030005 / 0x0C
        AuthorizeVoid/returnedGoods.jsp 0x2030006 / 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x2030002 / 0x0C
        AuthorizeVoid/depositCancel.jsp 0x2040001 / 0x13
        CheckCard/balanceInquiry.jsp tlamBalanceInquiry.jsp 0x2050001 / 0x01
        CheckCard/balanceInquiryCardNumber.jsp 0x2050002 / 0x1B
        CheckCard/refuelCheckCard.jsp 0x2050003 / 0x21
        DircetIO/history.jsp 0x2060001 / 0x04
        AuthorizeSales/fullPayment.jsp 0x3010001 / 0x05 0x02
        AuthorizeSales/payment.jsp tlamTraining.jsp training.jsp 0x3010004 / 0x05 0x02
        AuthorizeSales/justCharge.jsp 0x1A03010002 / 0x14
        CashDeposit/cashDeposit.jsp 0x3020001 / 0x15 0x0A
        AuthorizeVoid/paymentCancel.jsp 0x3030001 / 0x18 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x3030003 / 0x18 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x3030004 / 0x18 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x3030005 / 0x18 0x0C
        AuthorizeVoid/paymentCancel.jsp 0x3030002 / 0x18 0x0C
        AuthorizeVoid/depositCancel.jsp 0x3040001 / 0x18 0x0C
        CheckCard/balanceInquiry.jsp tlamBalanceInquiry.jsp 0x3050001 / 0x16 0x01
        DircetIO/history.jsp 0x3060001 / 0x17 0x04

## Thinca������
Ӧ������

	/* enum Thincacloud::ThincaError, copyof_1329, width 4 bytes
		THINCA_S_SUCCESS  = 0
		THINCA_E_CANCEL  = 65h
		THINCA_E_RETURN_CARD_DATA  = 67h
		THINCA_E_INVALID_VALUE  = 0C9h
		THINCA_E_BUSY    = 0CAh
		THINCA_E_CAN_NOT_CANCEL  = 0CAh
		THINCA_E_NETWORK_ERROR  = 0CBh
		THINCA_E_NETWORK_TIMEOUT  = 0CCh
		THINCA_E_RW_ERROR  = 0CDh
		THINCA_E_ILLEGAL_STATE  = 0CEh
		THINCA_E_INVALID_CONFIG  = 0CFh
		THINCA_E_RW_CLAIMED_TIMEOUT  = 0D0h
		THINCA_E_RW_NOT_AVAILABLE  = 0D1h
		THINCA_E_RW_NOT_IC_CHIP_FORMATTING  = 0D2h
		THINCA_E_RW_UNSUPPORTED_VERSION  = 0D3h
		THINCA_E_INVALID_TERMINAL  = 12Dh
		THINCA_E_INVALID_MERCHANT  = 12Eh
		THINCA_E_INVALID_REQUEST  = 12Fh
		THINCA_E_INVALID_SERVICE  = 130h
		THINCA_E_FAIL_INIT_AUTH_TERM  = 131h
		THINCA_E_FAIL_REMOVE_TERM  = 132h
		THINCA_E_FAIL_CLOSE_SALES  = 133h
		THINCA_E_FAIL_AUTH_CHARGE_TERM  = 134h
		THINCA_E_FAIL_AUTH_DEPOSIT_TERM  = 134h
		THINCA_E_LOG_FULL  = 135h
		THINCA_E_FAILED_DEAL  = 136h
		THINCA_E_FAILED_TRANSACTION  = 136h
		THINCA_E_BEFORE_TERMINAL_USE_START_DATE  = 137h
		THINCA_E_AFTER_TERMINAL_USE_END_DATE  = 138h
		THINCA_E_INSUFFICIENT_BALANCE  = 191h
		THINCA_E_DEFICIENCY_BALANCE  = 191h
		THINCA_E_DISCOVER_MULTIPLE_CARDS  = 192h
		THINCA_E_UNKNOWN_CARD  = 193h
		THINCA_E_CARD_TIMEOUT  = 194h
		THINCA_E_CARD_COMMAND_ERROR  = 195h
		THINCA_E_PAYMENT_LIMIT  = 196h
		THINCA_E_POSSESSION_LIMIT  = 197h
		THINCA_E_CHARGE_LIMIT  = 198h
		THINCA_E_DEPOSIT_LIMIT  = 198h
		THINCA_E_NOT_TRANSACTABLE_CARD_STATUS  = 199h
		THINCA_E_REQUIRE_PIN_AUTHORIZATION  = 19Ah
		THINCA_E_RETRY_PIN_AUTHORIZATION  = 19Bh
		THINCA_E_FAIL_CARD_AUTHORIZATION  = 19Ch
		THINCA_E_DIFFERENT_CARD  = 19Dh
		THINCA_E_FAIL_VOID  = 19Eh
		THINCA_E_INSUFFICIENT_POINT_BALANCE  = 19Fh
		THINCA_E_POINT_UNAVAILABLE_CARD  = 1A0h
		THINCA_E_ILLEGAL_CARD  = 1F5h
		THINCA_E_INVALID_CARD  = 1F6h
		THINCA_E_NEGATIVE_CARD  = 1F7h
		THINCA_E_EXPIRED_CARD  = 1F8h
		THINCA_E_MOBILE_PIN_LOCK  = 1F9h
		THINCA_E_INVALID_SESSION  = 259h
		THINCA_E_UNAUTHENTICATED_USER  = 25Ah
		THINCA_E_UNAUTHENTICATED_POSITION  = 25Bh
		THINCA_E_AUTHENTICATED_USER  = 25Ch
		THINCA_E_AUTHENTICATED_POSITION  = 25Dh
		THINCA_E_FAIL_TERMINAL_AUTH  = 25Eh
		THINCA_E_FAIL_USERL_AUTH  = 25Fh
		THINCA_E_FAIL_USERL_AUTH_UNREGISTERED  = 260h
		THINCA_E_FAIL_POSITION_AUTH  = 261h
		THINCA_E_FAIL_POSITION_AUTH_UNREGISTERED  = 262h
		THINCA_E_INVALID_POSITION  = 263h
		THINCA_E_FATAL_AUTH  = 2BBh
		THINCA_E_CARD_WITHDRAWAL  = 2BCh
		THINCA_E_UNCONFIRMED_STATUS  = 321h
		THINCA_E_CARD_UNCONFIRMED_STATUS  = 322h
		THINCA_E_TRANSACTION_UNCONFIRMED_STATUS  = 323h
		THINCA_E_FATAL   = 384h
		THINCA_E_SESSION_TIMEOUT  = 385h
		THINCA_E_ICAS_ERROR  = 386h
		THINCA_E_BRAND_CENTER_ERROR  = 387h
	*/