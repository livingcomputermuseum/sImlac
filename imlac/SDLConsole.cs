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
using System.Windows.Forms;
using System.Drawing;
using System.Threading;

using imlac.IO;
using SdlDotNet.Graphics;

namespace imlac
{
    public enum DataSwitchMappingMode
    {
        Toggle,     // Pressing a key toggles the switch (from 0 to 1 or from 1 to 0)
        Momentary,  // Holding a key down indicates a "1", release indicates "0"
        MomentaryInverted,  // Same as above, but inverted
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
    /// This is used to filter keyboard messages from the App's message loop.
    /// This is necessary because the "Video" object that SDL provides appears
    /// to eat certain keystrokes and since I have no means to control it
    /// (and I'm not really all that interested in compiling my own version)
    /// this is the only alternative.
    /// 
    /// This class captures keystrokes for keys the emulator is interested in
    /// and fires an event containing the keycode.
    /// These keyboard messages are always passed on to the app.
    /// </summary>
    public class KeyboardFilter : IMessageFilter
    {
        public KeyboardFilter()
        {
            _keyModifiers = ImlacKeyModifiers.None;
            _keyLatched = false;
            _dataSwitches = 0x0; // ffff;
            _latchedKeyCode = ImlacKey.Invalid;
            _dataSwitchMappingMode = DataSwitchMappingMode.Toggle;

            _keyLatchedLock = new ReaderWriterLockSlim();
        }

        static KeyboardFilter()
        {
            BuildKeyMappings();
        }        

        public event EventHandler FullScreenToggle; 

        // TODO: should ensure consistency (threading)

        public ImlacKey LatchedKey
        {
            get { return _latchedKeyCode; }
        }

        public ImlacKeyModifiers Modifiers
        {
            get { return _keyModifiers; }
        }

        public bool KeyLatched
        {
            get
            {
                _keyLatchedLock.EnterReadLock();
                bool latched = _keyLatched;
                _keyLatchedLock.ExitReadLock();
                return latched;
            }
            set
            {
                _keyLatchedLock.EnterWriteLock();
                _keyLatched = value;
                _keyLatchedLock.ExitWriteLock();
            }
        }

        public ushort DataSwitches
        {
            get { return (ushort)_dataSwitches; }
        }

        public DataSwitchMappingMode DataSwitchMode
        {
            get { return _dataSwitchMappingMode;  }
            set { _dataSwitchMappingMode = value; }
        }

        public void MapDataSwitch(uint switchNumber, VKeys key)
        {
            _dataSwitchMappings[switchNumber] = key;
        }

        public VKeys GetDataSwitchMapping(uint switchNumber)
        {
            return _dataSwitchMappings[switchNumber];
        }

        public bool PreFilterMessage(ref Message m)
        {
            bool ret = false;

            switch (m.Msg)
            {
                case WM_SYSKEYDOWN:
                case WM_SOMETHINGDOWN:
                    //
                    // If this is a modifier key (Alt, Shift, Ctrl) then we track it separately
                    // (it is not tracked as a key)
                    //                    
                    switch ((VKeys)m.WParam.ToInt32())
                    {
                        case VKeys.Shift:
                            _keyModifiers |= ImlacKeyModifiers.Shift;
                            break;

                        case VKeys.Ctrl:
                            _keyModifiers |= ImlacKeyModifiers.Ctrl;
                            break;

                        case VKeys.Alt:
                            _keyModifiers |= ImlacKeyModifiers.Rept;
                            break;

                        default:

                            UpdateDataSwitches((VKeys)m.WParam.ToInt32(), true /* key down */);

                            //Console.WriteLine("{0:x}", m.WParam.ToInt32());

                            if ((VKeys)m.WParam.ToInt32() == VKeys.Insert)
                            {
                                if (FullScreenToggle != null)
                                {
                                    FullScreenToggle(this, null);
                                }
                            }

                            _keyLatchedLock.EnterWriteLock();

                            _keyLatched = true;
                            _latchedKeyCode = TranslateKeyCode((VKeys)m.WParam.ToInt32());

                            _keyLatchedLock.ExitWriteLock();
                            break;
                    }
                    break;

                case WM_SYSKEYUP:
                case WM_SOMETHINGUP:
                    //
                    // We only track keyboard modifiers and data switch toggles here.
                    //
                    switch ((VKeys)m.WParam.ToInt32())
                    {
                        case VKeys.Shift:
                            _keyModifiers &= (~ImlacKeyModifiers.Shift);
                            break;

                        case VKeys.Ctrl:
                            _keyModifiers &= (~ImlacKeyModifiers.Ctrl);
                            break;

                        case VKeys.Alt:
                            _keyModifiers &= (~ImlacKeyModifiers.Rept);
                            break;

                        default:
                            UpdateDataSwitches((VKeys)m.WParam.ToInt32(), false /* key up */);

                            _latchedKeyCode = ImlacKey.Invalid;
                            break;
                    }
                    break;

                default:
                    break;
            }

            return ret;
        }

