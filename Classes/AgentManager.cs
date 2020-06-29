using System;
using System.Collections.Generic;
using System.Text;

using System.Linq;
using System.IO;

//пользовательские классы
using static AgentSystem.Classes.SystemTools;
using AgentSystem.ReportCommandClasses;
using System.Reflection;
using System.Threading;
using System.Linq.Expressions;

namespace AgentSystem.Classes
{
    class AgentManager
    {
        public Agency agency;
        public DataBuffer inputStringsBuffer = new DataBuffer();
        public DataBuffer stationDataBuffer = new DataBuffer();
        public Context context = new Context();
        private bool testMode = false;
        private bool configCorrect = false;
        private string configMessage = "";
        string cmdStr = "serv>";


        public AgentManager(string[] args)
        {
            agency = new Agency(context,"Agency");

            context.InputStrings = inputStringsBuffer;
            context.StationDataBuffer = stationDataBuffer;
            (configCorrect, testMode, configMessage) = context.Load(args);
        }

        internal Context Context
        {
            get => default;
            set
            {
            }
        }
        internal Agency Agency
        {
            get => default;
            set
            {
            }
        }
        
        public void Run()
        {
            Console.Clear();
            Writeln("Программный модуль приёма и обработки данных (ПМ ПиОД)\nCервер AgentSystem, (R) 2020, Кабанов Алексей, НИУ МИЭТ");
            Writeln(configMessage);
            if (configCorrect)
            {
                if (testMode) SelectTest();
                else StartServer();
            }
        }

        private void StartServer()
        {
            StartAgents(Roles.DataTcpListener, context.DefaultAgentCount.Data_receiver);
            StartAgents(Roles.InputStringParser, context.DefaultAgentCount.Parser);
            StartAgents(Roles.ToDBWriter, context.DefaultAgentCount.Writer);
            StartAgents(Roles.CommandTcpListener, context.DefaultAgentCount.Commander);
            StatisticAll();
            Write($"{cmdStr}");
            while (true)
            {
                string cmd = Console.ReadLine();
                cmd = cmd.ToLower();
                if (cmd == "test" || cmd == "stop" || cmd == "help")
                {
                    if (cmd == "test") SelectTest();
                    if (cmd == "help")
                        Writeln("Допустимы команды: test, stop, help");
                    if (cmd == "stop")
                    {
                        StopAllAgents();
                        TheEnd();
                        return;
                    }
                }
                else Writeln("Нет такой команды");
                Write($"{cmdStr}");
            }
        }

