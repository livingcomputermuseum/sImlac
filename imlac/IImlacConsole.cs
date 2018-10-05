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

using imlac.IO;

namespace imlac
{
    public enum DrawingMode
    {
        Off,
        Normal,
        Dotted,
        Hidden,
        SGR1,      // reduced intensity
        Point,     // increased intensity
        Debug,     // For debugging purposes
    }
    
    public enum VKeys
    {
        // Keys that map to Imlac keyboard keys
        Shift = 0x10,
        Ctrl = 0x11,
        Alt = 0x12,

        End = 0x23,
        DownArrow = 0x28,
        RightArrow = 0x27,
        UpArrow = 0x26,
        LeftArrow = 0x25,
        Tab = 0x9,
        Return = 0xd,
        PageUp = 0x21,
        PageDown = 0x22,
        Home = 0x24,
        Pause = 0x91,
        Escape = 0x1b,
        Space = 0x20,

        Comma = 0xbc,
        Plus = 0xbb,
        Period = 0xbe,
        QuestionMark = 0xbf,
        Zero = 0x30,
        One = 0x31,
        Two = 0x32,
        Three = 0x33,
        Four = 0x34,
        Five = 0x35,
        Six = 0x36,
        Seven = 0x37,
        Eight = 0x38,
        Nine = 0x39,
        Minus = 0xbd,
        Semicolon = 0xba,

        Keypad0 = 0x60,
        Keypad2 = 0x62,
        Keypad4 = 0x64,
        Keypad5 = 0x65,
        Keypad6 = 0x66,
        DoubleQuote = 0xde,

        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4a,
        K = 0x4b,
        L = 0x4c,
        M = 0x4d,
        N = 0x4e,
        O = 0x4f,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5a,

        Delete = 0x8,

        // Additional keys, not used by the Imlac but available
        // for data switch mapping.
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7a,
        F12 = 0x7b,

        Keypad1 = 0x61,
        Keypad3 = 0x63,
        Keypad7 = 0x67,
        Keypad8 = 0x68,
        Keypad9 = 0x69,


        // hack to toggle fullscreen.
        Insert = 0x2d,

        // Special values for data switch mappings
        None0 = 0,      // No key mapped to data switch, DS for this bit is set to 0
        None1 = 1,      // Ditto, but for the value 1
    }

    /// <summary>
    /// IImlacConsole provides the interface for the components making up a standard
    /// "console" setup, which at the moment includes only the Display and Keyboard,
    /// but could be extended.
    /// </summary>
    public interface IImlacConsole
    {
        /// <summary>
        /// Indicates whether a key is currently pressed.
        /// </summary>
        bool IsKeyPressed
        {
            get;
        }

        /// <summary>
        /// Indicates the currently pressed key (if IsKeyPressed is true)
        /// </summary>
        ImlacKey Key
        {
            get;
        }

        /// <summary>
        /// Indicates any modifier bits (ctrl, shift) being pressed.
        /// </summary>
        ImlacKeyModifiers KeyModifiers
        {
            get;
        }

        /// <summary>
        /// Indicates the status of the front-panel data switches
        /// </summary>
        ushort DataSwitches
        {
            get;
        }

        bool ThrottleFramerate
        {
            get;
            set;
        }

        bool DataSwitchMappingEnabled
        {
            get;
            set;
        }

        DataSwitchMappingMode DataSwitchMode
        {
            get;
            set;
        }

        bool FullScreen
        {
            get;
            set;
        }

        void UnlatchKey();

        void ClearDisplay();

        void MoveAbsolute(uint x, uint y, DrawingMode mode);

        void DrawPoint(uint x, uint y);

        void RenderCurrent(bool completeFrame);

        void FrameDone();

        void SetScale(float scale);

        void MapDataSwitch(uint switchNumber, VKeys key);

        VKeys GetDataSwitchMapping(uint switchNumber);
    }
}
