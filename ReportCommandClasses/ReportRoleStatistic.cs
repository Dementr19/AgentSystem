using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace AgentSystem.ReportCommandClasses
{
    class ReportOneRoleStatistic : Report
    {
        public string Role { get; set; }
        public int AvailableAgent { get; set; }
        public int WorkingAgent { get; set; }
    }

    class ReportRoleStatistic : Report
    {
        public List<ReportOneRoleStatistic> RolesInfo { get; set; } = new List<ReportOneRoleStatistic>();
    }
}