        private void SelectTest()
        {
            //int input;
            while (true)
            {
                Writeln("\nВыберите номер теста:");
                Writeln(" 0. Выход");
                Writeln(" 1. Сценарий запуска 1-5-10");
                Writeln(" 2. Запуск TCP-сервера");
                Writeln(" 3. Остановка TCP-сервера");
                Writeln(" 4. Запуск агентов-парсеров");
                Writeln(" 5. Остановка агентов-парсеров");
                Writeln(" 6. Запуск агентов записи в БД");
                Writeln(" 7. Остановка агентов записи в БД");
                Writeln(" 8. Остановка всех агентов");
                Writeln(" 9. Сохранение лога");
                Writeln("10. Статистика по всем агентам");
                Writeln("11. Заполнение входного буфера из файла");
                Writeln("12. Статистика по tcp-серверам");
                Writeln("13. Запуск командного TCP-сервера");
                Writeln("14. Остановка командного TCP-сервера");
                Writeln("15. Мониторинг загрузки буферов");

                Writeln();
                Write($"{cmdStr}");
                string line = Console.ReadLine();
                Writeln();
                if (!Int32.TryParse(line, out int input)) input = 30;
                switch (input)
                {
                    case 0:
                        TheEnd();
                        return;
                    case 1:
                        Script_1_5_10();
                        break;
                    case 2:
                        StartTcp();
                        break;
                    case 3:
                        StopTcp();
                        break;
                    case 4:
                        StartStringParser();
                        break;
                    case 5:
                        StopStringParser();
                        break;
                    case 6:
                        StartDbWriter();
                        break;
                    case 7:
                        StopDbWriter();
                        break;
                    case 8:
                        StopAllAgents();
                        break;
                    case 9:
                        SaveLog();
                        break;
                    case 10:
                        StatisticAll();
                        break;
                    case 11:
                        FillInBuff();
                        break;
                    case 12:
                        StatisticTcp();
                        break;
                    case 13:
                        StartTcpCommand();
                        break;
                    case 14:
                        StopTcpCommand();
                        break;
                    case 15:
                        MonitoringBufferLoad();
                        break;
                    default:
                        Console.WriteLine($"Неправильный ввод {{{line}}}, попробуйте ещё раз.");
                        break;
                }
                Console.WriteLine("\nPress any key...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        void TheEnd()
        {
            StopAllAgents();
            SaveLog();
            Writeln("Завершение работы программы");
            //Environment.Exit(0); 
        }

        void Script_1_5_10()
        {
            StartAgents(Roles.DataTcpListener, 1);
            StartAgents(Roles.InputStringParser, 5);
            StartAgents(Roles.ToDBWriter, 10);
        }

        void MonitoringBufferLoad()
        {
            int num = 0;
            // устанавливаем метод обратного вызова
            TimerCallback tm = new TimerCallback(CheckBufferLoad);
            // создаем таймер
            Timer timer = new Timer(tm, num, 0, 1000);
            while (Console.ReadKey().Key != ConsoleKey.Enter) { }
            //отключение таймера
            timer.Change(0, Timeout.Infinite);
        }
        void CheckBufferLoad(object x)
        {
            int cntInString = inputStringsBuffer.GetCount();
            int cntDataDB = stationDataBuffer.GetCount();
            WriteLog($"strbuff = {cntInString}, stationbuff = {cntDataDB}");
            Writeln($"strbuff = {cntInString}, stationbuff = {cntDataDB}");
        }



        void FillInBuff()
        {
            //ClearLog();
            string filename = context.AppSettings["test_mode:input_strings_file"];       //"moscow-2010.txt";


            //функции для отладки
            //заполняет очередь строками из указанного файла
            try
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    inputStringsBuffer.Items.Clear();
                    string line = "";
                    DateTime beg_time = new DateTime();
                    beg_time = DateTime.Now.AddMinutes(-200);
                    int str_cnt = 0;
                    while ((line = sr.ReadLine()) != null)
                    {
                        DateString dstr = new DateString
                        {
                            Time = new DateTime(),
                            Line = line
                        };
                        dstr.Time = beg_time.AddMilliseconds(str_cnt++);
                        ////dstr.Line = $"{str_cnt++:d5}: {line}";
                        dstr.Line = line;
                        inputStringsBuffer.PutItem(dstr);
                    }
                }
            }
            catch 
            {
                Writeln($"Не удалось прочитать файл {filename}");
                return;
            }
            Writeln($"Входной буфер строками из файла {filename} успешно загружен.");
        }
        

        void StartTcp()
        {
            StartAgents(Roles.DataTcpListener);
        }

        void StopTcp()
        {
            StopAgents(Roles.DataTcpListener);
        }

        void StartTcpCommand()
        {
            StartAgents(Roles.CommandTcpListener, 1);
        }

        void StopTcpCommand()
        {
            StopAgents(Roles.CommandTcpListener, 1);
        }

        void StartStringParser()
        {
            StartAgents(Roles.InputStringParser);
        }

        void StopStringParser()
        {
            StopAgents(Roles.InputStringParser);
        }
        
        void StartDbWriter()
        {
            StartAgents(Roles.ToDBWriter);
        }

        void StopDbWriter()
        {
            StopAgents(Roles.ToDBWriter);
        }

        void StopAllAgents()
        {
            Writeln($"Статистика по всем агентам:\n");
            foreach (var role in Enum.GetValues(typeof(Roles)).Cast<Roles>())
            {
                StopAgents(role,100);
            }
        }

        public void StartAgents(Roles role, int count = 0)
        {
            ReportOneRoleStatistic state = agency.StatisticByRole(role);
            //Writeln($"Доступно {state.AvailableAgent} агентов роли {role}, из них - {state.WorkingAgent} работают");
            //StatisticAll();
            if (count == 0) Write($"Сколько агентов {role} запускать? ");
            while (count == 0)
            {
                Write(">");
                string line = Console.ReadLine();
                if (!Int32.TryParse(line, out count)) count = 0;
            }
            Writeln($"Запуск {count} агентов роли {role} с учетом уже имеющихся и работающих.");
            agency.StartAgentByRole(role, count);
            state = agency.StatisticByRole(Roles.InputStringParser);
            //StatisticAll();
        }

        public void StopAgents(Roles role, int count = 0)
        {
            ReportOneRoleStatistic state = agency.StatisticByRole(role);
            //Writeln($"Доступно {state.AvailableAgent} агентов роли {role}, из них - {state.WorkingAgent} работают");
            //StatisticAll();
            if (count == 0) Write("\nСколько агентов останавливать? ");
            while (count == 0)
            {
                Write(">");
                string line = Console.ReadLine();
                if (!Int32.TryParse(line, out count)) count = 0;
            }
            Writeln($"Останавливаем {count} агентов роли {role}.");
            agency.StopAgentByRole(role, count);
            state = agency.StatisticByRole(Roles.InputStringParser);
            //Writeln($"Доступно {state.AvailableAgent} агентов роли {role}, из них - {state.WorkingAgent} работают");
            //StatisticAll();
        }

        public void StatisticAll()
        {
            ReportOneRoleStatistic state;
            Writeln($"\nСтатистика по всем агентам:\n");
            int ms = TimeNowMs();
            foreach (var role in Enum.GetValues(typeof(Roles)).Cast<Roles>())
            {
                state = agency.StatisticByRole(role);
                Writeln($"{role,-18}: всего - {state.AvailableAgent,2}, работающих - {state.WorkingAgent,2}.");
            }
            ms = TimeNowMs() - ms;
            Writeln($"Время выполнения запроса: {ms} мс");

            /*
            Writeln($"\nLinq Статистика по всем агентам:\n");
            ms = TimeNowMs();
            foreach (var role in Enum.GetValues(typeof(Roles)).Cast<Roles>())
            {
                state = agency.StatisticByRole_LINQ(role);
                Writeln($"{role,-17}: всего - {state.AvailableAgent,2}, работающих - {state.WorkingAgent,2}.");
            }
            ms = TimeNowMs() - ms;
            Writeln($"Время выполнения запроса: {ms} мс");//*/
        }

        public void StatisticTcp()
        {
            int ms = TimeNowMs();
            ReportServerInfo info = agency.TcpServerInfo_LINQ();
            Writeln($"Статистика по tcp-агентам:\n");
            foreach (var role in info.RolesInfo)
            {
                //ReportOneRoleStatistic state = agency.StatisticByRole(role);
                Writeln($"{role.Role,-10}: всего - {role.AvailableAgent,2}, работающих - {role.WorkingAgent,2}.");
            }
            Writeln();
            foreach (var srv in info.TcpInfo)
            {
                string adr = srv.Address != null ? srv.Address : "null";
                string port = srv.Port != null ? srv.Port.ToString() : "null";
                Writeln($"{srv.Name,-18}: адрес {adr}:{port}");
            }
            ms = TimeNowMs() - ms;
            Writeln($"Время выполнения запроса: {ms} мс");
            
        }

    }

        //*/
    
}
