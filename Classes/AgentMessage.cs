using System;
using System.Collections.Generic;
using System.Text;

namespace AgentSystem.Classes
{
    //формат сообщений которыми обмениваются агенты
    class AgentMessage
    {
        public AgentMessage() { Priority = 1; }
        public AgentMessage(string from, string to, string message, int priority = 1)
        {
            From = from;
            To = to;
            Text = message;
            Priority = priority;
        }
        //Приоритет - для чего нужен, пока не придумал (так, - на всякий случай)
        public int Priority { get; set; }
        //Отправитель
        public string From { get; set; }
        //Получатель (адресат)
        public string To { get; set; }
        //Текст сообщения
        public string Text { get; set; }
    }
}
