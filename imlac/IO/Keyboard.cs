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

using System;
using System.Collections.Generic;

namespace imlac.IO
{
    [Flags]
    public enum ImlacKeyModifiers
    {
        None = 0x000,
        Shift = 0x100,
        Ctrl = 0x200,
        Rept = 0x400,
    }

    public enum ImlacKey
    {
        None = 0x0,
        DataXmit = 0x2,
        Down = 0x4,
        Right = 0x5,
        Up = 0x6,
        Left = 0x8,
        Tab = 0x9,
        CR = 0x0d,
        FF = 0x0c,
        LF = 0x0a,
        PageXmit = 0xe,
        Home = 0xf,
        Brk = 0x19,
        Esc = 0x1b,
        Space = 0x20,

        Comma = 0x2c,
        Minus = 0x2d,
        Period = 0x2e,
        Slash = 0x2f,
        K0 = 0x30,
        K1 = 0x31,
        K2 = 0x32,
        K3 = 0x33,
        K4 = 0x34,
        K5 = 0x35,
        K6 = 0x36,
        K7 = 0x37,
        K8 = 0x38,
        K9 = 0x39,
        Colon = 0x3a,
        Semicolon = 0x3b,

        D0 = 0x18,
        D2 = 0x1a,
        D4 = 0x1c,
        D5 = 0x1d,
        D6 = 0x1e,
        Unlabeled = 0x1f,

        A = 0x61,
        B = 0x62,
        C = 0x63,
        D = 0x64,
        E = 0x65,
        F = 0x66,
        G = 0x67,
        H = 0x68,
        I = 0x69,
        J = 0x6a,
        K = 0x6b,
        L = 0x6c,
        M = 0x6d,
        N = 0x6e,
        O = 0x6f,
        P = 0x70,
        Q = 0x71,
        R = 0x72,
        S = 0x73,
        T = 0x74,
        U = 0x75,
        V = 0x76,
        W = 0x77,
        X = 0x78,
        Y = 0x79,
        Z = 0x7a,

        Del = 0x7f,

        Invalid = 0xff,
    }

    public class Keyboard : IIOTDevice
    {
        public Keyboard(ImlacSystem system)
        {
            _system = system;
            Reset();
        }

        static Keyboard()
        {
            BuildKeyMappings();
        }

        public void Clock()
        {
            // If we do not already have a key latched and one has been pressed,
            // we will raise the Ready flag now.
            if (!_keyReady)
            {
                if (_system.Display.NewKeyPressed)
                {                    
                    _keyReady = true;
                }
            }
        }

        public void Reset()
        {
            _keyReady = false;
        }

        public int[] GetHandledIOTs()
        {
            return _handledIOTs;
        }

        public bool KeyReady
        {
            get { return _keyReady; }
        }

        public void ExecuteIOT(int iotCode)
        {            
            switch (iotCode)
            {
                case 0x11:
                    _system.Processor.AC |= GetScancodeForCurrentKey();
                    Trace.Log(LogType.Keyboard, "Key OR'd into AC {0}", Helpers.ToOctal(_system.Processor.AC));
                    break;
         
                case 0x12:
                    _keyReady = false;
                    _system.Display.UnlatchKey();
                    Trace.Log(LogType.Keyboard, "Keyboard flag reset.");
                    break;

                case 0x13:
                    _system.Processor.AC |= GetScancodeForCurrentKey();
                    _keyReady = false;
                    _system.Display.UnlatchKey();
                    Trace.Log(LogType.Keyboard, "Key OR'd into AC {0}, keyboard flag reset.", Helpers.ToOctal(_system.Processor.AC));
                    break;
            }            
        }

        private ushort GetScancodeForCurrentKey()
        {
            ushort scanCode = 0;
            ImlacKey key = _system.Display.Key;
            ImlacKeyModifiers modifiers = _system.Display.KeyModifiers;

            Trace.Log(LogType.Keyboard, "Keypress is {0}", key);

            if (key != ImlacKey.Invalid)
            {
                scanCode = (modifiers & ImlacKeyModifiers.Shift) != 0 ? _keyMappings[key].ShiftedCode : _keyMappings[key].NormalCode;

                if (scanCode == 0)
                {
                    // no code for shifted key, just use normal one.
                    scanCode = _keyMappings[key].NormalCode;
                }

                // bit 8 is always set
                scanCode = (ushort)(scanCode | 0x80);
                
                //
                // The Repeat, Control, and Shift keys correspond to bits 5, 6, and 7 of the
                // scancode returned.
                //
                if ((modifiers & ImlacKeyModifiers.Rept) != 0)
                {
                    scanCode |= 0x400;
                }

                if ((modifiers & ImlacKeyModifiers.Ctrl) != 0)
                {
                    scanCode |= 0x200;
                }

                if ((modifiers & ImlacKeyModifiers.Shift) != 0)
                {
                    scanCode |= 0x100;
                }

                Trace.Log(LogType.Keyboard, "Final keycode is {0}", Helpers.ToOctal(scanCode));
            }

            return scanCode;
        }

        private readonly int[] _handledIOTs = { 0x11, 0x12, 0x13 };

        private bool _keyReady;

        private ImlacSystem _system;

