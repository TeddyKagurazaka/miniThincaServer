using miniThincaLib.Helper;
using System.Net;
using System.Text;

namespace miniThincaLib;
public class miniThinca
{
    public TcapHandler handler = new TcapHandler();
    public static Configuration config;

    /// <summary>
    /// 以指定IP地址初始化
    /// </summary>
    /// <param name="IpAddressStr">IP地址</param>
    public miniThinca(string IpAddressStr)
    {
        config = new Configuration(IpAddressStr);
    }

    /// <summary>
    /// 主入口
    /// </summary>
    /// <param name="Context">HTTP请求</param>
    public void Dispatcher(HttpListenerContext Context)
    {
        #region 初始化参数
        byte[] Input = null;
        string OutputStr = "";
        byte[] OutputBinary = new byte[] { };
        bool UseBinary = false;
        #endregion

        #region 获取Input
        try
        {
            using (var memstream = new MemoryStream())
            {
                Context.Request.InputStream.CopyTo(memstream);
                Input = memstream.ToArray();
            }
        }
        catch (Exception) {
            Context.Response.StatusCode = 500;
            Context.Response.Close();
            return;
        }
        #endregion

        #region 处理请求
        Logger.Log("================");
        Logger.Log("Request:" + Context.Request.RawUrl);
        Logger.Log("Content:" + (Input.Length == 0 ? "" : HexByteArrayExtensionMethods.ToHexString(Input)));
        var RequestRawUrlParam = Context.Request.RawUrl.Split('/',StringSplitOptions.RemoveEmptyEntries);

        if(RequestRawUrlParam.Length >= 1 && RequestRawUrlParam[0] == "thinca")
        {
            if (RequestRawUrlParam.Length == 1) //initAuth入口点
            {
                // case "/thinca":
                OutputStr = Newtonsoft.Json.JsonConvert.SerializeObject(new Models.Activation.ActivateClass());
                Context.Response.AddHeader("x-certificate-md5", "757cffc53b98fc903476de6a672a1000");
            }
            else
            {
                switch (RequestRawUrlParam[1])
                {
                    //SEGA部分
                    case "terminals":   //配置同步(initAuth后)
                        OutputStr = Newtonsoft.Json.JsonConvert.SerializeObject(new Models.Activation.initSettingClass("10"));
                        break;
                    case "counters":    //投币信息上传(initAuth后) 不用返回
                    case "statuses":    //状态信息上传(initAuth后) 不用返回
                    case "sales":
                        break;

                    //thinca部分
                    //initAuth部分
                    case "common-shop":
                        switch (RequestRawUrlParam[2])
                        {
                            case "initauth.jsp":    //机台信息登记
                                //OutputStr = string.Format("SERV=http://{0}/thinca/common-shop/stage2", ipAddress);
                                OutputStr = string.Format("SERV={0}", config.initAuthEndpoint);
                                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                                break;
                            case "emlist.jsp":      //获取支付业务地址
                                OutputStr = string.Format("SERV={0}", config.emStage2Endpoint);
                                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                                break;
                            case "stage2":
                                UseBinary = true;
                                OutputBinary = handler.HandleTcapRequest(
                                    requestMethod: TcapHandler.TcapRequestType.initAuth,
                                    Input: Input);
                                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                                break;
                            case "emstage2":
                                UseBinary = true;
                                OutputBinary = handler.HandleTcapRequest(
                                    requestMethod:TcapHandler.TcapRequestType.emStage2,
                                    Input:Input);
                                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                                break;
                        }
                        break;

                    //AuthorizeSales部分
                    case "emoney":
                        //"http://{0}/thinca/emoney/{1}/{2}/" -> [0] = thinca,[1] = emoney ,[2] = brandName,[3] = TermSerial,[4] = method
                        //此时RequestRawUrlParam[2]为品牌名(paseli transit edy等)
                        //简化一点 暂时不判断品牌名
                        switch (RequestRawUrlParam[4])
                        {
                            case "payment.jsp":
                                OutputStr = string.Format("SERV={0}",
                                    config.ReturnBrandPaymentStage2Address(RequestRawUrlParam[2], RequestRawUrlParam[3]));
                                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                                break;
                            case "balanceInquiry.jsp":
                                OutputStr = string.Format("SERV={0}",
                                    config.ReturnBrandPaymentStage2Address(RequestRawUrlParam[2], RequestRawUrlParam[3], "query_stage2"));
                                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                                break;
                            case "stage2":
                                UseBinary = true;
                                OutputBinary = handler.HandleTcapRequest(
                                    requestMethod: TcapHandler.TcapRequestType.AuthorizeSales,
                                    Input: Input,
                                    brandName: RequestRawUrlParam[2],
                                    termSerial: RequestRawUrlParam[3]);
                                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                                break;
                            case "query_stage2":
                                UseBinary = true;
                                OutputBinary = handler.HandleTcapRequest(
                                    requestMethod: TcapHandler.TcapRequestType.BalanceInquire,
                                    Input: Input,
                                    brandName: RequestRawUrlParam[2],
                                    termSerial: RequestRawUrlParam[3]);
                                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                                break;
                        }
                        break;
                }
            }
        }

        #endregion

        #region 发送回复
        if (!UseBinary)
            OutputBinary = Encoding.UTF8.GetBytes(OutputStr);

        Logger.Log("Response:" + (OutputBinary.Length == 0 ? "" : HexByteArrayExtensionMethods.ToHexString(OutputBinary)));

        try
        {
            Context.Response.StatusCode = 200;
            Context.Response.OutputStream.Flush();
            Context.Response.OutputStream.Write(OutputBinary, 0, OutputBinary.Length);
            Context.Response.OutputStream.Flush();
            Context.Response.Close();
            Logger.Log("================");
        }
        catch (Exception ex)
        {
            Logger.Log("Error:" + ex.Message);
        }
        #endregion

        return;
    }
}

