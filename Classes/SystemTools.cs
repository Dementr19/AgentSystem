using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace AgentSystem.Classes
{
    class SystemTools
    {
        public static List<string> Log = new List<string>();
        static object log_locker = new object();


        public static void WriteLog(string str)
        {
            lock (log_locker)
            {
                Log.Add($"[{DateTime.Now}] {str}");
            }
        }

        public static void Write(string str)
        {
            Console.Write(str);
        }

        public static void Writeln(string str = "")
        {
            Console.WriteLine(str);
        }

        public static void ClearLog()
        {
            Log.Clear();
        }
        public static void SaveLog()
        {
            string writePath = "agent_system.log";
            Console.WriteLine($"\nЗапись лога в файл {writePath}");
            try
            {
                using (StreamWriter sw = new StreamWriter(writePath, false, System.Text.Encoding.Default))
                {
                    foreach(string str in Log)
                        sw.WriteLine(str);
                }
                Console.WriteLine($"\nЗапись лога завершена успешно.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static int TimeNowMs()
        {
            return GetMsFromTime(DateTime.Now);
        }

        public static int DeltaTimeMs(int prev_time)
        {
            int dt = TimeNowMs() - prev_time;
            return dt < 0 ? dt + 60000 : dt; //коррекция секунд при переходе через ноль (59->60(0)->1)
        }

        public static int GetMsFromTime(DateTime time)
        {         
            return time.Second * 1000 + time.Millisecond;
        }
    }
}