        private ImlacKey TranslateKeyCode(VKeys virtualKey)
        {
            if (_keyMappings.ContainsKey(virtualKey))
            {
                return _keyMappings[virtualKey];
            }
            else
            {
                return ImlacKey.Invalid;
            }
        }


        private void UpdateDataSwitches(VKeys virtualKey, bool keyDown)
        {
            //
            // If this is a key mapped to a front panel switch
            // we will toggle the bit in the DS register based on whether
            // the key is down or up and the specified mapping mode.
            //
            for (int i = 0; i < 16; i++)
            {
                if (_dataSwitchMappings[i] == VKeys.None0)
                {
                    _dataSwitches &= ~(0x1 << (15 - i));
                }
                else if (_dataSwitchMappings[i] == VKeys.None1)
                {
                    _dataSwitches |= (0x1 << (15 - i));
                }
                else if (virtualKey == _dataSwitchMappings[i])
                {
                    switch (_dataSwitchMappingMode)
                    {
                        case DataSwitchMappingMode.Momentary:
                        case DataSwitchMappingMode.MomentaryInverted:
                            if (_dataSwitchMappingMode == DataSwitchMappingMode.MomentaryInverted)
                            {
                                // Invert the sense
                                keyDown = !keyDown;
                            }

                            // toggle this bit
                            if (keyDown)
                            {
                                // or it in
                                _dataSwitches |= (0x1 << (15 - i));
                            }
                            else
                            {
                                // mask it out
                                _dataSwitches &= ~(0x1 << (15 - i));
                            }
                            break;

                        case DataSwitchMappingMode.Toggle:
                            if (keyDown)
                            {
                                // toggle it
                                _dataSwitches ^= (0x1 << (15 - i));
                            }
                            break;
                    }
                }
            }
        }

