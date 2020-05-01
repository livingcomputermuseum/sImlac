using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static imlac.ImlacSystem;

namespace imlac
{
    public enum ImlacCPUType
    {
        PDS1,
        PDS4,
    }

    public static class Configuration
    {
        static Configuration()
        {
            MITMode = false;
            CPUType = ImlacCPUType.PDS1;
            SquiggleMode = false;
        }
        //
        // Static System configuration parameters
        //
        public static bool          MITMode;
        public static ImlacCPUType  CPUType;
        public static bool          SquiggleMode;
    }
}
