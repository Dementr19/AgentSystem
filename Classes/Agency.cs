using System;
using System.Collections.Generic;
using System.Text;

using System.Threading;
using System.Linq;

//пользовательские классы
using static AgentSystem.Classes.SystemTools;
using AgentSystem.ReportCommandClasses;

namespace AgentSystem.Classes
{
     // агентство
    class Agency
    {
        //название агенства
        public string Name { get; set; }
        public Context Context { get; set; }
        //справочник агентов
        public Dictionary<string, Agent> AgentReference { get; set; }

        internal Agent Agent
        {
            get => default;
            set
            {
            }
        }

        //id-переменная для генерации имен агентов
        private int id = 0;

        //конструкторы агенства
        public Agency(Context context, string name = "DefaultAgencyName") { Name = name; ActionsOnCreate(context); }

        void ActionsOnCreate(Context context)
        {
            Context = context;
            AgentReference = new Dictionary<string, Agent>();
        }

        // удаление агентов 
        public void DeleteAgent(Agent agent)
        {
            AgentReference.Remove(agent.Name);
        }

        // добавление агента
        public void AddAgent(Agent agent)
        {
            AgentReference.Add(agent.Name, agent);
        }

        private string generate_agent_name(Roles role)
        {
            string name;
            switch (role)
            {
                case Roles.InputStringParser:
                    name = "DataParser";
                    break;
                case Roles.DataTcpListener:
                    name = "DataServer";
                    break;
                case Roles.ToDBWriter:
                    name = "Writer";
                    break;
                case Roles.CommandTcpListener:
                    name = "CommandServer";
                    break;
                default:
                    name = "Inactiver";
                    break;
            }
            return $"{name}{id++:d2}";
        }

        public Agent CreateAgent(Roles role)
        {
            Agent agent = new Agent(generate_agent_name(role), role)
            {
                Agency = this
            };
            AddAgent(agent);
            return agent;
        }

        public void StartAgent(Agent agent)
        {
            if (agent.State == AgentState.Idle)
            {
                agent.State = AgentState.Working;
                agent.Thread = new Thread(agent.Start);
                agent.Thread.Start();
            }
        }

        public void StopAgent(Agent agent)
        {
            if (agent.State != AgentState.Idle)
            {
                AgentMessage msgStop = new AgentMessage
                {
                    From = "Agency",
                    Text = $"{AgentCommand.Stop}",
                    To = agent.Name
                };
                agent.AddMessage(msgStop);
            }
        }

        /*----------------------------------------------------------------------------------------------------
         * Запускает (переводит в состояние Working и стартует в отдельном потоке) нужное количество агентов
         * с учетом имеющихся работающих (Working) и простаивающих (Idle)
         * ---------------------------------------------------------------------------------------------------
         */
        public void StartAgentByRole(Roles role, int count)
        {
            ReportOneRoleStatistic state = StatisticByRole(role);
            //подсчет разницы между нужным для запуска количеством агентов и реально
            //имеющимся зоопарком работающих и простаивающих агентов
            int da = count - state.WorkingAgent;

            //если работающих агентов больше затребованного, ничего не делаем
            if (da > 0)
            {
                int count_idle = state.AvailableAgent - state.WorkingAgent;

                if (da < count_idle)
                {
                    //запустить {da} штук                    
                    foreach (var rec in AgentReference)
                    {
                        Agent agent = rec.Value;
                        if (agent.Role.Name == role && agent.State == AgentState.Idle)
                        {
                            StartAgent(agent);
                            da--;
                        }
                    }
                }
                else
                {
                    //запустить все нерабочие + создать 
                    //и запустить {da - count_idle} штук
                    foreach (var rec in AgentReference)
                    {
                        Agent agent = rec.Value;
                        if (agent.Role.Name == role && agent.State == AgentState.Idle)
                        {
                            StartAgent(agent);
                        }
                    }
                    for (int i = 0; i < (da - count_idle); i++)
                    {
                        Agent agent = CreateAgent(role);
                        StartAgent(agent);
                    }
                }
            }

        }

