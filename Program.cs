using System;

namespace AgentSystem.Classes
{
    class Program
    {
        static void Main(string[] args)
        {
            AgentManager SuperAgent = new AgentManager(args);
            SuperAgent.Run();
            return;
        }
    }
}
