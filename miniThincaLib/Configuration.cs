using System;
using System.Net;

namespace miniThincaLib
{
	public class Configuration
	{
		string _ipAddress = "127.0.0.1";

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


		public string terminalEndpoint { get { return string.Format("http://{0}/thinca/terminals", _ipAddress); } }
		public string statusesEndpoint { get { return string.Format("http://{0}/thinca/statuses", _ipAddress); } }
		public string salesEndpoint { get { return string.Format("http://{0}/thinca/sales", _ipAddress); } }
		public string countersEndpoint { get { return string.Format("http://{0}/thinca/counters", _ipAddress); } }

		public string initAuthEndpoint { get { return string.Format("http://{0}/thinca/common-shop/stage2", _ipAddress); } }
		public string emStage2Endpoint { get { return string.Format("http://{0}/thinca/common-shop/emstage2", _ipAddress); } }

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

        public List<string> availableEMoneyUrl
        {
            get
            {
                List<string> brandArray = new List<string>();
                foreach (var brand in _brandSwitch)
                {
                    if (brand.Value == true)
                        brandArray.Add(
							string.Format("http://{0}/thinca/emoney/{1}/",
							_ipAddress,
							BrandTypeToBrandName(brand.Key))
						);
                }

                return brandArray;
            }
        }


        public Configuration(string ipAddress)
		{
			_ipAddress = ipAddress;
		}

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

		public string ReturnBrandPaymentStage2Address(string brandName,string stage2 = "stage2")
			=> string.Format("http://{0}/thinca/emoney/{1}/{2}",_ipAddress,brandName,stage2);
    }
}

