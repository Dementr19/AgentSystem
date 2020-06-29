using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace AgentSystem.ReportCommandClasses
{
    class ReportCommandError : Report
    {
        public bool Result { get; set; }
        public string Error { get; set; }
    }
}