        private static void BuildKeyMappings()
        {
            _keyMappings = new Dictionary<VKeys, ImlacKey>();

            _keyMappings.Add(VKeys.End, ImlacKey.DataXmit);
            _keyMappings.Add(VKeys.DownArrow, ImlacKey.Down);
            _keyMappings.Add(VKeys.RightArrow, ImlacKey.Right);
            _keyMappings.Add(VKeys.UpArrow, ImlacKey.Up);
            _keyMappings.Add(VKeys.LeftArrow, ImlacKey.Left);
            _keyMappings.Add(VKeys.Tab, ImlacKey.Tab);
            _keyMappings.Add(VKeys.Return, ImlacKey.CR);
            _keyMappings.Add(VKeys.PageUp, ImlacKey.FF);
            _keyMappings.Add(VKeys.PageDown, ImlacKey.PageXmit);
            _keyMappings.Add(VKeys.Home, ImlacKey.Home);
            _keyMappings.Add(VKeys.Pause, ImlacKey.Brk);
            _keyMappings.Add(VKeys.Escape, ImlacKey.Esc);
            _keyMappings.Add(VKeys.Space, ImlacKey.Space);

            _keyMappings.Add(VKeys.Comma, ImlacKey.Comma);
            _keyMappings.Add(VKeys.Plus, ImlacKey.Minus);
            _keyMappings.Add(VKeys.Period, ImlacKey.Period);
            _keyMappings.Add(VKeys.QuestionMark, ImlacKey.Slash);
            _keyMappings.Add(VKeys.Zero, ImlacKey.K0);
            _keyMappings.Add(VKeys.One, ImlacKey.K1);
            _keyMappings.Add(VKeys.Two, ImlacKey.K2);
            _keyMappings.Add(VKeys.Three, ImlacKey.K3);
            _keyMappings.Add(VKeys.Four, ImlacKey.K4);
            _keyMappings.Add(VKeys.Five, ImlacKey.K5);
            _keyMappings.Add(VKeys.Six, ImlacKey.K6);
            _keyMappings.Add(VKeys.Seven, ImlacKey.K7);
            _keyMappings.Add(VKeys.Eight, ImlacKey.K8);
            _keyMappings.Add(VKeys.Nine, ImlacKey.K9);
            _keyMappings.Add(VKeys.Minus, ImlacKey.Colon);
            _keyMappings.Add(VKeys.Semicolon, ImlacKey.Semicolon);

            _keyMappings.Add(VKeys.Keypad0, ImlacKey.D0);
            _keyMappings.Add(VKeys.Keypad2, ImlacKey.D2);
            _keyMappings.Add(VKeys.Keypad4, ImlacKey.D4);
            _keyMappings.Add(VKeys.Keypad5, ImlacKey.D5);
            _keyMappings.Add(VKeys.Keypad6, ImlacKey.D6);
            _keyMappings.Add(VKeys.DoubleQuote, ImlacKey.Unlabeled);

            _keyMappings.Add(VKeys.A, ImlacKey.A);
            _keyMappings.Add(VKeys.B, ImlacKey.B);
            _keyMappings.Add(VKeys.C, ImlacKey.C);
            _keyMappings.Add(VKeys.D, ImlacKey.D);
            _keyMappings.Add(VKeys.E, ImlacKey.E);
            _keyMappings.Add(VKeys.F, ImlacKey.F);
            _keyMappings.Add(VKeys.G, ImlacKey.G);
            _keyMappings.Add(VKeys.H, ImlacKey.H);
            _keyMappings.Add(VKeys.I, ImlacKey.I);
            _keyMappings.Add(VKeys.J, ImlacKey.J);
            _keyMappings.Add(VKeys.K, ImlacKey.K);
            _keyMappings.Add(VKeys.L, ImlacKey.L);
            _keyMappings.Add(VKeys.M, ImlacKey.M);
            _keyMappings.Add(VKeys.N, ImlacKey.N);
            _keyMappings.Add(VKeys.O, ImlacKey.O);
            _keyMappings.Add(VKeys.P, ImlacKey.P);
            _keyMappings.Add(VKeys.Q, ImlacKey.Q);
            _keyMappings.Add(VKeys.R, ImlacKey.R);
            _keyMappings.Add(VKeys.S, ImlacKey.S);
            _keyMappings.Add(VKeys.T, ImlacKey.T);
            _keyMappings.Add(VKeys.U, ImlacKey.U);
            _keyMappings.Add(VKeys.V, ImlacKey.V);
            _keyMappings.Add(VKeys.W, ImlacKey.W);
            _keyMappings.Add(VKeys.X, ImlacKey.X);
            _keyMappings.Add(VKeys.Y, ImlacKey.Y);
            _keyMappings.Add(VKeys.Z, ImlacKey.Z);

            _keyMappings.Add(VKeys.Delete, ImlacKey.Del);
        }

        private static Dictionary<VKeys, ImlacKey> _keyMappings;

