using System;
using System.Collections.Generic;
using System.Text;

namespace AgentSystem.ReportCommandClasses
{
    class ReportTcpServer : Report
    {
        public string Name { get; set; }
        public string Address { get; set; } = null;
        public int? Port { get; set; } = null;
    }

    class ReportServerInfo : Report
    {
        public List<ReportOneRoleStatistic> RolesInfo { get; set; } = new List<ReportOneRoleStatistic>();
        public List<ReportTcpServer> TcpInfo { get; set; } = new List<ReportTcpServer>();
    }
}