        private struct ImlacKeyMapping
        {
            public ImlacKeyMapping(ImlacKey key, byte normal, byte shifted)
            {
                Key = key;
                NormalCode = normal;
                ShiftedCode = shifted;
            }

            public ImlacKey Key;
            public byte NormalCode;
            public byte ShiftedCode;
        }

        private static void BuildKeyMappings()
        {
            _keyMappings = new Dictionary<ImlacKey, ImlacKeyMapping>();

            AddMapping(ImlacKey.None, 0x0, 0x0);
            AddMapping(ImlacKey.DataXmit, 0x2, 0x0);
            AddMapping(ImlacKey.Down, 0x4, 0x0);
            AddMapping(ImlacKey.Right, 0x5, 0x0);
            AddMapping(ImlacKey.Up, 0x6, 0x0);
            AddMapping(ImlacKey.Left, 0x8, 0x0);
            AddMapping(ImlacKey.Tab, 0x9, 0x0);
            AddMapping(ImlacKey.CR, 0x0d, 0x0);
            AddMapping(ImlacKey.FF, 0x0c, 0x0);
            AddMapping(ImlacKey.LF, 0x0a, 0x0);
            AddMapping(ImlacKey.PageXmit, 0xe, 0x0);
            AddMapping(ImlacKey.Home, 0xf, 0x0);
            AddMapping(ImlacKey.Brk, 0x19, 0x0);
            AddMapping(ImlacKey.Esc, 0x1b, 0x0);
            AddMapping(ImlacKey.Space, 0x20, 0x0);

            AddMapping(ImlacKey.Comma, 0x2c, 0x3c);
            AddMapping(ImlacKey.Minus, 0x2d, 0x3d);
            AddMapping(ImlacKey.Period, 0x2e, 0x3e);
            AddMapping(ImlacKey.Slash, 0x2f, 0x3f);
            AddMapping(ImlacKey.K0, 0x30, 0x0);
            AddMapping(ImlacKey.K1, 0x31, 0x21);
            AddMapping(ImlacKey.K2, 0x32, 0x22);
            AddMapping(ImlacKey.K3, 0x33, 0x23);
            AddMapping(ImlacKey.K4, 0x34, 0x24);
            AddMapping(ImlacKey.K5, 0x35, 0x25);
            AddMapping(ImlacKey.K6, 0x36, 0x26);
            AddMapping(ImlacKey.K7, 0x37, 0x27);
            AddMapping(ImlacKey.K8, 0x38, 0x28);
            AddMapping(ImlacKey.K9, 0x39, 0x29);
        
            AddMapping(ImlacKey.Colon, 0x3a, 0x2a);
            AddMapping(ImlacKey.Semicolon, 0x3b, 0x2b);

            AddMapping(ImlacKey.D0, 0x18, 0x0);
            AddMapping(ImlacKey.D2, 0x1a, 0x0);
            AddMapping(ImlacKey.D4, 0x1c, 0x0);
            AddMapping(ImlacKey.D5, 0x1d, 0x0);
            AddMapping(ImlacKey.D6, 0x1e, 0x0);
            AddMapping(ImlacKey.Unlabeled, 0x1f, 0x0);

            AddMapping(ImlacKey.A, 0x61, 0x41);
            AddMapping(ImlacKey.B, 0x62, 0x42);
            AddMapping(ImlacKey.C, 0x63, 0x43);
            AddMapping(ImlacKey.D, 0x64, 0x44);
            AddMapping(ImlacKey.E, 0x65, 0x45);
            AddMapping(ImlacKey.F, 0x66, 0x46);
            AddMapping(ImlacKey.G, 0x67, 0x47);
            AddMapping(ImlacKey.H, 0x68, 0x48);
            AddMapping(ImlacKey.I, 0x69, 0x49);
            AddMapping(ImlacKey.J, 0x6a, 0x4a);
            AddMapping(ImlacKey.K, 0x6b, 0x4b);
            AddMapping(ImlacKey.L, 0x6c, 0x4c);
            AddMapping(ImlacKey.M, 0x6d, 0x4d);
            AddMapping(ImlacKey.N, 0x6e, 0x4e);
            AddMapping(ImlacKey.O, 0x6f, 0x4f);
            AddMapping(ImlacKey.P, 0x70, 0x50);
            AddMapping(ImlacKey.Q, 0x71, 0x51);
            AddMapping(ImlacKey.R, 0x72, 0x52);
            AddMapping(ImlacKey.S, 0x73, 0x53);
            AddMapping(ImlacKey.T, 0x74, 0x54);
            AddMapping(ImlacKey.U, 0x75, 0x55);
            AddMapping(ImlacKey.V, 0x76, 0x56);
            AddMapping(ImlacKey.W, 0x77, 0x57);
            AddMapping(ImlacKey.X, 0x78, 0x58);
            AddMapping(ImlacKey.Y, 0x79, 0x59);
            AddMapping(ImlacKey.Z, 0x7a, 0x5a);
            
            AddMapping(ImlacKey.Del, 0x7f, 0x0);
        }

        private static void AddMapping(ImlacKey key, byte normal, byte shifted)
        {
            _keyMappings.Add(key, new ImlacKeyMapping(key, normal, shifted));
        }

        private static Dictionary<ImlacKey, ImlacKeyMapping> _keyMappings;

        
    }
}
