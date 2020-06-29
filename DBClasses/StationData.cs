using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace AgentSystem.DBClasses
{
    public class StationData
    {
        public int Id { get; set; }

        public int TcpTime { get; set; }
        public int GetSTime { get; set; }
        public int PutDbTime { get; set; }
        public int GetDbTime { get; set; }
        public int DeltaWriter { get; set; }
        public int DbTime { get; set; }

        public int StationId { get; set; }
        public int NodeId { get; set; }
        public DateTime Time { get; set; }
        //старые поля
        /*
        public int? Idto { get; set; }
        public float? Crat { get; set; }
        public float? Icrh { get; set; }
        public float? Crdp { get; set; }
        public int? Crpa { get; set; }
        public int? Crwd { get; set; }
        public int? Crws { get; set; }
        public int? Crcl { get; set; }
        public int? Crhc { get; set; }
        public int? Crhv { get; set; }
        */
        //новые поля
        public int? Fid00 { get; set; }
        public float? Fid01 { get; set; }
        public float? Fid02 { get; set; }
        public float? Fid03 { get; set; }
        public int? Fid04 { get; set; }
        public int? Fid05 { get; set; }
        public int? Fid06 { get; set; }
        public int? Fid07 { get; set; }
        public int? Fid08 { get; set; }
        public int? Fid09 { get; set; }
    }
}