        private ImlacKey            _latchedKeyCode;        
        private ImlacKeyModifiers   _keyModifiers;
        private bool                _keyLatched;

        private ReaderWriterLockSlim _keyLatchedLock;

        // Data switch mappings:
        // There are 16 switches mapped here, a value of None0 or None1 means
        // that no key is mapped and to hardcode return value to 0 or 1
        // The first entry corresponds to bit 0 (MSB)
        // and the last entry corresponds to bit 15 (LSB)
        private VKeys[] _dataSwitchMappings = 
        {
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
            VKeys.None0,
        };

        private int _dataSwitches;
        private DataSwitchMappingMode _dataSwitchMappingMode;

        private const int WM_SOMETHINGUP = 0x105;
        private const int WM_SOMETHINGDOWN = 0x104;
        private const int WM_KEYUP = 0x103;
        private const int WM_KEYDOWN = 0x102;
        private const int WM_SYSKEYUP = 0x101;
        private const int WM_SYSKEYDOWN = 0x100;

        
    }
    //
    // Provides a console using SDL.
    //
    public class SDLConsole : IImlacConsole
    {
        public SDLConsole(float scaleFactor)
        {
            if (scaleFactor <= 0)
            {
                throw new ArgumentOutOfRangeException("scaleFactor");
            }

            _scaleFactor = scaleFactor;
            _throttleFramerate = true;

            _lock = new ReaderWriterLockSlim();
            _swapLock = new ReaderWriterLockSlim();
         
            _frame = 0;

            _frameTimer = new FrameTimer(40);
            _timer = new HighResTimer();

            _fullScreen = false;

            _displayList = new List<Vector>(_displayListSize);
            _displayListIndex = 0;

            //
            // Prepopulate the display list with Vectors.  Only those used in the current frame are
            // actually rendered, we prepopulate the list to prevent having to cons up new ones
            // constantly.
            //
            for (int i = 0; i < _displayListSize; i++)
            {
                _displayList.Add(new Vector(DrawingMode.Off, 1, 0, 0, 0, 0));
            }

            InvokeDisplayThread();
        }        
        
        public bool IsKeyPressed
        {
            get { return _keyboardFilter.KeyLatched; }
        }

        public ImlacKey Key
        {
            get { return _keyboardFilter.LatchedKey; }
        }        

        public ImlacKeyModifiers KeyModifiers
        {
            get { return _keyboardFilter.Modifiers; }
        }

        public void UnlatchKey()
        {
            _keyboardFilter.KeyLatched = false;
        }

        public ushort DataSwitches
        {
            get { return _keyboardFilter.DataSwitches; }
        } 

        public bool ThrottleFramerate
        {
            get { return _throttleFramerate; }
            set { _throttleFramerate = value; }
        }

        public bool DataSwitchMappingEnabled
        {
            get { return _dataSwitchMappingEnabled; }
            set { _dataSwitchMappingEnabled = value; }
        }

        public DataSwitchMappingMode DataSwitchMode
        {
            get { return _keyboardFilter.DataSwitchMode; }
            set { _keyboardFilter.DataSwitchMode = value; }
        }

        public bool FullScreen
        {
            get { return _fullScreen; }
            set
            {
                if (value != _fullScreen)
                {
                    _fullScreen = value;
                    UpdateScreenMode();
                }
            }
        }

        public void ClearDisplay()
        {
            _lock.EnterWriteLock();
            _displayListIndex = 0;
            _lock.ExitWriteLock();
            RenderCurrent(true);
        }

        public void SetScale(float scale)
        {
            if (scale <= 0)
            {
                throw new ArgumentOutOfRangeException("scale");
            }

            _scaleFactor = scale;
            UpdateDisplayScale();
        }

        public void MapDataSwitch(uint switchNumber, VKeys key)
        {
            _keyboardFilter.MapDataSwitch(switchNumber, key);
        }

        public VKeys GetDataSwitchMapping(uint switchNumber)
        {
            return _keyboardFilter.GetDataSwitchMapping(switchNumber);
        }

