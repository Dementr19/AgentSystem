using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace AgentSystem.ReportCommandClasses
{
    class Report 
    {
        public int TimeMs { get; set; }

        public string ToJSON()
        {
            string res = JsonConvert.SerializeObject(this);
            return res;
        }
    }
}
