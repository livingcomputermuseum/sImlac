﻿using System;
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
        //
        // Static System configuration parameters
        //
        public static bool          MITMode;
        public static ImlacCPUType  CPUType;
    }
}