        private void UpdateDisplayScale()
        {
            _xResolution = (int)(2048.0 * _scaleFactor);
            _yResolution = (int)(2048.0 * _scaleFactor);

            Video.SetVideoMode((int)_xResolution, (int)_yResolution, 32, false, false, _fullScreen, true);
            Video.WindowCaption = "Imlac PDS-1";
            _displaySurface = Video.Screen.CreateCompatibleSurface((int)_xResolution, (int)_yResolution, true);
            _displayBox = new SdlDotNet.Graphics.Primitives.Box(0, 0, (short)Video.Screen.Rectangle.Width, (short)Video.Screen.Rectangle.Height);
        }

        public void MoveAbsolute(uint x, uint y, DrawingMode mode)
        {            
            //
            // Take coordinates as an 11-bit quantity (0-2048) even though we may not be displaying the full resolution.
            //             
            if (mode != DrawingMode.Off)
            {
                AddNewVector(mode, _x, _y, x, y);                
            }

            _x = x;
            _y = y;
        }

        public void DrawPoint(uint x, uint y)
        {
            _x = x;
            _y = y;

            AddNewVector(DrawingMode.Point, x, y, x, y);
        }

        public void FrameDone()
        {
            RenderCurrent(true);
            //
            // Sync to 40hz framerate
            //
            if (_throttleFramerate)
            {
                _frameTimer.WaitForFrame();
            }
        }        

        private void InvokeDisplayThread()
        {
            _displayThread = new System.Threading.Thread(new System.Threading.ThreadStart(DisplayThread));
            _displayThread.Start();
           
            _initEvent = new ManualResetEvent(false);

            WaitHandle[] handles = { _initEvent };

            WaitHandle.WaitAll(handles);

            Thread.Sleep(500);           

        }

        private void DisplayThread()
        {
            UpdateDisplayScale();
            _initEvent.Set();

            _keyboardFilter = new KeyboardFilter();
            Application.AddMessageFilter(_keyboardFilter);

            _keyboardFilter.FullScreenToggle += new EventHandler(OnFullScreenToggle);
          
            Application.Run();
        }

        void OnFullScreenToggle(object sender, EventArgs e)
        {
            _fullScreen = !_fullScreen;

            UpdateScreenMode();            
        }                    
       
        public void RenderCurrent(bool completeFrame)
        {            
            // Draw the current set of vectors
            _lock.EnterReadLock();
            _frame++;

            if (_frame == 60)
            {
                double currentTime = _timer.GetCurrentTime();
                double fps = _frame / ((currentTime - _lastTime));
                _lastTime = currentTime;
                _frame = 0;

                Video.WindowCaption = String.Format("Imlac PDS-1 fps {0}", fps);
            }

            //
            // If we're drawing a complete frame (not running in debug mode)
            // fade out the last frame by drawing an alpha-blended black rectangle over the display.
            // (slow persistence phosphor simulation!)            
            // Otherwise clear the display completely.
            //
            _displayBox.Draw(_displaySurface, completeFrame ? Color.FromArgb(32, Color.Black) : Color.Black, false, true);

            // And draw in this frame's vectors
            for (int i = 0; i < _displayListIndex; i++)
            {
                _displayList[i].Draw(_displaySurface);
            }

            _lock.ExitReadLock();

            _swapLock.EnterReadLock();
            Video.Screen.Blit(_displaySurface);

            if (completeFrame)
            {
                _displayListIndex = 0;
            }

            Video.Screen.Update();
            _swapLock.ExitReadLock();
        }

