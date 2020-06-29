using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using AgentSystem.DBClasses;

namespace AgentSystem.ReportCommandClasses
{
    class ReportDBRequest : Report
    {
        public List<StationData> Records { get; set; } = new List<StationData>();

        /*
        public ReportDBRequest()
        {
            FromTime = DateTime.Now;
            ToTime = DateTime.Now;
            Records = new List<StationData>();
        }*/
    }
}
