using System;
using System.Collections.Generic;
using System.Text;

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using AgentSystem.DBClasses;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;

namespace AgentSystem.Classes
{
    class TcpAddress
    {
        public string Address { get; set; }
        public int Port { get; set; }
        //ссылка на агента TcpListener, слушающего данный адрес, иначе null 
        public Agent Agent { get; set; } 
    }
    //класс объекта для хранения контекста работы системы
    class Context
    {
        //ссылка на буфер принятых строк
        public DataBuffer InputStrings { get; set; }
        //ссылка на буфер данных для записи в БД
        public DataBuffer StationDataBuffer { get; set; }
        //список значений {address:port:agent} для настройки агента TcpListener
        public List<TcpAddress> ListDataTcpAddress { get; set; }
        //адрес для получения команд
        public List<TcpAddress> ListCommandTcpAddress { get; set; }
        //объект конфигурации для настройки контекста
        public IConfiguration AppSettings;
        //название и тип полей строки с показаниями датчиков и полей БД
        public string[][] Fields { get; } = new string[2][];
        //строка подключения к БД
        //для передачи данных имя пользователя будет server
        //для запроса клиента надо будет подставлять имя пользователя
        public DbContextOptions OptionsDb;
        //количество агентов, запускаемых по умолчанию
        public (int Data_receiver, int Parser, int Writer, int Commander) DefaultAgentCount = (0, 0, 0, 0);

        

        internal DataBuffer DataBuffer
        {
            get => default;
            set
            {
            }
        }
        internal TcpAddress TcpAddress
        {
            get => default;
            set
            {
            }
        }
        internal DateString DateString
        {
            get => default;
            set
            {
            }
        }

        public DBClasses.StationData StationData
        {
            get => default;
            set
            {
            }
        }

        //для блокировки при доступе к списку tcp-адресов для приема данных ListTcpAddress
        object list_locker = new object();

        public TcpAddress GetTcpAddress(Agent agent)
        {
            TcpAddress address = null;
            if (agent.Role.Name == Roles.DataTcpListener)
            {
                lock (list_locker)
                {
                    foreach (var adr in ListDataTcpAddress)
                    {
                        if (adr.Agent == null)      // если адрес не занят 
                        {
                            address = adr;
                            address.Agent = agent;
                            break;
                        }
                    }
                }
            }
            else
            {
                lock (list_locker)
                {
                    foreach (var adr in ListCommandTcpAddress)
                    {
                        if (adr.Agent == null)      // если адрес не занят 
                        {
                            address = adr;
                            address.Agent = agent;
                            break;
                        }
                    }
                }
            }
            return address;
        }

        public void FreeTcpAddress(TcpAddress adr)
        {
            lock (list_locker)
            {
                adr.Agent = null;
            }
        }
        public void AddDataTcpAddress(string str)
        {
            string[] lines = str.Split(':');
            if (IPAddress.TryParse(lines[0], out IPAddress ip_adr) && UInt32.TryParse(lines[1], out uint port))
            {
                lock (list_locker)
                {
                    ListDataTcpAddress.Add(new TcpAddress { Address = lines[0], Port = (int)port, Agent = null });
                }
            }
        }
        public void AddCommandTcpAddress(string str)
        {
            string[] lines = str.Split(':');
            if (IPAddress.TryParse(lines[0], out IPAddress ip_adr) && UInt32.TryParse(lines[1], out uint port))
            {
                lock (list_locker)
                {
                    ListCommandTcpAddress.Add(new TcpAddress { Address = lines[0], Port = (int)port, Agent = null });
                }
            }
        }

