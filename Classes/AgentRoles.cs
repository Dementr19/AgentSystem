using System;
using System.Collections.Generic;
using System.Text;

using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Sockets;
using System.Net;

//пользовательские классы
using static AgentSystem.Classes.SystemTools;
using AgentSystem.DBClasses;
using System.Reflection;
using System.Linq;
//using AgentSystem.ReportCommandClasses;


namespace AgentSystem.Classes
{
    enum Roles { InputStringParser, ToDBWriter, DataTcpListener, CommandTcpListener }
    enum Startmode { Setup, Main, Finally }

    class AgentRoles
    {
        public Roles Name { get; set; }
        public Agent Agent;
        //очередь для сохранения обЪектов роли агента
        private Queue<object> SaveObject { get; set; } = new Queue<object>();

        public AgentRoles(Agent ref_agent, Roles role)
        {
            Agent = ref_agent;
            Name = new Roles();
            Name = role; 
        }
        
        public bool Run(Startmode stmode)
        {
            bool res = false;
            switch (Name)
            {
                case Roles.InputStringParser:
                    res = ParseInputString(stmode);
                    break;
                case Roles.ToDBWriter:
                    res = WriteToDB(stmode);
                    break;
                case Roles.DataTcpListener:
                    res = ListenDataTcp(stmode);
                    break;
                case Roles.CommandTcpListener:
                    //res = ListenCommandTcp(stmode);
                    res = ListenCommandHttp(stmode);
                    break;
                default:
                    Console.WriteLine("Такой работы ещё не придумали...");
                    break;
            }
            return res;
        }

