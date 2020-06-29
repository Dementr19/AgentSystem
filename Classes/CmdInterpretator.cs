using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Linq.Expressions;

//пользовательские классы
using static AgentSystem.Classes.SystemTools;
using AgentSystem.ReportCommandClasses;
using AgentSystem.DBClasses;


namespace AgentSystem.Classes
{
    class FilterItem
    {
        public string From { get; set; }
        public string To { get; set; }
    }

    class DBType
    {
        public string Type { get; set; }
        public string MinValue { get; set; }
        public string MaxValue { get; set; }
        public DBType(string type_name, string min, string max)
        {
            Type = type_name;
            MinValue = min;
            MaxValue = max;
        }
    }

    public delegate string CmdFunction(string[] param); 
    enum Privileges { NO_PRIVILEGES, SERVER, ADMIN, USER }
    class Command
    {
        public string Name { get; }
        public Privileges Privilege { get; }
        public CmdFunction Action { get; }
        public Command(string cmd_name, Privileges priv, CmdFunction action)
        {
            Name = cmd_name;
            Privilege = priv;
            Action = action;
        }
    }

    class CmdInterpretator
    {
        //public delegate string Function(string[] param);
        public Dictionary<string, Command> Commands { get; set; }
        public Agency Agency { get; set; }

        internal FilterItem FilterItem
        {
            get => default;
            set
            {
            }
        }

        internal DBType DBType
        {
            get => default;
            set
            {
            }
        }

        public CmdInterpretator(Agency agency)
        {
            Agency = agency;
            Command ServerInfoCmd = new Command("serverinfo", Privileges.ADMIN, ServerInfoAction);
            Command SelectDataCmd = new Command("selectdata", Privileges.USER, SelectDataAction);
            Command AgentInfoCmd = new Command("agentinfo", Privileges.ADMIN, AgentInfoAction);


            Commands = new Dictionary<string, Command>
            {
                 { ServerInfoCmd.Name, ServerInfoCmd }
                ,{ SelectDataCmd.Name, SelectDataCmd }
                ,{ AgentInfoCmd.Name, AgentInfoCmd }
            };
        }


        public string Run(string name, string[] args, Privileges priv)
        {
            int ms = TimeNowMs();
            ReportCommandError error = new ReportCommandError
            {
                Result = false,
                Error = $"Error: '{name}' not found"
            };
            error.TimeMs = TimeNowMs() - ms;
            string res = error.ToJSON();          //$"Error: {name} not found";

            foreach(var cmd in Commands)
            {
                if (name == cmd.Key)
                {
                    if (!(priv > cmd.Value.Privilege))
                    {
                        res = cmd.Value.Action(args);
                    }
                    else
                    {
                        error.Error = $"Error: Недостаточно прав для выполнения команды '{name}'";
                        error.TimeMs = TimeNowMs() - ms;
                        res = error.ToJSON();
                    }
                    break;
                }
            }
            return res;
        } 

        private string AgentInfoAction(string[] param)
        {
            ReportRoleStatistic rep_roles = new ReportRoleStatistic();
            int ms = TimeNowMs();
            foreach (var role in Enum.GetValues(typeof(Roles)).Cast<Roles>())
            {
                ReportOneRoleStatistic stat_role = Agency.StatisticByRole(role);
                stat_role.Role = role.ToString();
                rep_roles.RolesInfo.Add(stat_role);
            }
            rep_roles.TimeMs = TimeNowMs() - ms;

            string res = rep_roles.ToJSON();
            return res;
        }