        public (bool, bool, string) Load(string[] args)
        {
            bool confCorrect = true;
            bool tstMode = false;
            string confMsg = "";
            string error = "Ошибка конфигурации: ";
            string warning = "Предупреждение: ";
            string header = $"Загрузка конфигурации сервера\n";
            //формирование конфигурации
            var builder = new ConfigurationBuilder()
                .AddJsonFile("config/agentconfig.json")
                .AddJsonFile("config/clients.json")
                .AddCommandLine(args);
            try
            {
                AppSettings = builder.Build();
                //чтение данных в контекст из конфигурации
                ListDataTcpAddress = new List<TcpAddress>();
                ListCommandTcpAddress = new List<TcpAddress>();
                //Чтение из конфигурации tcp-адресов и портов
                var list_data = AppSettings.GetSection("data_tcp").GetChildren();
                if (list_data == null)
                    throw new Exception("Отсутствуют настройки ip-адреса для приёма данных (секция data_tcp)");
                foreach (var section in list_data)
                {
                    var tcp = section.Value;
                    if (IsTcpAddrValid(tcp)) AddDataTcpAddress(tcp);
                }
                var list_cmd = AppSettings.GetSection("cmd_tcp").GetChildren();
                if (list_cmd == null)
                    throw new Exception("Отсутствуют настройки ip-адреса для приёма запросов клиентов (секция cmd_tcp)");
                foreach (var section in list_cmd)
                {
                    var tcp = section.Value;
                    if (IsTcpAddrValid(tcp)) AddCommandTcpAddress(tcp);
                }
                //настройка названий и типов датчиков, а также названий полей БД
                var fields = AppSettings.GetSection("fields").GetChildren();
                if (fields == null)
                    throw new Exception("Отсутствуют определения полей входной строки данных и таблицы БД (секция 'fields')");
                string fields_count = AppSettings["fields:count"];
                if (int.TryParse(fields_count, out int n))
                {
                    if (n > 10) throw new Exception("Количество полей базы данных не может быть больше 10 (секция 'fields')");
                    if (n < 1) throw new Exception("Количество полей базы данных не может быть меньше 1 (секция 'fields')");
                    string[] id = new string[n + 2];
                    string[] tp = new string[n + 2];
                    for (int i = 0; i < n; i++)
                    {
                        id[i] = AppSettings[$"fields:fid_{i:d2}:name"];
                        tp[i] = AppSettings[$"fields:fid_{i:d2}:type"];
                    }
                    id[n] = "tcp";    // для тестирования временнЫх интервалов
                    id[n + 1] = "gets";   // аналогично
                    Fields[0] = id;
                    Fields[1] = tp;
                    //Настройка запуска агентов по умолчанию
                    var default_agents = AppSettings.GetSection("running_default_agents").GetChildren();
                    if (default_agents == null)
                        confMsg += $"{warning}Отсутствуют параметры запуска модулей сервера (секция 'running_default_agents')\n";
                    string receiver = AppSettings["running_default_agents:data_receiver"];
                    if (!int.TryParse(receiver, out DefaultAgentCount.Data_receiver))
                        confMsg += $"{warning}Параметры запуска модуля приёма данных заданы некорректно\n";
                    string parser = AppSettings["running_default_agents:parser"];
                    if (!int.TryParse(parser, out DefaultAgentCount.Parser))
                        confMsg += $"{warning}Параметры запуска модуля обработки данных заданы некорректно\n";
                    string writer = AppSettings["running_default_agents:writer"];
                    if (!int.TryParse(writer, out DefaultAgentCount.Writer))
                        confMsg += $"{warning}Параметры запуска модуля записи данных в БД заданы некорректно\n";
                    string commander = AppSettings["running_default_agents:commander"];
                    if (!int.TryParse(commander, out DefaultAgentCount.Commander))
                        confMsg += $"{warning}Параметры запуска модуля обработки запросов клиентов заданы некорректно\n";
                    // Настройка опций подключения к БД 
                    // Настройка подключения к базе данных
                    string useDb = AppSettings["ConnectionStrings:DefaultConnection"];
                    string connectionString = AppSettings[$"ConnectionStrings:{useDb}"];
                    if (connectionString == null || connectionString == "") 
                        throw new Exception("Отсутствуют параметры подключения к БД для записи данных");
                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    if (useDb == "localdb")
                    {
                        OptionsDb = optionsBuilder
                            .UseSqlServer(connectionString)
                            .Options;
                    }
                    else
                    {
                        //БД: имя и пароль сервера, от которого пишутся данные в БД
                        string user = AppSettings["server:name"];
                        string psw = AppSettings["server:dbpassword"];
                        connectionString = connectionString.Replace("{user}", user)
                            .Replace("{password}", psw);
                        OptionsDb = optionsBuilder
                            .UseNpgsql(connectionString)
                            .Options;
                    }
                    //попытка подключиться к БД для проверки строки подключения
                    try
                    {
                        using (var conn = new NpgsqlConnection(connectionString))
                        {
                            conn.Open();
                            using (var cmd = new NpgsqlCommand("SELECT 1", conn))
                            {
                                var reader = cmd.ExecuteReader();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Невозможно подключиться к БД со строкой подключения: {connectionString}");
                    }
                }
            }
            catch (Exception e)
            {
                SystemTools.WriteLog($"{confMsg}\n{error}{e.Message}");
                return (false, false, $"{confMsg}\n{error}{e.Message}");
            }
            foreach (var prm in args)
                if (prm.Contains("test"))
                    tstMode = true;

            if (confMsg == "") confMsg = $"{header}Конфигурация загружена успешно\n";
            return (confCorrect, tstMode, confMsg);
        }
        private bool IsTcpAddrValid(string addr)
        {
            bool res = false;
            string[] strs = addr.Split(':');
            if (strs.Length == 2)
            {
                if (IPAddress.TryParse(strs[0],out IPAddress ip))
                {
                    if (int.TryParse(strs[1], out int port))
                        res = true;
                }
            }
            return res;
        }
    }
}