        public void StopAgentByRole(Roles role, int count)
        {
            ReportOneRoleStatistic state = StatisticByRole(role);
            //если запрошенное кол-во останавливаемых агентов меньше работающих - останавливаем ровно столько, сколько запрошено
            //если запрошенное кол-во останавливаемых агентов больше работающих - останавливаем всех, т.е столько, сколько работает
            if (count < state.WorkingAgent)
            {
                foreach (var rec in AgentReference)
                {
                    if (count > 0)
                    {
                        Agent agent = rec.Value;
                        if (agent.Role.Name == role && agent.State != AgentState.Idle)
                        {
                            StopAgent(agent);
                            count--;
                        }
                    }
                }
            }
            else
            {
                foreach (var rec in AgentReference)
                {
                    Agent agent = rec.Value;
                    if (agent.Role.Name == role && agent.State != AgentState.Idle)
                    {
                        StopAgent(agent);
                    }
                }
            }
        }

        public ReportOneRoleStatistic StatisticByRole(Roles role)
        {
            ReportOneRoleStatistic stat = new ReportOneRoleStatistic
            {
                AvailableAgent = 0,
                WorkingAgent = 0
            };
            foreach (var rec in AgentReference)
            {
                Agent agent = rec.Value;
                if (agent.Role.Name == role)
                {
                    stat.AvailableAgent++;
                    if (agent.State != AgentState.Idle) stat.WorkingAgent++;
                }
            }
            return stat;
        }

        public ReportOneRoleStatistic StatisticByRole_LINQ(Roles role)
        {
            ReportOneRoleStatistic stat = new ReportOneRoleStatistic
            {
                Role = role.ToString(),
                AvailableAgent = 0,
                WorkingAgent = 0
            };
            stat.AvailableAgent = AgentReference
                                    .Where(rec => rec.Value.Role.Name == role)
                                    .Count();
            stat.WorkingAgent = AgentReference
                                    .Where(rec => rec.Value.Role.Name == role && rec.Value.State == AgentState.Working)
                                    .Count();
            return stat;
        }

        public ReportServerInfo TcpServerInfo_LINQ()
        {
            int ms = TimeNowMs();
            List<Roles> tcproles = new List<Roles> { Roles.CommandTcpListener, Roles.DataTcpListener }; 
            ReportServerInfo tcpinfo = new ReportServerInfo();
            //обобщенная информация по tcp-агентам с разбивкой по ролям
            foreach(var role in tcproles)
            {
                tcpinfo.RolesInfo.Add(StatisticByRole_LINQ(role));
            }
            //получить список всех tcp-агентов
            var list_tcp = AgentReference.Where(pair => tcproles.Contains(pair.Value.Role.Name)).Select(a => a.Value);
            //информация по каждому tcp-агенту
            foreach (var tcp_agent in list_tcp)
            {
                ReportTcpServer info = new ReportTcpServer { Name = tcp_agent.Name };
                foreach (var adr in Context.ListDataTcpAddress.Union(Context.ListCommandTcpAddress))
                {
                    if (adr.Agent != null)
                        //if (adr.Agent.Name == info.Name)
                        if (adr.Agent.Equals(tcp_agent))
                        {
                            info.Address = adr.Address;
                            info.Port = adr.Port;
                            break;
                        }
                }
                tcpinfo.TcpInfo.Add(info);
            }
            tcpinfo.TimeMs = TimeNowMs() - ms;
            return tcpinfo;
        }

        public List<ReportTcpServer> TcpServerInfo()
        {
            List<ReportTcpServer> list = new List<ReportTcpServer>();
            foreach(var rec in AgentReference)
            {
                if (rec.Value.Role.Name == Roles.DataTcpListener)
                {
                    ReportTcpServer stat = new ReportTcpServer
                    {
                        Name = rec.Value.Name
                    };
                    foreach (var adr in Context.ListDataTcpAddress)
                    {
                        if (adr.Agent != null)
                            if (adr.Agent.Name == stat.Name)
                            {
                                stat.Address = adr.Address;
                                stat.Port = adr.Port;
                                break;
                            }
                    }
                    list.Add(stat);
                }
                if (rec.Value.Role.Name == Roles.CommandTcpListener)
                {
                    ReportTcpServer stat = new ReportTcpServer
                    {
                        Name = rec.Value.Name
                    };
                    foreach (var adr in Context.ListCommandTcpAddress)
                    {
                        if (adr.Agent != null)
                            if (adr.Agent.Name == stat.Name)
                            {
                                stat.Address = adr.Address;
                                stat.Port = adr.Port;
                                break;
                            }
                    }
                    list.Add(stat);
                }
            }
            return list;
        }

    }

}
