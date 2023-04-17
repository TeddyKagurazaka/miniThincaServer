using System.Net;
using System.Text;

namespace miniThincaLib;
public class miniThinca
{
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
        catch (Exception e) {
            Context.Response.StatusCode = 500;
            Context.Response.Close();
            return;
        }
        #endregion

        #region 处理请求
        switch (Context.Request.RawUrl)
        {
            //SEGA部分
            case "/thinca":             //initAuth入口点
                break;
            case "/thinca/terminals":   //配置同步(initAuth后)
                break;
            case "/thinca/counters/ACAE01A9999":    //投币信息上传(initAuth后) 不用返回
            case "/thinca/statuses/ACAE01A9999":    //状态信息上传(initAuth后) 不用返回
                break;

            //thinca部分
            case "/thinca/common-shop/initauth.jsp":    //机台信息登记
                OutputStr = "SERV=http://192.168.31.201/thinca/stage2";
                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                break;
            case "/thinca/common-shop/emlist.jsp":      //获取支付业务地址
                OutputStr = "SERV=http://192.168.31.201/thinca/emstage2";
                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                break;
            case "/thinca/stage2":      //TCAP通讯 initauth.jsp

                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                break;
            case "/thinca/emstage2":    //TCAP通讯 emlist.jsp

                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                break;

            //PASELI支付部分
            case "/thinca/emoney/paseli/payment.jsp":   //PASELI支付
                OutputStr = "SERV=http://192.168.31.201/thinca/paseli/stage2";
                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                break;
            case "/thinca/emoney/paseli/balanceInquiry.jsp":    //PASELI余额查询
                OutputStr = "SERV=http://192.168.31.201/thinca/paseli/query_stage2";
                Context.Response.AddHeader("Content-Type", "application/x-tlam");
                break;
            case "/thinca/paseli/stage2":   //TCAP通讯 PASELI支付

                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                break;
            case "/thinca/paseli/query_stage2": //TCAP通讯 PASELI余额查询

                Context.Response.AddHeader("Content-Type", "application/x-tcap");
                break;


        }
        #endregion

        #region 发送回复
        if (!UseBinary)
            OutputBinary = Encoding.UTF8.GetBytes(OutputStr);
        
        try
        {
            Context.Response.StatusCode = 200;
            Context.Response.OutputStream.Flush();
            Context.Response.OutputStream.Write(OutputBinary, 0, OutputBinary.Length);
            Context.Response.OutputStream.Flush();
            Context.Response.Close();
        }
        catch (Exception ex)
        {
            
        }
        #endregion

        return;
    }
}

