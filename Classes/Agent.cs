using System;
using System.Collections.Generic;
using System.Text;

using System.Threading;

//пользовательские классы
using static AgentSystem.Classes.SystemTools;

namespace AgentSystem.Classes
{
    //режимы работы агента: бесконечный; работает пока не пуст буфер
    enum AgentMode {Infinity, NotEmptyBuffer}
    
    //соcтояния агента: работает, простаивает, на паузе, запускается после паузы с дальнейшим переходом в режим stateWorking
    enum AgentState { Working, Idle, OnPause, OnContinue}

    //команды, выполняемые агентом
    //NoCommand      -  команды нет.
    //Start("start") -  в состоянии Idle инициализируется, запускается в отдельном потоке и начинает работу,
    //                  в состоянии OnPause аналогична Continue,
    //                  в состоянии Working и OnContinue ничего не меняет (игнорируется)
    //Stop("stop")  -   в состоянии Idle ничего не делает,
    //                  во всех других - завершает текущую итерацию и остановливается, переходит в состояние Idle,
    //                  "завершает" - это означает, что происходит переход на окончание работы агента с закрытием потока.
    //Pause("pause") -  в состоянии Idle и OnPause ничего не делает
    //                  в состоянии Working завершает текущую итерацию и приостаналивает работу агента (весь контекст
    //                  сохраняется), поток не завершается, работа агента крутится по холостой ветке, т.е. агент формально
    //                  работает, но полезной работы не делает.
    //                  в состоянии OnContinue ожидает перехода агента в состояние Working и далее см. выше
    //Continue("continue") - игнорируется во всех состояниях, кроме OnPause.
    //                       из состояния OnPause агент переводится в состояние OnContinue, в котором происходит
    //                       восстановление ранее сохраненного контекста (если необходимо) с дальнейшим переводом агента в 
    //                       в состояние Working. Если восстановление контекста исполнения не требуется, допускается
    //                       пропуск состояния OnContinue, т.е. происходит переход из состояния OnPause сразу 
    //                       в состояние Working.
    enum AgentCommand { NoCommand, Start, Stop, Pause, Continue }

    //агент-оператор - рабочий на все руки
    class Agent
    {
        //для рефлексии - например при удалении из списка разнотипных агентов 
        public string ClassType { get; set; }

        //основное кодовое имя агента
        public string Name { get; set; }

        //агентство, в котором зарегестрирован агент
        public Agency Agency { get; set; }

        //роль агента, которая определяет все его действия
        public AgentRoles Role { get; set; }

        //режим работы агента (нужно для работы в потоке):
        // NotEmptyBuffer - при опустошении входного буфера заканчивает работу (для проведения тестов, в основном);
        // Infinity - крутится бесконечно - рабочий режим (может быть прерван извне путем пересылки команды "stop" в 
        //            сообщении агенту);
        public AgentMode Mode { get; set; }

        //текущее состояние агента
        public AgentState State { get; set; }

        //поток, в котором агент будет выполняться (каждый агент выполняется в отдельном потоке)
        public Thread Thread { get; set; }

        //сообщения от других агентов: агенты способны общаться напрямую
        public Queue<AgentMessage> Messages { get; set; }

        //агент может активироваться и чего-нибудь сделать
        //map<string, function<void(AgentFunctions&, MissionDetails&, AgentReport&) > >AgentTasks;
        //public delegate void Function(string agentFunctions, string MissionDetails, string AgentReport);
        //public Dictionary<string, Function> AgentTasks;

        public Agent(string name, Roles role, string class_type = "Agent") => ActionOnCreate(name, role, class_type); 
        //public Agent(Agent agent) { ActionOnCreate(agent.Name, agent.Role.Name, agent.ClassType); }

        void ActionOnCreate(string name, Roles role, string class_type)
        {
            ClassType = class_type;
            Role = new AgentRoles(this, role);
            Name = name;
            Messages = new Queue<AgentMessage>();
            Mode = AgentMode.Infinity;
            State = AgentState.Idle;
        }

        public AgentMessage ReadMessage()
        {
            AgentMessage returnMessage = Messages.Dequeue();       //front() pop();
            return returnMessage;
        }

        public void AddMessage(AgentMessage message)
        {
            Messages.Enqueue(message); //push()
        }

        public AgentCommand CheckMessage()
        {
            AgentCommand command = AgentCommand.NoCommand;           
            if (Messages.Count > 0)
            {
                AgentMessage msg = ReadMessage();
                if (msg.From == "Agency" && msg.Text == $"{AgentCommand.Stop}") command = AgentCommand.Stop;
            }
            return command;
        }

        public void Start()
        {
            State = AgentState.Working; //агент работает
            WriteLog($"{Name}.{Role.Name}: настройка контекста работы агента");
            if (Role.Run(Startmode.Setup))
            {
                WriteLog($"{Name}.{Role.Name}: агент приступил к работе.");
                while (true)
                {
                    //надо проверять сообщения - вдруг поступит команда "забить на работу"
                    if (CheckMessage() != AgentCommand.Stop)
                    {
                        Role.Run(Startmode.Main);
                    }
                    else
                    {
                        WriteLog($"{Name}.{Role.Name}: получена команда \"stop\"");
                        Role.Run(Startmode.Finally);
                        break;
                    }
                    Thread.Sleep(1);
                }
                WriteLog($"{Name}: Отработал метод роли агента {Role.Name}");
                State = AgentState.Idle; //перевод агента в режим простоя ("Нерабочий режим") и завершение потока
            }
            else // настройка завершена неудачно 
            {
                WriteLog($"{Name}.{Role.Name}: завершена работа агента из-за неудачной настройки.");
                State = AgentState.Idle; //перевод агента в режим простоя ("Нерабочий режим") и завершение потока
            }
        }

    }
}

