using System;
using System.Net;

namespace miniThincaLib
{
	public class Configuration
	{
		/// <summary>
		/// 本机IP地址
		/// </summary>
		string _ipAddress = "127.0.0.1";

		/// <summary>
		/// 可用的emoney brand开关
		/// </summary>
		Dictionary<Models.ThincaBrandType, bool> _brandSwitch =
			new Dictionary<Models.ThincaBrandType, bool>()
		{
			{Models.ThincaBrandType.Nanaco,false},
            {Models.ThincaBrandType.Edy,false},
            {Models.ThincaBrandType.Id,false},
            {Models.ThincaBrandType.Quicpay,false},
            {Models.ThincaBrandType.Transport,false},
            {Models.ThincaBrandType.Waon,false},
            {Models.ThincaBrandType.Nanaco2,false},
            {Models.ThincaBrandType.Paseli,true},
            {Models.ThincaBrandType.Sapica,false},
        };

		/// <summary>
		/// 机台信息同步地址(SEGA用) -> Models.Activation.endpointClass
		/// </summary>
		public string terminalEndpoint { get { return string.Format("http://{0}/thinca/terminals", _ipAddress); } }

        /// <summary>
        /// 机台状态上报地址(SEGA用) -> Models.Activation.endpointClass
        /// </summary>
        public string statusesEndpoint { get { return string.Format("http://{0}/thinca/statuses", _ipAddress); } }

        /// <summary>
        /// 机台销售上报地址(SEGA用) -> Models.Activation.endpointClass
        /// </summary>
        public string salesEndpoint { get { return string.Format("http://{0}/thinca/sales", _ipAddress); } }

        /// <summary>
        /// 机台投币上报地址(SEGA用) -> Models.Activation.endpointClass
        /// </summary>
        public string countersEndpoint { get { return string.Format("http://{0}/thinca/counters", _ipAddress); } }

		/// <summary>
		/// Thinca初始化认证地址(Thinca用) -> resource.xml/common-shop/initauth.jsp
		/// </summary>
		public string initAuthEndpoint { get { return string.Format("http://{0}/thinca/common-shop/stage2", _ipAddress); } }
        /// <summary>
        /// Thinca获取支付api地址(Thinca用) -> resource.xml/common-shop/emlist.jsp
        /// </summary>
        public string emStage2Endpoint { get { return string.Format("http://{0}/thinca/common-shop/emstage2", _ipAddress); } }

        /// <summary>
        /// 返回可用的EMoeny ID(SEGA: Models.Activation.initSettingClass, Thinca: SecurityMessage.AdditionalSecurityMessage
        /// </summary>
        public List<int> availableEMoney
		{
			get
			{
				List<int> brandArray = new List<int>();
				foreach(var brand in _brandSwitch)
				{
					if (brand.Value == true)
						brandArray.Add((int)brand.Key);
				}
				return brandArray;
			}
		}

        /// <summary>
        /// 返回可用的EMoeny 状态(Thinca: SecurityMessage.AdditionalSecurityMessage
        /// </summary>
        public List<int> availableEMoneyResultCode
		{
			get
			{
                List<int> brandArray = new List<int>();
                foreach (var brand in _brandSwitch)
                {
                    if (brand.Value == true)
                        brandArray.Add(1);
                }
                return brandArray;
            }
		}

		/// <summary>
		/// 以指定IP地址初始化类
		/// </summary>
		/// <param name="ipAddress">本机IP地址</param>
        public Configuration(string ipAddress)
		{
			_ipAddress = ipAddress;
		}

		/// <summary>
		/// 将BrandID转换成Brand Name
		/// </summary>
		/// <param name="brandType">BrandID</param>
		/// <returns></returns>
		string BrandTypeToBrandName(Models.ThincaBrandType brandType)
		{
			switch (brandType)
			{
				case Models.ThincaBrandType.Edy:
					return "edy";
				case Models.ThincaBrandType.Id:
					return "id";
				case Models.ThincaBrandType.Nanaco:
					return "nanaco";
				case Models.ThincaBrandType.Nanaco2:
					return "nanaco2";
				case Models.ThincaBrandType.Paseli:
					return "paseli";
				case Models.ThincaBrandType.Quicpay:
					return "quicpay";
				case Models.ThincaBrandType.Sapica:
					return "sapica";
				case Models.ThincaBrandType.Transport:
					return "transport";
				case Models.ThincaBrandType.Waon:
					return "waon";
				default:
					return "others";
			}
		}

		/// <summary>
		/// 返回EMoney TCAP Endpoint地址
		/// </summary>
		/// <param name="brandName">brand名(BrandTypeToBrandName)</param>
		/// <param name="TermSerial">机器ID,用于区分</param>
		/// <param name="stage2">stage2名</param>
		/// <returns></returns>
		public string ReturnBrandPaymentStage2Address(string brandName,string TermSerial,string stage2 = "stage2")
			=> string.Format("http://{0}/thinca/emoney/{1}/{2}/{3}",_ipAddress,brandName,TermSerial,stage2);

		/// <summary>
		/// 返回EMoney TLAM Endpoint列表
		/// </summary>
		/// <param name="TermSerial">机器ID,用于生成可区分地址</param>
		/// <returns></returns>
		public List<string> ReturnBrandUrl(string TermSerial)
		{
            List<string> brandArray = new List<string>();
			foreach (var brand in _brandSwitch)
			{
				if (brand.Value == true)
					brandArray.Add(
					  string.Format("http://{0}/thinca/emoney/{1}/{2}/",
					  _ipAddress,
					  BrandTypeToBrandName(brand.Key),
					  TermSerial)
				  );
            }
			return brandArray;
		}
    }
}

