# 花活
这里会尽可能的尝试解释ThincaPayment的原理，这样你就能整些花活了

## 注册流程
此处以从未碰过EMoney的机器为例

在机器选择 游戏-电子支付信息-机台认证-开始认证 后，机台会先检查VFD连接，连接正常后开始读授权卡流程

授权卡的参数如下:

	授权卡本身为Mifare Classic类型，也就是刷你家门禁或者门锁的那张卡。
	卡片密码为 thinca 转16进制 (74 68 69 6E 63 61)
	其中包含以下内容：
		storeCardId(offset+0 16byte 明文)
		merchantCode(offset+16 20byte 明文)
		storeBranchNumber(offset+36 5byte 明文)
		证书密码(offset+41 16byte passphrase开头的明文)
		之后填充00到128 byte

	这一块内容会进行加密：
		Key:E8ADB54FBE8D2DC44A16C6B339A06457C3A1253ED8910D6BA666A77F5A3E1FFC
		IV:2B0C9071661C20AFEC7061CC639E7BAB
		Method:AES CBC

	加密后得到128byte 密文，将其写入卡片的Sector 1 第二排到 Sector 3 的第三排
	设置Sector 1的第一排为 54 43 02 01(修改这一排好像会触发比如proxySetting之类的情况，会导致验证不通过，一般不动)

	完工。

如果你用的是Repo里的thincamod4.mct，这个文件自带的参数是这样：

	storeCardId:1144551419198100
	merchantCode:11451419191145141919
	storeBranchNumber:11514
	证书密码:passphrase114514
	不包含Proxy设置

完成读卡以后，机器会将参数发往 tfps-res-pro/env.json 的 tasms.root_endpoint。如果你有改env.json的话服务器这时候就会有请求了。请求的内容如下：
	
	User-Proxy:SEGA AMUSEMENT
	{
		"modelName":"ACA",			//ALLS HX2
		"serialNumber":"ACAE01A9999",		//Mainboard ID
		"merchantCode":"11451419191145141919",	//看上面
		"storeBranchNumber":11514,
		"storeCardId":"1144551419198100"
	}

服务器收到请求后下发初始设置，内容如下

	Header: "x-certificate-md5" = "证书Base64前的MD5"
	{
		certificate : "",	//客户端请求证书,pkcs#7格式,授权卡读到的密码要能解开这个证书
					//此处要附上证书的base64后内容，原始内容计算MD5以后设置到header上
		initSettings :{
			endpoints : {
				terminals:{	//同步配置地址，机器会请求该地址更新initSettings
					uri: ""	//地址
				},
				statuses:{ 同terminals },	//状态上报地址，进待机前会发一次
				sales:{ 同terminals },		//销售上报地址，定时发送
				counters:{ 同terminals }	//投币上报地址，进待机前，每盘游戏结束时发送
			},
			intervals : {
				checkSetting : 3600,	//同步配置间隔(秒)
				sendStatus : 3600	//上报状态间隔(秒)
			},
			settigsType : "AmusementTerminalSettings", //锁死，不能改
			status : "00",	//结果码，可以使用以下情况
					//00:完成认证(初始认证时用)
					//10:正常(刷新配置时用，此时才能触发刷钱包)
					//11:暂停
					//12:维护
					//80:暂不可用
					//90:已撤除(撤除时用)
			terminalId : "111122223333444455556666777788",	//终端ID，显示在设置菜单，必须为数字
			version : "2023-01-01T12:34:56",	//版本号，可以按照这个格式乱改，应该是在statuses回传
			availableElectronicMoney : [ 8 ],	//可以使用的电子钱包的BrandID，参照下面
			cashAvailability : true,	//机器能不能接受投币?(不是很懂)
			productCode : 1001	//产品代码?
		}
	}

如果请求没有问题，客户机会将证书文件以 {terminalId}.p12 格式保存在 AMFS:\\emoney 文件夹下，然后对Thinca层设置证书，并触发 Thinca->initAuth()

<b>注意:所有的Thinca请求 必须通过特制TCAP包通讯才能获得识别机器的参数，其他位置(header body)均无参数。</b>

客户机此时会开始请求 \{commonPrimaryUri\}/initAuth.jsp (TLAM Metadata) <br/> \{commonPrimaryUri\} 为 tfps-res-pro/resource.xml 的 /thincaResource/common/commonPrimaryUri