        //parser --------------------------------------------------------------
        public bool ParseInputString(Startmode mode)
        {
            bool res = false;
            switch (mode)
            {
                case Startmode.Setup:
                    res = true;
                    break;
                case Startmode.Main:
                    DateString dstr = Agent.Agency.Context.InputStrings.GetItem() as DateString;
                    if (dstr != null)
                    {
                        int t = DeltaTimeMs(GetMsFromTime(dstr.Time)); //время (в мс) нахождения строки принятых данных в буфере (с учетом lock`а)
                        dstr.Line = $"{dstr.Line}-gets{t}"; //убрать отсюда, убрать из парсинга и ниже вместо "+ dbset.GetSTime)" написать "+ t)"

                        //WriteLog($"{RefAgent.Name}:  in->{dstr.Line}");
                        Match match = Regex.Match(dstr.Line, @"^[\d]{5}"); // 5 подряд идущих цифр с начала строки
                        if (!match.Success)
                            Writeln(dstr.Line);

                        StationData dbset = parsing_data_string(dstr);
                        if (dbset != null)
                        {
                            dbset.PutDbTime = DeltaTimeMs(dbset.TcpTime + dbset.GetSTime); //время (в мс) парсинга строки агентом 
                            Agent.Agency.Context.StationDataBuffer.PutItem(dbset);
                            //для сравнения
                            //string out_str = parsing_data_string_to_str(dstr.Line);
                            //string from_db_str = join_string_from_db(set, dstr.Line.Substring(0, 7));
                            //WriteLog($"{RefAgent.Name}: out->{out_str}\n{RefAgent.Name}: -db->{from_db_str}");
                        }
                        else WriteLog($"{Agent.Name}: out->!!! -Drop- {dstr.Line}");
                    }
                    else
                    {
                        //буфер опустел
                        //если режим не Infinity(бесконечный), агент завершает работу                        
                        if (Agent.Mode == AgentMode.NotEmptyBuffer)
                        {
                            WriteLog($"{Agent.Name}.{Agent.Role.Name} режим{Agent.Mode}: завершена работа по причине -= буфер пуст =-  ");
                            break;
                        }
                    }
                    res = true;
                    break;
                default:
                    res = true;
                    break;
            }
            return res;
        }
        private StationData parsing_data_string(DateString dateString)
        {
            string[][] id = Agent.Agency.Context.Fields;
            string line_src = dateString.Line;
            StationData res = new StationData { StationId = -1, NodeId = -1 }; //результирующий объект для вывода
 
            //номер станции
            Match match = Regex.Match(line_src, @"^[\d]{5}"); // 5 подряд идущих цифр с начала строки
            if (match.Success) res.StationId = Convert.ToInt32(match.Value);
            //номер узла станции
            Regex regex = new Regex(@"[\d]{5}"); // 5 подряд идущих цифр 
            match = regex.Match(line_src, 5, 5); // поиск по шаблону, начиная с 5-го символа строки, поиск в 5 символах
            if (match.Success) res.NodeId = Convert.ToInt32(match.Value);

            if (!(res.StationId < 0) && !(res.NodeId < 0))
            {
                //----- Датчики
                string pattern_decimal = @"-?\d+(\.\d+)?";
                res.Time = dateString.Time;
                //int i = 0;
                string cur_pattern;

                for (int i = 0; i < id[0].Length-2; i++)
                {
                    cur_pattern = $"{id[0][i]}{pattern_decimal}";
                    PropertyInfo info = res.GetType().GetProperty($"Fid{i:d2}");
                    if (id[1][i] != "float")
                    {
                        int? val = (int?)find_sensor(dateString.Line, cur_pattern);
                        info.SetValue(res, val);
                    }
                    else
                    {
                        float? val = (float?)find_sensor(dateString.Line, cur_pattern);
                        info.SetValue(res, val);
                    }
                }
                //10 ---  время приема пакета tcp-сервером (в мс) -----
                res.TcpTime = GetMsFromTime(dateString.Time);
                //11 ---  время выдачи строки из буфера  (в мс) ----
                cur_pattern = $"{id[0][11]}{pattern_decimal}";
                res.GetSTime = (int)find_sensor(dateString.Line, cur_pattern);
            }
            else res = null;
            return res;
        }
        private StationData ____parsing_data_string_old(DateString dateString)
        {
            string[] id = { "idto",
                            "crat", // 1-f
                            "icrh", // 2-f
                            "crdp", // 3-f
                            "crpa",
                            "crwd",
                            "crws",
                            "crcl",
                            "crhc",
                            "crhv",
                            "tcp",
                            "gets"
            };            // float и int - @"icrh-?\d+(\.\d+)?"

            //string str_num = dateString.Line.Substring(0, 7);
            string line_src = dateString.Line;      //.Remove(0, 7);

            StationData res = new StationData { StationId = -1, NodeId = -1 }; //результирующий объект для вывода

            //номер станции
            Match match = Regex.Match(line_src, @"^[\d]{5}"); // 5 подряд идущих цифр с начала строки
            if (match.Success) res.StationId = Convert.ToInt32(match.Value);

            //номер узла станции
            Regex regex = new Regex(@"[\d]{5}"); // 5 подряд идущих цифр 
            match = regex.Match(line_src, 5, 5); // поиск по шаблону, начиная с 5-го символа строки, поиск в 5 символах
            if (match.Success) res.NodeId = Convert.ToInt32(match.Value);

            if (!(res.StationId < 0) && !(res.NodeId < 0))
            {
                //----- Датчики
                string pattern_decimal = @"-?\d+(\.\d+)?";
                int i = 0;
                res.Time = dateString.Time;
                //0 -------- idto ----------
                string cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid00 = (int?)find_sensor(dateString.Line, cur_pattern); //Idto
                i++;
                //1 -------- crat - float --------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid01 = find_sensor(dateString.Line, cur_pattern); //Crat
                i++;
                //2 -------- icrh ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid02 = (float?)find_sensor(dateString.Line, cur_pattern); //Icrh
                i++;
                //3 -------- crdp - float --------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid03 = find_sensor(dateString.Line, cur_pattern); //Crdp 
                i++;
                //4 -------- crpa ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid04 = (int?)find_sensor(dateString.Line, cur_pattern); //Crpa
                i++;
                //5 -------- crwd ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid05 = (int?)find_sensor(dateString.Line, cur_pattern); //Crwd
                i++;
                //6 -------- crws ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid06 = (int?)find_sensor(dateString.Line, cur_pattern); //Crws
                i++;
                //7 -------- crcl ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid07 = (int?)find_sensor(dateString.Line, cur_pattern); //Crcl
                i++;
                //8 -------- crhc ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid08 = (int?)find_sensor(dateString.Line, cur_pattern); //Crhc
                i++;
                //9 -------- crhv ----------
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.Fid09 = (int?)find_sensor(dateString.Line, cur_pattern); //Crhv
                i++;
                //10 ---  время приема пакета tcp-сервером  -----
                //cur_pattern = $"{id[i]}{pattern_decimal}";
                //res.TcpTime = (int)find_sensor(dateString.Line, cur_pattern);
                res.TcpTime = GetMsFromTime(dateString.Time);
                i++;
                //11 ---  время выдачи строки из буфера  ----
                cur_pattern = $"{id[i]}{pattern_decimal}";
                res.GetSTime = (int)find_sensor(dateString.Line, cur_pattern);
            }
            else res = null;
            return res;
        }
        private float? find_sensor(string str, string pattern)
        {
            float? res = null;
            Match match = Regex.Match(str, pattern);
            if (match.Success)
            {
                // для русской локализации заменяем точки на запятые. Правильно использовать перегрузку 
                // TryParse с параметром NumberInfo
                //string snum = matches[0].ToString().Substring(4).Replace('.',','); 
                string snum = match.Value.ToString().Substring(4).Replace('.', ',');
                if (float.TryParse(snum, out float number))
                {
                    res = number;
                }
            }
            return res;
        }
        private string __parsing_data_string_to_str(string line)
        {
            string[] id = { "idto",
                            "crat", // 1-f
                            "icrh",
                            "crdp", // 3-f
                            "crpa",
                            "crwd",
                            "crws",
                            "crcl",
                            "crhc",
                            "crhv"
            };

            // float и int - @"icrh-?\d+(\.\d+)?"

            string str_num = line.Substring(0, 7);
            string line_src = line.Remove(0, 7);
            string line_res = ""; //результирующая строка для вывода

            //номер станции
            string str_res = "~~~~~"; // значение параметра для записи в строку в случае неверного его значения из входа,
                                      // т.е визуальный образ значения null
            Match match = Regex.Match(line_src, @"^[\d]{5}"); // 5 подряд идущих цифр с начала строки
            if (match.Success)
                str_res = match.Value;
            line_res += str_res;

            //номер узла станции
            str_res = " ~~~~~";
            Regex regex = new Regex(@"[\d]{5}"); // 5 подряд идущих цифр 
            match = regex.Match(line_src, 5, 5); // поиск по шаблону, начиная с 5-го символа строки, поиск в 5 символах
            if (match.Success)
                str_res = $" {match.Value}";
            line_res += str_res;

            //Датчики
            for (int i = 0; i < 10; i++)
            {
                str_res = "~~~";
                line_res += $" {id[i]}:";
                //MatchCollection matches = Regex.Matches(line, id[i] + tboxFloat.Text); //  @"-?\d+(\.\d+)?"
                match = Regex.Match(line_src, id[i] + @"-?\d+(\.\d+)?"); //  @"-?\d+(\.\d+)?"
                if (match.Success)
                {
                    // для русской локализации заменяем точки на запятые. Правильно использовать перегрузку 
                    // TryParse с параметром NumberInfo
                    //string snum = matches[0].ToString().Substring(4).Replace('.',','); 
                    string snum = match.Value.ToString().Substring(4).Replace('.', ',');
                    if (decimal.TryParse(snum, out decimal number))
                    {
                        str_res = number.ToString();
                    }
                }
                line_res += str_res;
            }
            return str_num + line_res;
        }
        private string __join_string_from_db(StationData dbset, string str_num)
        {
            string[] id = { "idto",
                            "crat", // 1-f
                            "icrh",
                            "crdp", // 3-f
                            "crpa",
                            "crwd",
                            "crws",
                            "crcl",
                            "crhc",
                            "crhv"
            };


            string line_res = ""; //результирующая строка для вывода

            //номер станции
            line_res += $"{dbset.StationId:d5}";

            //номер узла станции
            line_res += $" {dbset.NodeId:d5}";

            //Датчики
            string str_null = "~~~";
            string str_res;
            int i = 0;
            //0 -------- idto ----------
            if (dbset.Fid00.HasValue) str_res = $"{dbset.Fid00}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //1 -------- crat - float --------
            if (dbset.Fid01.HasValue) str_res = $"{dbset.Fid01}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //2 -------- icrh ----------
            if (dbset.Fid02.HasValue) str_res = $"{dbset.Fid02}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //3 -------- crdp - float --------
            if (dbset.Fid03.HasValue) str_res = $"{dbset.Fid03}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //4 -------- crpa ----------
            if (dbset.Fid04.HasValue) str_res = $"{dbset.Fid04}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //5 -------- crwd ----------
            if (dbset.Fid05.HasValue) str_res = $"{dbset.Fid05}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //6 -------- crws ----------
            if (dbset.Fid06.HasValue) str_res = $"{dbset.Fid06}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //7 -------- crcl ----------
            if (dbset.Fid07.HasValue) str_res = $"{dbset.Fid07}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //8 -------- crhc ----------
            if (dbset.Fid08.HasValue) str_res = $"{dbset.Fid08}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //9 -------- crhv ----------
            if (dbset.Fid09.HasValue) str_res = $"{dbset.Fid09}";
            else str_res = str_null;
            line_res += $" {id[i++]}:{str_res}";
            //Date
            line_res += $" - {dbset.Time}";
            return str_num + line_res;
        }
        //end parser --------------------------------------------------------------
        //writer --------------------------------------------------------------
        public bool WriteToDB(Startmode mode)
        {
            bool res = false;
            switch (mode)
            {
                case Startmode.Setup:
                    res = true;
                    break;
                case Startmode.Main:
                    var context = Agent.Agency.Context;
                    StationData rec = Agent.Agency.Context.StationDataBuffer.GetItem() as StationData;
                    if (rec != null)
                    {
                        rec.GetDbTime = DeltaTimeMs(rec.TcpTime + rec.GetSTime + rec.PutDbTime);  //время (в мс) нахождения записи в буфере данных
                        rec.DeltaWriter = DeltaTimeMs(rec.TcpTime + rec.GetSTime + rec.PutDbTime + rec.GetDbTime); //время (в мс) между извлечением из буфера 
                                                                                                                   //(после lock`а) и записью в БД
                        rec.DbTime = DeltaTimeMs(rec.TcpTime);
                        using (AppDbContext db = new AppDbContext(context.OptionsDb, context.Fields[0]))
                        {
                            db.Add(rec);
                            
                            db.SaveChanges();
                        }
                    }
                    res = true;
                    break;
                default:
                    res = true;
                    break;
            }
            return res;
        }