        private void AddNewVector(DrawingMode mode, uint startX, uint startY, uint endX, uint endY)
        {
            //
            // Scale the vector to the current scaling factor.
            // The Imlac specifies 11 bits of resolution (2048 points in X and Y)
            // which corresponds to a _scaleFactor of 1.0.
            //
            startX = (uint)(startX * _scaleFactor);
            startY = (uint)(startY *_scaleFactor);
            endX = (uint)(endX * _scaleFactor);
            endY = (uint)(endY * _scaleFactor);

            _lock.EnterWriteLock();

            Vector newVector = _displayList[_displayListIndex];
            newVector.Modify(mode, (short)startX, (short)(_yResolution - startY), (short)endX, (short)(_yResolution - endY));
            _displayListIndex++;

            _lock.ExitWriteLock();
        }

        private void UpdateScreenMode()
        {
            _swapLock.EnterWriteLock();
            Video.SetVideoMode((int)_xResolution, (int)_yResolution, 32, false, false, _fullScreen, true);
            _displaySurface = Video.Screen.CreateCompatibleSurface((int)_xResolution, (int)_yResolution, true);
            _swapLock.ExitWriteLock();
        }

        private class Vector
        {
            public Vector(DrawingMode mode, int thickness, uint startX, uint startY, uint endX, uint endY)
            {
                _mode = mode;
                _lines = new SdlDotNet.Graphics.Primitives.Line[thickness];

                for (int i = 0; i < thickness; i++)
                {
                    _lines[i] = new SdlDotNet.Graphics.Primitives.Line((short)(startX + i), (short)(startY + i), (short)(endX + i), (short)(endY + i));
                }

                UpdateColor();
            }

            public void Modify(DrawingMode mode, short startX, short startY, short endX, short endY)
            {
                if (_mode != mode)
                {
                    _mode = mode;
                    UpdateColor();
                }

                for (int i = 0; i < _lines.Length; i++)
                {
                    _lines[i].XPosition1 = (short)(startX + i);
                    _lines[i].XPosition2 = (short)(endX + i);
                    _lines[i].YPosition1 = (short)(startY + i);
                    _lines[i].YPosition2 = (short)(endY + i);
                }
            }

            public void Draw(Surface displaySurface)
            {
                // TODO: handle dotted lines, line thickness options
                for (int i = 0; i < _lines.Length; i++)
                {
                    _lines[i].Draw(displaySurface, _color, true);
                }
            }

            private void UpdateColor()
            {
                switch (_mode)
                {
                    case DrawingMode.Dotted:
                    case DrawingMode.Normal:
                        _color = NormalColor;
                        break;

                    case DrawingMode.Point:
                        _color = PointColor;
                        break;

                    case DrawingMode.SGR1:
                        _color = SGRColor;
                        break;

                    case DrawingMode.Debug:
                        _color = DebugColor;
                        break;
                }
            }

            private DrawingMode _mode;
            private SdlDotNet.Graphics.Primitives.Line[] _lines;
            private Color _color;

            private static Color NormalColor = Color.FromArgb(196, Color.ForestGreen);
            private static Color PointColor = Color.FromArgb(255, Color.ForestGreen);
            private static Color SGRColor = Color.FromArgb(128, Color.ForestGreen);
            private static Color DebugColor = Color.FromArgb(255, Color.OrangeRed);
        }


        private System.Threading.Thread _displayThread;
        private Surface _displaySurface;
        private SdlDotNet.Graphics.Primitives.Box _displayBox;

        private ManualResetEvent _initEvent;
        
        private uint _x;
        private uint _y;

        private int _xResolution;
        private int _yResolution;
        private float _scaleFactor;

        private bool _fullScreen;
        private bool _throttleFramerate;
        private bool _dataSwitchMappingEnabled;

        private int _displayListIndex;
        private List<Vector> _displayList;
        private const int _displayListSize = 100000;        // Considerably more than a real Imlac could ever hope to draw in a single frame.

        private System.Threading.ReaderWriterLockSlim _lock;
        private System.Threading.ReaderWriterLockSlim _swapLock;

        // keyboard input data
        private KeyboardFilter _keyboardFilter;

        private uint _frame;
        private double _lastTime;

        // Framerate management
        FrameTimer _frameTimer;
        HighResTimer _timer;
        
    }
}