此时需要回复TCAP通讯地址，以本项目为例的内容如下：
	
	SERV=http://127.0.0.1/thinca/common-shop/stage2
	//此时也可将SERV换成COMM，但这样请求的话需要所有的请求包Header都改为0201
	//游戏以第一个读到的地址为准，同时放置两个无效
	//这个包需要设置Content-Type : "application/x-tlam" 否则不认

客户机请求该地址触发TCAP通讯。<br /><i>所有的TCAP包都要设置 Content-Type : "application/x-tcap" 否则不认。</i>

	第一组包必然为Handshake，返回HandshakeAccept包。
	完成Handshake后，客户机转入Idle状态，此时必然发送空，此时返回OperateDeviceMessage包获取机器参数并设置机器状态。
	OperateDeviceMessage包必然会有返回，完成所有设置以后即可发送Farewell包完成TCAP通讯。

	注意:只有这里(通过OperateDeviceMessage REQUEST包)可以获得机器参数！

如此处包格式出现错误（无法解析包、参数不规范等），游戏会发回Error包，同时机器叫三声，提示注册失败。

如未正确通过OperateDeviceMessage设置机器状态，同样会引发Thinca层因nullptr access报错，或无法进入成功状态导致读卡器闪蓝灯（回到开头）。

完成initAuth.jsp后，机台会请求 \{commonPrimaryUri\}/emlist.jsp 用于获取付款Api，流程和initAuth.jsp一样。<br/><b>注意：如果想在后面的支付环节获得识别机台的方式，此时下发的URL必须包含能识别机台的参数，否则除非重新认证没有其他办法可以获取。</b>

完成以上步骤后，机台将以上相关参数写入NVRAM(sysfile.dat 0x2000位置)，回到电子支付信息菜单，此时能看到ID和支持Brand，完成注册。

<i>TCAP包流程建议直接看代码，这要是每个包都写出来这玩意要塞爆</i>

## 支付流程
游戏弹出投币提示后就会开始调用支付流程。<br /><i>如果不跳（比如只让你投币），检查terminals回传的json有没有设置status为10</i>

此时根据选择的Brand类型，游戏开始请求认证时获取的地址(TLAM Metadata 别忘了设置ContentType)，一样先回复TCAP通讯地址。

然后触发TCAP通讯。<b>所有的支付流程都在TCAP层完成然后带Receipt返回游戏，不带Receipt返回只会让游戏重新开始支付流程。</b>

	第一组必然Handshake，返回HandshakeAccept
	第二组必为空，使用OperateDeviceMessage包开始调用读卡器/VFD显示刷卡信息
	OperateDeviceMessage必然会有返回，解析返回结果来获取卡号/SeqNumber/支付额，通过OperateDeviceMessage返回支付状态包(相当于Receipt)
	完事以后返回Farewell包完成TCAP通讯

Aime读卡器读卡必然带阻塞，要么带卡号返回，要么因为超时发送空包。注意如果超时时间过长会出现游戏等amd返回的情况。

TCAP通讯结束时游戏会根据结果决定是重新请求 or 返回上一层 or 记录支付信息并投币。

<i>这一部分也建议直接看代码</i>

## 关于BrandID
Thinca和SEGAY使用的Brand ID会有一些不同

		(SEGA)	(THINCA)
	Nanaco:	1	1
	Edy:	2	2
	Id:	3	3
	交通卡:	4	5
	WAON:	5	6
	PASELI:	6	8
	SAPICA:	7	7(NANACO2)
	NUM:	8	9(SAPICA)

	QuicPay	(不支持)	4

## 关于TCAP包
TCAP包的格式如下：<br/><i>所有涉及到数字的位置均为大端序 </i>

	(02 05)(类型 1byte)(body的总长度 2byte)(body ?byte)
	//如果你在TLAM请求时 返回的是COMM而不是SERV，此时包头应改为02 01

	可用的类型如下：
	01 : Handshake
	02 : Farewell
	03 : Error
	04 : AppDataTransfer
	05 : UpdateEntity
	06 : OperateEntity

	TCAP允许一个包塞数个body，只要都属于同一个类型

