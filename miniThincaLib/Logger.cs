namespace miniThincaLib
{
    internal class Logger
    {
        public enum LogLevel
        {
            Error = 0,
            Info = 1,
            Debug = 2
        }

        public static LogLevel currentLevel = LogLevel.Debug;

        /// <summary>
        /// 打Log
        /// </summary>
        /// <param name="Content">内容</param>
        /// <param name="lvl">Log等级</param>
        public static void Log(string Content,LogLevel lvl = LogLevel.Debug)
        {
            if(currentLevel >= lvl)
            {
                string msg = string.Format("[{0}][{1}]{2}",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),lvl.ToString(),Content);
                Console.WriteLine(msg);
            }
        }
    }
}