        private string SelectDataAction(string[] param)
        {
            int ms = TimeNowMs();

            DBType db_int = new DBType("int", $"{int.MinValue}", $"{int.MaxValue}");
            DBType db_date = new DBType ("date", $"{DateTime.MinValue}", $"{DateTime.MaxValue}");
            DBType db_float = new DBType ("float", $"{float.MinValue}", $"{float.MaxValue}");

            Dictionary<string, DBType> prms_base = new Dictionary<string, DBType> {
                { "station", db_int },
                { "node", db_int },
                { "time", db_date }
            };
            Dictionary<string, FilterItem> Filter = new Dictionary<string, FilterItem>();
            string res;
            ReportCommandError error = new ReportCommandError
            {
                Result = false,
                Error = $"Error: Arguments not found" //неправильный вызов команды
            };
            ReportDBRequest rep_db = new ReportDBRequest();

            void addValue(string base_world, string arg, string value)
            {
                if (Filter.ContainsKey(base_world))               //если фильтр-запись по базовому слову уже есть, дописываем значение в неё
                    if (arg.StartsWith("from_"))
                    {
                        Filter[base_world].From = value;
                    }
                    else
                    {
                        Filter[base_world].To = value;
                    }
                else
               {                                               
                    FilterItem fi = new FilterItem();           //иначе: создаем новую фильтр-запись по базовому слову
                    if (arg.StartsWith("from_"))
                    {
                        fi.From = value;
                        fi.To = prms_base[base_world].MaxValue;
                    }
                    else
                    {
                        fi.From = prms_base[base_world].MinValue;
                        fi.To = value;
                    }
                    Filter.Add(base_world, fi);                   //и добавляем её в фильтр
                }
            }
            //проверка параметров на корректность
            if (!(param.Length < 1))
            {
                bool isArgumentsValid = true; //результат проверки входных параметров (аргументов)
                //проверяем КАЖДЫЙ параметр на корректность
                foreach (var prm in param)
                {
                    string[] strs = prm.Split('=');
                    if (strs.Length == 2)                                                             //если есть и имя параметра, и его значение
                        if ((strs[0] != null && strs[0] != "") && (strs[1] != null && strs[1] != "")) //и они не пусты
                        {
                            string arg_name = strs[0];
                            string arg_value = strs[1].Replace('+', ' ');
                            string base_world = arg_name.Replace("from_", "").Replace("to_", "");   //определяем базовое слово параметра
                            if (prms_base.ContainsKey(base_world))                                  //и проверяем его наличие в словаре
                            {                                                                       //Если слово в словаре есть, то:
                                string type = prms_base[base_world].Type;                           //выясняем его тип
                                bool isValueValid = false;
                                switch (type)                                                       //проверяем корректность значения
                                {
                                    case "int":
                                        isValueValid = int.TryParse(arg_value, out _);
                                        break;
                                    case "date":
                                        isValueValid = DateTime.TryParse(arg_value, out _);
                                        break;
                                    case "float":
                                        isValueValid = float.TryParse(arg_value, out _);
                                        break;
                                }
                                if (isValueValid) addValue(base_world, arg_name, arg_value);
                                else
                                {
                                    error.Error = $"The value '{arg_value}' of the argument '{arg_name}' is incorrect.";
                                    isArgumentsValid = false;
                                }
                            }
                            else
                            {
                                error.Error = $"No such argument '{arg_name}'";
                                isArgumentsValid = false;

                            }
                        } else
                        {
                            error.Error = $"Wrong parameter '{strs[0]}={strs[1]}'";
                            isArgumentsValid = false;
                        }
                    if (!isArgumentsValid) break;  //если проверка параметра завершилась неудачей выходим из цикла и возвращаем ошибку
                }
                
                if (isArgumentsValid)
                {
                    //строим linq запрос с использованием полученного фильтра
                    using (AppDbContext db = new AppDbContext(Agency.Context.OptionsDb, Agency.Context.Fields[0]))
                    {
                        //IQueryable<StationData> q = db.ListStationData;

                        var q = from row in db.ListStationData select row;

                        if (Filter.ContainsKey("station"))
                            q = q.Where(row => row.StationId >= int.Parse(Filter["station"].From) && row.StationId <= int.Parse(Filter["station"].To));
                        if (Filter.ContainsKey("node"))
                            q = q.Where(row => row.NodeId >= int.Parse(Filter["node"].From) && row.NodeId <= int.Parse(Filter["node"].To));
                        if (Filter.ContainsKey("time"))
                            q = q.Where(row => row.Time >= DateTime.Parse(Filter["time"].From) && row.Time <= DateTime.Parse(Filter["time"].To));
                        
                        rep_db.Records.AddRange(q);
                    }
                    //настройка успешного выполнения команды
                    error.Result = true;
                }
            }
            if (error.Result)
            {
                rep_db.TimeMs = TimeNowMs() - ms;
                res = rep_db.ToJSON();
            }
            else
            {
                error.TimeMs = TimeNowMs() - ms;
                res = error.ToJSON();
            }
            return res;

            /* это проверка аргументов для обычной команды
            try { args.Add(strs[0], strs[1].Replace('+', ' ')); }
            catch
            {

                error.Error = $"{strs[0]}: Invalid argument duplication";
                res_last_operation = false;
                break;
            }//*/
        }

        public string ServerInfoAction(string[] param)
        {
            int ms = TimeNowMs();
            ReportServerInfo rsi = Agency.TcpServerInfo_LINQ();
            rsi.TimeMs = TimeNowMs() - ms;
            string res = rsi.ToJSON();
            return res;
        }


        //*/
    }
}