Body包的格式如下：
	
	(methodLo 1byte) (paramLo 1byte) (paramHi 1byte) (methodHi 1byte) (messageBodyLength 2byte) (message ?byte)

	methodLo | methodHi 组成 Int16 作为 method，可用的method如下:
	00 Default:(其他包Parse失败时回落)
		00 00 : RequestMessage (不可以有message)
		00 01 : RequestWarningMessage
		其他  : RequestUnknownMessage 

	01 Handshake:	(必须按 23 -> 81 -> 24 的顺序，否则报错)
		00 23 : RequestMessage	(不可以有message)
		00 24 : RequestMessage	(不可以有message)
		00 81 : RequestAcceptMessage (message必须为 02 05 00)

	02 Farewell:	(必须按 23 -> 25 -> 24 的顺序，否则报错)
		00 23 : RequestServerGoodByeMessage (不可以有message)
		00 24 : RequestServerGoodByeDoneMessage (不可以有message)
		00 25 : RequestReturnCodeMessage (message固定为4位byte 组成return code)

	03 Error:
		00 21 : RequestPacketFormatErrorMessage
		00 22 : RequestIllegalStateErrorMessage
		00 23 : RequestUnexpectedErrorMessage

	04 AppDataTransfer:
		01 01 : RequestFelicaCommandMessage (需要有1~255byte内的message)
		01 04 : RequestFelicaPrecommandMessage (需要有1~255byte内的message)
		01 05 : RequestFelicaExcommandMessage (需要有3~255byte内的message)
		01 06 : RequestFelicaCommandThrurwMessage  (需要有3~255byte内的message)
		01 09 : RequestFelicaPrecommandThrurwMessage (需要有4~255byte内的message)
		01 0A : RequestFelicaExcommandThrurwMessage (需要有4~255byte内的message)

	05 UpdateEntity: (接受后随下一个包返回)
		00 30 : RequestRequestIdMessage (2byte message，回传内容和message一致)
		00 81 : RequestSetNetworkTimeoutMessage (4byte message)
		01 01 : RequestFelicaSelectInternalMessage (不可传message)
		01 81 : RequestFelicaSetTimeoutMessage (4byte message)
		01 82 : RequestFelicaSetRetryCountMessage (4byte message)

	06 OperateEntity: (接受后随下一个包返回) (需要 paramLo | paramHi = param)
		00 25 : RequestOperateDeviceMessage (这个包下面讲）
		00 81 : RequestPlaySoundMessage (不处理参数)
		01 01 : RequestFelicaOpenRwRequestMessage (不可传message，param 必须为00 04)
		01 05 : RequestFelicaCloseRwRequestMessage (不可传message，param 必须为00 04)

客户机请求服务器的包除Body包会出现的method外，还会有以下情况：
	
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
	?? (其他我没找出来在哪个类型的):
		00 02 : ResponseFelicaOpenRwStatusMessage/ResponseFelicaResponseMessage
		00 03 : ResponseFelicaSelectedDeviceMessage/ResponseFelicaErrorMessage
		00 06 : ResponseFelicaCloseRwStatusMessage
		00 07 : ResponseFelicaResponseThrurwMessage
		00 08 : ResponseFelicaErrorThrurwMessage

因为客户机请求包格式和服务器回传包格式一致，可以直接Parse后获得客户机的请求信息，尤其是 ResponseDevicesMessage 会包含可用于OperateDeviceMessage的参数，有能力可以处理一下。		

## 关于OperateEntity:RequestOperateDeviceMessage
OperateDeviceMessage可以直接操作ResponseDevicesMessage所列举的设备，有时候操作相关设备才能完成整个流程。

此时message的格式为 (COMMAND 长度 1byte)(COMMAND 字符串)(00 00)(Payload 长度+2 2byte)(Payload 长度 2byte)(Payload ?byte) <br />
如果没有Payload，Command结束后后跟(00 00 00 00)即可。<br />
有些Command会要求省略(Payload长度 + 2)部分，下面会注明。

以SEGAY为例，一般ResponseDevicesMessage会发送以下设备信息：

	00 01 -> General Client
	00 02 -> General Status
	00 03 -> General Option
	00 04 -> Felica R/W
	00 05 -> Generic R/W Event
	00 06 -> Generic R/W Status
	00 07 -> Generic R/W Option
	00 08 -> Gemeroc NFC RW

在构造OperateDeviceMessage包的时候，所设置的param代表对以上指定设备的操作。<br />
<i>所以RequestFelicaOpenRwRequestMessage 必须设置param为00 04,因为只可能对Felica R/W操作</i>

以下是测试可用的情况

	00 01 -> General Client
		REQUEST: 返回当前系统信息,XML格式.(其中会包含AdditionalJson等信息，很重要)，不要求Payload
		CURRENT: 设置系统信息,XML格式.(用于更新AdditionalJson和回传状态码等，很重要）
			(initAuth时候Payload要求前跟(EF BB BF) 3 byte，Payment时不要求）
		RESULT: 效果和CURRENT一致
		CANSEL: 未知
		TIMESTAMP: 返回设备侧当前时间
		WAIT: 等待指定长度时间，Payload为8byte Int64。
		UNIXTIME: 返回设备侧UNIX时间戳
		UNIXTIMEWAIT: 根据UNIX时间戳等待，Payload为10byte(8byte Int64)(2byte)
		STATUS: 返回 illegal general client

	00 02 -> General Status
		STATUS: 效果未知，Payload为6byte(00 00)(00 02)(00 01)

	00 03 -> General Option
		STATUS: 触发ioEvent，Payload为(00 00 00 00 00 00 00 00)(明文Json)

	00 08 -> Generic NFC RW (操作Aime读卡器)
		OPEN_RW: 开启读卡器(Payload为3byte, (00 00)(01:Mifare Only?,08:Felica Only?,09:Both?))
		CLOSE_RW: 关闭读卡器(不要求Payload)
		TARGET_DETECT: 检测卡片(Payload 4byte 最长等待时间(毫秒))
			此方法会塞死，要么超时要么读到卡片信息
		APDU_COMMAND:? (Payload 最少3byte (code1)(code2 2byte)(data?) (其中code2 + 3 <= length))
		FELICA_COMMAND:? (Payload 最少7byte (code1 4byte)(code2 1byte)(code3 3byte) (code3 + 7 <= length)
		OPTION:? (Payload 6byte (code1 4byte)(code2 2byte) (code2 > 0 时看起来会发生什么))

## 关于CURRENT 设置的Xml和AdditionalSecurityInformation
在这里返回ResultCode,ResultCodeExtended,Balance,还有初始化的时候会读取AdditionalSecurityInformation来获得BrandUrlMap之类的。

看代码吧，说起来麻烦。

## 关于ioEvent
ioEvent是个Json,大约格式是这样

	{
		deviceType : 0,	//ioEvent类型，可选: 
				//1 : MessageEvent	(在VFD上显示信息）
				//2 : SoundEvent		(播放声音）
				//3 : LedEvent		(控制LED应该，但是对Aime没用）
				//16 : AmountEvent	(显示要支付的金额，但是没调起来)
				//32 : EnableCancelEvent
				//48 : ClientIoSaveDealNumberEvent
				//64 : ProgressEvent
		deviceNumber : 0,
		actionType : 0,
		sendData : [],	//ioEvent参数，要将byte逐位转为signed int，有以下情况:
				//MessageEvent {(brandType 2byte),(messageId 2byte),(timeout? 2byte)}
				//(此时系统从Resource.xml找对应brand和id的message显示在vfd上)
				//
				//SoundEvent {(brandType 2byte),(soundId 1byte),(timeout 2byte)}
				//(播放指定brand和id的sound文件)
				//
				//LedEvent {0,0,timeOut(2byte),param2(2byte),param3(2byte),param4(2byte)}
				//(因为没触发所以不知道有啥用，但是deviceNumber 0=FFFF0000 1=FF00FF00 2=FF0000FF 3=00000000 4=FFFFFFFF)
				//
				//AmountEvent {(brandType 2byte)(id 1byte)(amount 4byte)(amountBalance 4byte)(timeout 2byte)}
				//(找指定brand和id的amount字符串 用于显示支付金额)
				//
				//EnableCancelInfo 不需要参数
				//
				//SaveDealNumberIo {(brandID 2byte)(dealNumber string)}
		screen : false
	}

## Thinca方法
挂IDA的时候看大数字会好用

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

## Thinca错误码
应该有用

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