        //tcp-сервер Data ------------------------------------------------------
        public bool ListenDataTcp(Startmode mode)
        {
            bool res = false;
            switch (mode) 
            {
                case Startmode.Setup:
                    TcpListener server = null;
                    TcpAddress address = Agent.Agency.Context.GetTcpAddress(Agent);
                    if (address != null)
                    {
                        try
                        {
                            server = new TcpListener(IPAddress.Parse(address.Address), address.Port);
                            // запуск слушателя
                            server.Start();
                            server.BeginAcceptTcpClient(receiveStationData, server);
                            var ipen = server.LocalEndpoint as IPEndPoint;
                            WriteLog($"{Agent.Name}: агент приступил к работе по адресу {ipen.Address}:{ipen.Port}");
                            res = true;
                        }
                        catch (Exception e)
                        {
                            WriteLog($"{Agent.Name}: агент не запустился с сообщением: {e.Message}");
                        }
                        //if (server != null) 
                        SaveObject.Enqueue(server);
                        SaveObject.Enqueue(address);
                    }
                    else    //адрес получить не удалость
                    {
                        WriteLog($"{Agent.Name}: не удалось получить tcp-адрес, завершаю работу.");
                    }
                    break;
                case Startmode.Main:
                    res = true;
                    break;
                case Startmode.Finally:
                    server = SaveObject.Dequeue() as TcpListener;
                    address = SaveObject.Dequeue() as TcpAddress;
                    if (server != null)
                    {
                        server.Stop();
                        Agent.Agency.Context.FreeTcpAddress(address);
                    }
                    //очистка очереди
                    SaveObject.Clear();
                    break;
                default:
                    res = true;
                    break;
            }            
            return res;
        }
        private void receiveStationData(IAsyncResult tcpList)
        {
            var tcp_listener = tcpList.AsyncState as TcpListener; //аналог  = (TcpListener) tcpList.AsyncState;
            if (tcp_listener != null)
            {
                try
                {
                    // пробуем получить текущего клиента и что то с ним делаем
                    var client = tcp_listener.EndAcceptTcpClient(tcpList);

                    int time_ms = DateTime.Now.Millisecond;

                    // получаем сетевой поток для чтения и записи
                    NetworkStream stream = client.GetStream();

                    byte[] data = new byte[256];
                    StringBuilder response = new StringBuilder();
                    do
                    {
                        int bytes = stream.Read(data, 0, data.Length);
                        response.Append(Encoding.UTF8.GetString(data, 0, bytes));
                    }
                    while (stream.DataAvailable); // пока данные есть в потоке
                                                  // закрываем поток
                    stream.Close();
                    //!!!!! м.б. его и не надо закрывать - уточнить!!!!! закрываем подключение
                    //client.Close();

                    //Полученную строку данных снабжаем текущим временем сервера
                    DateString dstr = new DateString();
                    //dstr.Time = new DateTime();
                    dstr.Time = DateTime.Now;
                    //str.Line = $"{++str_cnt:d5}: {str.Line}";
                    dstr.Line = response.ToString();     //+$"-tcp{dstr.Time}");

                    Agent.Agency.Context.InputStrings.PutItem(dstr);  

                    //Writeln($"{response.ToString()}");
                    //Writeln(string.Format("Accept {0}", client));

                    //принимаем следующего клиента
                    tcp_listener.BeginAcceptTcpClient(receiveStationData, tcp_listener);
                }
                catch (Exception e)
                {
                    WriteLog($"{Agent.Name}.{Agent.Role}: Оборван сеанс связи со шлюзом с сообщением: {e.Message}") ;
                }
            }
        }
        //end tcp-сервер Data------------------------------------------------------
        
