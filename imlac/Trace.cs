/*  
    This file is part of sImlac.

    sImlac is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    sImlac is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with sImlac.  If not, see <http://www.gnu.org/licenses/>.
*/

#define TRACE

using System;

namespace imlac
{
    /// <summary>
    /// Specifies the category of Trace message --
    /// Trace messages can be limited to a certain set, this defines those sets.
    /// </summary>
    [Flags]
    public enum LogType
    {        
        None =              0x0,
        Processor =         0x1,
        DisplayProcessor =  0x2,
        Display =           0x4,
        Keyboard =          0x8,        
        Interrupt =         0x10,
        TTY =               0x20,
        PTR =               0x40,
        Telnet =            0x80,
        All =               0x7fffffff
    }

    public static class Trace
    {      
        public static void Log(LogType level, string format, params object[] args)
        {
            if ((_level & level) == level)
            {
                // OK to trace
                SetColor(level);
                Console.WriteLine(format, args);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
        
        public static LogType TraceLevel
        {
            get { return _level; }
            set 
            { 
                _level = value;
                if (_level == LogType.None)
                {
                    Trace.TraceOn = false;
                }
                else
                {
                    Trace.TraceOn = true;
                }
            }
        }

        /// <summary>
        /// Selects a color for the given type.  
        /// </summary>
        /// <param name="level"></param>
        private static void SetColor(LogType level)
        {
            ConsoleColor color = ConsoleColor.Gray;
            switch (level)
            {
                case LogType.Processor:
                    color = ConsoleColor.Gray;
                    break;

                case LogType.DisplayProcessor:
                    color = ConsoleColor.DarkGreen;
                    break;

                case LogType.Display:
                    color = ConsoleColor.Green;
                    break;

                case LogType.Keyboard:
                    color = ConsoleColor.Cyan;
                    break;

                case LogType.Interrupt:
                    color = ConsoleColor.Blue;
                    break;

                case LogType.Telnet:
                    color = ConsoleColor.Cyan;
                    break;
               
                default:
                    // No change
                    break;
            }

            Console.ForegroundColor = color;
        }

        public static bool TraceOn = false;
        private static LogType _level;
    }
}