        //tcp-сервер Command --------------------------------------------------
        public bool __ListenCommandTcp(Startmode mode)
        {
            bool res = false;
            switch (mode)
            {
                case Startmode.Setup:
                    TcpListener server = null;
                    TcpAddress address = Agent.Agency.Context.GetTcpAddress(Agent);
                    if (address != null)
                    {
                        try
                        {
                            server = new TcpListener(IPAddress.Parse(address.Address), address.Port);
                            // запуск слушателя
                            server.Start();
                            server.BeginAcceptTcpClient(receiveCommand, server);
                            var ipen = server.LocalEndpoint as IPEndPoint;
                            WriteLog($"{Agent.Name}: агент приступил к работе по адресу {ipen.Address}:{ipen.Port}");
                            res = true;
                        }
                        catch (Exception e)
                        {
                            WriteLog($"{Agent.Name}: агент не запустился с сообщением: {e.Message}");
                        }
                        //if (server != null) 
                        SaveObject.Enqueue(server);
                        SaveObject.Enqueue(address);
                    }
                    else    //адрес получить не удалость
                    {
                        WriteLog($"{Agent.Name}: не удалось получить tcp-адрес, работа завершена.");
                    }
                    break;
                case Startmode.Main:
                    break;
                case Startmode.Finally:
                    server = SaveObject.Dequeue() as TcpListener;
                    address = SaveObject.Dequeue() as TcpAddress;
                    if (server != null)
                    {
                        server.Stop();
                        Agent.Agency.Context.FreeTcpAddress(address); //освобождаем tcp-адрес
                    }
                    //очистка очереди
                    SaveObject.Clear();
                    break;
                default:
                    res = true;
                    break;
            }
            return res;
        }
        private void receiveCommand(IAsyncResult tcpList)
        {
            var tcp_listener = tcpList.AsyncState as TcpListener; //аналог  = (TcpListener) tcpList.AsyncState;
            if (tcp_listener != null)
            {
                try
                {
                    // пробуем получить текущего клиента и что то с ним делаем
                    var client = tcp_listener.EndAcceptTcpClient(tcpList);
                    //int time_ms = DateTime.Now.Millisecond;
                    Thread thread = new Thread(new ParameterizedThreadStart(client_session));
                    thread.Start(client);
                    //принимаем следующего клиента
                    tcp_listener.BeginAcceptTcpClient(receiveCommand, tcp_listener);
                }
                catch (Exception e)
                {
                    Writeln(e.Message);
                }
            }
        }
        private void client_session(object tcp_client)
        {
            TcpClient client = tcp_client as TcpClient;
            // получаем сетевой поток для чтения и записи
            NetworkStream stream = client.GetStream();

            byte[] data = new byte[256];
            StringBuilder response = new StringBuilder();
            do
            {
                int bytes = stream.Read(data, 0, data.Length);
                response.Append(Encoding.UTF8.GetString(data, 0, bytes));
            }
            while (stream.DataAvailable); // читаем пока данные есть в потоке

            //Вызывается какое-либо действие над полученной строкой
            //получение строки из GET-запроса
            //Match match = Regex.Match(response.ToString(), @"GET /[\S]*");// HTTP/1.1"); // 1-я строка
          Match match = Regex.Match(response.ToString(), @"/[a-z_]+[\S]*"); // 1-я строка
            

            string str = "";
            if (match.Success)
                str = match.Value.ToString().Substring(1);//.Substring(5);
            //Writeln($"\n-={str}=-");
            //разбираем и выполняем команду
            str = parsing_command(str, Privileges.ADMIN);
            //Writeln(str);
            //Отправляем ответ клиенту
            stream.Write(Encoding.UTF8.GetBytes(str));
            //завершаем сеанс
            stream.Close();
            client.Close();
        }
        private string parsing_command(string str, Privileges priv)
        {
            string[] func = str.Split('?');
            string[] param = null;
            if (func.Length > 1)  param = func[1].Split('&');
            //удаление параметра "nonce"
            param = param.Where(x => !x.Contains("nonce")).ToArray();


            //Writeln($"{func[0]}::{param}");
            //Write($"{func[0]}::");
            //if (param != null) foreach (string prm in param) Write($" {prm}");
            //Writeln();
            CmdInterpretator cmd = new CmdInterpretator(Agent.Agency);
            return cmd.Run(func[0],param, priv);
        }
        //end tcp-сервер Command---------------------------------------------------
        //Web-сервер Command -----------------------------------------------------
        public bool ListenCommandHttp(Startmode mode)
        {
            bool res = false;
            var context = Agent.Agency.Context;
            switch (mode)
            {
                case Startmode.Setup:
                    WebServer server = null;
                    TcpAddress addr = context.GetTcpAddress(Agent);
                    if (addr != null)
                    {
                        try
                        {
                            //server = new WebServer(new List<string> { "http://192.168.10.100:33330/" }, WriteLog);
                            server = new WebServer(new List<string> { $"http://{addr.Address}:{addr.Port}/" }, WriteLog);
                            //настройка интерпретатора
                            server.SetInterpretator(parsing_command);
                            //получение данных о зарегистрированных пользователях
                            server.AddUsersFromConfig(context.AppSettings);
                            //настройка ответа на GET-запрос
                            var fpath = context.AppSettings["get_method_file"];
                            server.SetResponseToGetRequest(fpath);
                            // запуск слушателя
                            server.Start();
                            res = true;
                        }
                        catch (Exception e)
                        {
                            WriteLog($"{Agent.Name}: При настройке окружения агента произошла ошибка: {e.Message}");
                        }
                        if (server != null)
                        {
                            SaveObject.Enqueue(server);
                            SaveObject.Enqueue(addr);
                        }
                    }
                    else    //адрес получить не удалость
                    {
                        WriteLog($"{Agent.Name}: не удалось получить tcp-адрес, завершаю работу.");
                    }
                    break;
                case Startmode.Main:
                    break;
                case Startmode.Finally:
                    server = SaveObject.Dequeue() as WebServer;
                    addr = SaveObject.Dequeue() as TcpAddress;
                    if (server != null)
                    {
                        server.Stop();
                        context.FreeTcpAddress(addr); //освобождаем tcp-адрес
                    }
                    //очистка очереди
                    SaveObject.Clear();
                    break;
                default:
                    res = true;
                    break;
            }
            return res;
        }

        private string parsing_command_stub(string str)
        {
            return $"Обработан запрос: {str}";
        }


        //end Web-сервер Command1 -------------------------------------------------

    }
}

