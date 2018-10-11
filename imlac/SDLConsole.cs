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
using SDL2;
using static SDL2.SDL;

namespace imlac
{
    public enum DataSwitchMappingMode
    {
        Toggle,     // Pressing a key toggles the switch (from 0 to 1 or from 1 to 0)
        Momentary,  // Holding a key down indicates a "1", release indicates "0"
        MomentaryInverted,  // Same as above, but inverted
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
            _keyLatchedLock = new ReaderWriterLockSlim();
            _syncEvent = new AutoResetEvent(false);

             _frame = 0;

            try
            {
                _frameTimer = new FrameTimer(40);
            }
            catch
            {
                // Unable to initialize frame timer, we will not be able
                // to throttle execution.
                _frameTimer = null;
            }

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
                _displayList.Add(new Vector(DrawingMode.Off, 0, 0, 0, 0));
            }

            BuildKeyMappings();
        }        
        
        public bool IsKeyPressed
        {
            get
            {
                _keyLatchedLock.EnterReadLock();
                bool latched = _keyLatched;
                _keyLatchedLock.ExitReadLock();
                return latched;
            }
        }

        public ImlacKey Key
        {
            get { return _latchedKeyCode; }
        }        

        public ImlacKeyModifiers KeyModifiers
        {
            get { return _keyModifiers; }
        }

        public void UnlatchKey()
        {
            _keyLatchedLock.EnterReadLock();
            _keyLatched = false;
            _keyLatchedLock.ExitReadLock();
        }

        public ushort DataSwitches
        {
            get { return (ushort)_dataSwitches; }
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
            get { return _dataSwitchMappingMode; }
            set { _dataSwitchMappingMode = value; }
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

        public void Shutdown()
        {
            //
            // Tell the SDL event loop to wrap things up.
            //
            _userEvent.type = SDL.SDL_EventType.SDL_QUIT;
            SDL.SDL_PushEvent(ref _userEvent);
        }

        /// <summary>
        /// Waits for the screen to be ready for access.
        /// </summary>
        public void WaitForSync()
        {
            _syncEvent.WaitOne();
        }

        public void Show()
        {
            ShowInternal();
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
            _dataSwitchMappings[switchNumber] = key;
        }

        public VKeys GetDataSwitchMapping(uint switchNumber)
        {
            return _dataSwitchMappings[switchNumber];
        }

        private void InitializeSDL()
        {
            DoUpdateDisplayScale();

            int retVal = 0;

            // Get SDL humming
            if ((retVal = SDL.SDL_Init(SDL.SDL_INIT_VIDEO)) < 0)
            {
                throw new InvalidOperationException(String.Format("SDL_Init failed.  Error {0:x}", retVal));
            }

            // 
            if (SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "0") == SDL.SDL_bool.SDL_FALSE)
            {
                throw new InvalidOperationException("SDL_SetHint failed to set scale quality.");
            }

            _sdlWindow = SDL.SDL_CreateWindow(
                "Imlac PDS-1",
                SDL.SDL_WINDOWPOS_UNDEFINED,
                SDL.SDL_WINDOWPOS_UNDEFINED,
                _xResolution,
                _yResolution,
                _fullScreen ? SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN : SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);


            if (_sdlWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException("SDL_CreateWindow failed.");
            }

            _sdlRenderer = SDL.SDL_CreateRenderer(_sdlWindow, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (_sdlRenderer == IntPtr.Zero)
            {
                // Fall back to software
                _sdlRenderer = SDL.SDL_CreateRenderer(_sdlWindow, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);

                if (_sdlRenderer == IntPtr.Zero)
                {
                    // Still no luck.
                    throw new InvalidOperationException("SDL_CreateRenderer failed.");
                }
            }

            SDL.SDL_SetRenderDrawBlendMode(_sdlRenderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);

            // Clear the screen.
            SDL.SDL_SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0xff);
            SDL.SDL_RenderFillRect(_sdlRenderer, ref _displayRect);
            SDL.SDL_RenderPresent(_sdlRenderer);

            // Register a User event for rendering and resizing.
            _userEventType = SDL.SDL_RegisterEvents(1);
            _userEvent = new SDL.SDL_Event();
            _userEvent.type = SDL.SDL_EventType.SDL_USEREVENT;
            _userEvent.user.type = _userEventType;           
        }

        private void UpdateDisplayScale()
        {
            //
            // Send a render event to the SDL message loop so that things
            // will get rendered.
            //
            _userEvent.user.code = (int)UserEventType.Resize;
            SDL.SDL_PushEvent(ref _userEvent);
        }

        private void DoUpdateDisplayScale()
        {
            _xResolution = (int)(2048.0 * _scaleFactor);
            _yResolution = (int)(2048.0 * _scaleFactor);

            _displayRect = new SDL.SDL_Rect();
            _displayRect.x = 0;
            _displayRect.y = 0;
            _displayRect.h = (int)_yResolution;
            _displayRect.w = (int)_xResolution;

            if (_sdlWindow != null)
            {
                _lock.EnterWriteLock();
                SDL.SDL_SetWindowSize(_sdlWindow, _xResolution, _yResolution);
                SDL.SDL_SetWindowFullscreen(_sdlWindow,
                    _fullScreen ? (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP : 0);

                //
                // Calculate x/y offsets to center display rendering
                ///
                int newWidth = 0;
                int newHeight = 0;
                SDL.SDL_GetWindowSize(_sdlWindow, out newWidth, out newHeight);

                _xOffset = Math.Max(0, (newWidth - _xResolution) / 2);
                _yOffset = Math.Max(0, (newHeight - _yResolution) / 2);

                _displayRect.h = newHeight == 0 ? _yResolution : newHeight;
                _displayRect.w = newWidth == 0 ? _xResolution : newWidth;

                // Clear the display list so no garbage remains.
                _displayListIndex = 0;

                _lock.ExitWriteLock();
            }
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
            if (_throttleFramerate && _frameTimer != null)
            {
                _frameTimer.WaitForFrame();
            }
        }

        private void ShowInternal()
        {
            InitializeSDL();

            // Signal that the display is ready.
            _syncEvent.Set();

            bool quit = false;        
            while (!quit)
            {
                SDL.SDL_Event e;

                //
                // Run main message loop
                //
                while (SDL.SDL_WaitEvent(out e) != 0)
                {
                    switch (e.type)
                    {
                        case SDL.SDL_EventType.SDL_USEREVENT:                            
                            if (e.user.code == (int)UserEventType.Render)
                            {
                                DoRender(_renderCompleteFrame);
                            }
                            else if (e.user.code == (int)UserEventType.Resize)
                            {
                                DoUpdateDisplayScale();
                            }
                            break;

                        case SDL.SDL_EventType.SDL_QUIT:
                            quit = true;
                            return;

                        case SDL.SDL_EventType.SDL_KEYDOWN:
                            SdlKeyDown(e.key.keysym.sym);
                            break;

                        case SDL.SDL_EventType.SDL_KEYUP:
                            SdlKeyUp(e.key.keysym.sym);
                            break;
                    }
                }

                SDL.SDL_Delay(0);
            }

            //
            // Done, clean up.
            //
            SDL.SDL_DestroyRenderer(_sdlRenderer);
            SDL.SDL_DestroyWindow(_sdlWindow);
            SDL.SDL_Quit();
        }

        private void SdlKeyDown(SDL_Keycode key)
        {            
            switch(key)
            {
                case SDL_Keycode.SDLK_LSHIFT:
                case SDL_Keycode.SDLK_RSHIFT:
                    _keyModifiers |= ImlacKeyModifiers.Shift;
                    break;

                case SDL_Keycode.SDLK_LCTRL:
                case SDL_Keycode.SDLK_RCTRL:
                    _keyModifiers |= ImlacKeyModifiers.Ctrl;
                    break;

                case SDL_Keycode.SDLK_LALT:
                case SDL_Keycode.SDLK_RALT:
                    _keyModifiers |= ImlacKeyModifiers.Rept;
                    break;

                default:

                    UpdateDataSwitches(key, true /* key down */);

                    if (key == SDL_Keycode.SDLK_INSERT)
                    {
                        FullScreenToggle();
                    }

                    _keyLatchedLock.EnterWriteLock();

                    _keyLatched = true;
                    _latchedKeyCode = TranslateKeyCode(key);

                    _keyLatchedLock.ExitWriteLock();
                    break;
            }            
        }

        private void SdlKeyUp(SDL.SDL_Keycode key)
        {
            switch (key)
            {
                case SDL_Keycode.SDLK_LSHIFT:
                case SDL_Keycode.SDLK_RSHIFT:
                    _keyModifiers &= ~ImlacKeyModifiers.Shift;
                    break;

                case SDL_Keycode.SDLK_LCTRL:
                case SDL_Keycode.SDLK_RCTRL:
                    _keyModifiers &= ~ImlacKeyModifiers.Ctrl;
                    break;

                case SDL_Keycode.SDLK_LALT:
                case SDL_Keycode.SDLK_RALT:
                    _keyModifiers &= ~ImlacKeyModifiers.Rept;
                    break;

                default:

                    UpdateDataSwitches(key, false /* key down */);

                    _keyLatchedLock.EnterWriteLock();
                    _latchedKeyCode = ImlacKey.Invalid;
                    _keyLatchedLock.ExitWriteLock();
                    break;
            }
        }

        void FullScreenToggle()
        {
            _fullScreen = !_fullScreen;

            UpdateScreenMode();
        }  
        
        public void RenderCurrent(bool completeFrame)
        {
            //
            // Send a render event to the SDL message loop so that things
            // will get rendered.
            //
            _renderCompleteFrame = completeFrame;
            _userEvent.user.code = (int)UserEventType.Render;
            SDL.SDL_PushEvent(ref _userEvent);

            //
            // Wait for rendering to complete before returning.
            //
            WaitForSync();
        }
       
        public void DoRender(bool completeFrame)
        {

            // Draw the current set of vectors
            _lock.EnterReadLock();
            _frame++;

            //
            // If we're drawing a complete frame (not running in debug mode)
            // fade out the last frame by drawing an alpha-blended black rectangle over the display.
            // (slow persistence phosphor simulation!)            
            // Otherwise clear the display completely.
            //
            if (completeFrame)
            {
                SDL.SDL_SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 32);
            }
            else
            {
                SDL.SDL_SetRenderDrawColor(_sdlRenderer, 0, 0, 0, 0xff);
            }

            SDL.SDL_RenderFillRect(_sdlRenderer, ref _displayRect);

            // And draw in this frame's vectors
            for (int i = 0; i < _displayListIndex; i++)
            {                
                _displayList[i].Draw(_sdlRenderer);
            }            
            
            SDL.SDL_RenderPresent(_sdlRenderer);

            if (completeFrame)
            {
                _displayListIndex = 0;
            }

            _lock.ExitReadLock();

            //
            // Indicate that we're through rendering.
            //
            _syncEvent.Set();
        }

        private void AddNewVector(DrawingMode mode, uint startX, uint startY, uint endX, uint endY)
        {
            //
            // Scale the vector to the current scaling factor.
            // The Imlac specifies 11 bits of resolution (2048 points in X and Y)
            // which corresponds to a _scaleFactor of 1.0.
            //
            startX = (uint)(startX * _scaleFactor + _xOffset);
            startY = (uint)(startY *_scaleFactor - _yOffset);
            endX = (uint)(endX * _scaleFactor + _xOffset);
            endY = (uint)(endY * _scaleFactor - _yOffset);

            _lock.EnterWriteLock();

            Vector newVector = _displayList[_displayListIndex];
            newVector.Modify(mode, (short)startX, (short)(_yResolution - startY), (short)endX, (short)(_yResolution - endY));
            _displayListIndex++;

            _lock.ExitWriteLock();
        }

        private void UpdateScreenMode()
        {            
            UpdateDisplayScale();
        }

        private class Vector
        {
            public Vector(DrawingMode mode, uint startX, uint startY, uint endX, uint endY)
            {
                _mode = mode;
                _x1 = (int)startX;
                _y1 = (int)startY;
                _x2 = (int)endX;
                _y2 = (int)endY;

                UpdateColor();
            }

            public void Modify(DrawingMode mode, short startX, short startY, short endX, short endY)
            {
                if (_mode != mode)
                {
                    _mode = mode;
                    UpdateColor();
                }

                _x1 = (int)startX;
                _y1 = (int)startY;
                _x2 = (int)endX;
                _y2 = (int)endY;
            }

            public void Draw(IntPtr sdlRenderer)
            {
                // TODO: handle dotted lines, line thickness options    
                SDL.SDL_SetRenderDrawColor(sdlRenderer, _color.R, _color.G, _color.B, _color.A);
                SDL.SDL_RenderDrawLine(sdlRenderer, _x1, _y1, _x2, _y2);                
            }

            private void UpdateColor()
            {
                switch (_mode)
                {
                    case DrawingMode.Normal:
                        _color = NormalColor;
                        break;

                    case DrawingMode.Point:
                    case DrawingMode.Dotted:
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
            private Color _color;
            private int _x1;
            private int _y1;
            private int _x2;
            private int _y2;

            private static Color NormalColor = Color.FromArgb(48, Color.ForestGreen);
            private static Color PointColor = Color.FromArgb(255, Color.ForestGreen);
            private static Color SGRColor = Color.FromArgb(128, Color.ForestGreen);
            private static Color DebugColor = Color.FromArgb(255, Color.OrangeRed);
        }
        
        private static void BuildKeyMappings()
        {
            _sdlImlacKeymap = new Dictionary<SDL_Keycode, ImlacKey>();

            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_END, ImlacKey.DataXmit);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_DOWN, ImlacKey.Down);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_RIGHT, ImlacKey.Right);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_UP, ImlacKey.Up);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_LEFT, ImlacKey.Left);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_TAB, ImlacKey.Tab);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_RETURN, ImlacKey.CR);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_PAGEUP, ImlacKey.FF);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_RIGHTBRACKET, ImlacKey.LF);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_PAGEDOWN, ImlacKey.PageXmit);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_HOME, ImlacKey.Home);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_PAUSE, ImlacKey.Brk);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_ESCAPE, ImlacKey.Esc);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_SPACE, ImlacKey.Space);

            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_COMMA, ImlacKey.Comma);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_EQUALS, ImlacKey.Minus);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_PERIOD, ImlacKey.Period);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_SLASH, ImlacKey.Slash);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_0, ImlacKey.K0);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_1, ImlacKey.K1);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_2, ImlacKey.K2);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_3, ImlacKey.K3);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_4, ImlacKey.K4);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_5, ImlacKey.K5);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_6, ImlacKey.K6);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_7, ImlacKey.K7);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_8, ImlacKey.K8);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_9, ImlacKey.K9);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_MINUS, ImlacKey.Colon);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_SEMICOLON, ImlacKey.Semicolon);

            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_KP_0, ImlacKey.D0);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_KP_2, ImlacKey.D2);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_KP_4, ImlacKey.D4);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_KP_5, ImlacKey.D5);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_KP_6, ImlacKey.D6);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_QUOTE, ImlacKey.Unlabeled);

            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_a, ImlacKey.A);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_b, ImlacKey.B);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_c, ImlacKey.C);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_d, ImlacKey.D);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_e, ImlacKey.E);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_f, ImlacKey.F);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_g, ImlacKey.G);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_h, ImlacKey.H);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_i, ImlacKey.I);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_j, ImlacKey.J);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_k, ImlacKey.K);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_l, ImlacKey.L);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_m, ImlacKey.M);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_n, ImlacKey.N);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_o, ImlacKey.O);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_p, ImlacKey.P);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_q, ImlacKey.Q);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_r, ImlacKey.R);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_s, ImlacKey.S);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_t, ImlacKey.T);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_u, ImlacKey.U);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_v, ImlacKey.V);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_w, ImlacKey.W);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_x, ImlacKey.X);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_y, ImlacKey.Y);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_z, ImlacKey.Z);

            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_DELETE, ImlacKey.Del);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_BACKSPACE, ImlacKey.Del);
            _sdlImlacKeymap.Add(SDL_Keycode.SDLK_BACKSLASH, ImlacKey.Brk);

            _sdlVKeymap = new Dictionary<SDL_Keycode, VKeys>();

            _sdlVKeymap.Add(SDL_Keycode.SDLK_END, VKeys.End);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_DOWN, VKeys.DownArrow);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_RIGHT, VKeys.RightArrow);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_UP, VKeys.UpArrow);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_LEFT, VKeys.LeftArrow);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_TAB, VKeys.Tab);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_RETURN, VKeys.Return);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_PAGEUP, VKeys.PageUp);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_PAGEDOWN, VKeys.PageDown);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_HOME, VKeys.Home);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_PAUSE, VKeys.Pause);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_ESCAPE, VKeys.Escape);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_SPACE, VKeys.Space);

            _sdlVKeymap.Add(SDL_Keycode.SDLK_COMMA, VKeys.Comma);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_PLUS, VKeys.Plus);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_PERIOD, VKeys.Period);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_QUESTION, VKeys.QuestionMark);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_0,VKeys.Zero);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_1,VKeys.One);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_2,VKeys.Two);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_3,VKeys.Three);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_4,VKeys.Four);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_5,VKeys.Five);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_6,VKeys.Six);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_7,VKeys.Seven);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_8,VKeys.Eight);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_9,VKeys.Nine);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_MINUS, VKeys.Minus);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_SEMICOLON, VKeys.Semicolon);

            _sdlVKeymap.Add(SDL_Keycode.SDLK_KP_0, VKeys.Keypad0);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_KP_2, VKeys.Keypad2);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_KP_4, VKeys.Keypad4);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_KP_5, VKeys.Keypad5);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_KP_6, VKeys.Keypad6);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_QUOTEDBL, VKeys.DoubleQuote);

            _sdlVKeymap.Add(SDL_Keycode.SDLK_a,VKeys.A);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_b,VKeys.B);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_c,VKeys.C);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_d,VKeys.D);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_e,VKeys.E);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_f,VKeys.F);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_g,VKeys.G);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_h,VKeys.H);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_i,VKeys.I);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_j,VKeys.J);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_k,VKeys.K);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_l,VKeys.L);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_m,VKeys.M);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_n,VKeys.N);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_o,VKeys.O);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_p,VKeys.P);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_q,VKeys.Q);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_r,VKeys.R);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_s,VKeys.S);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_t,VKeys.T);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_u,VKeys.U);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_v,VKeys.V);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_w,VKeys.W);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_x,VKeys.X);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_y,VKeys.Y);
            _sdlVKeymap.Add(SDL_Keycode.SDLK_z,VKeys.Z);

            _sdlVKeymap.Add(SDL_Keycode.SDLK_DELETE, VKeys.Delete);
        }

        private ImlacKey TranslateKeyCode(SDL_Keycode virtualKey)
        {
            if (_sdlImlacKeymap.ContainsKey(virtualKey))
            {
                return _sdlImlacKeymap[virtualKey];
            }
            else
            {
                return ImlacKey.Invalid;
            }
        }

        private void UpdateDataSwitches(SDL_Keycode sdlKey, bool keyDown)
        {
            if (_sdlVKeymap.ContainsKey(sdlKey))
            {

                VKeys virtualKey = _sdlVKeymap[sdlKey];

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
        }

        private static Dictionary<SDL_Keycode, ImlacKey> _sdlImlacKeymap;
        private static Dictionary<SDL_Keycode, VKeys> _sdlVKeymap;
        private ImlacKey _latchedKeyCode;
        private ImlacKeyModifiers _keyModifiers;
        private bool _keyLatched;

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

        //
        // SDL
        //
        private IntPtr _sdlWindow = IntPtr.Zero;
        private IntPtr _sdlRenderer = IntPtr.Zero;
        private SDL.SDL_Rect _displayRect;

        // 
        // SDL User events
        //
        private UInt32 _userEventType;
        private SDL.SDL_Event _userEvent;        

        private enum UserEventType
        {
            Render = 0,
            Resize
        }

        private AutoResetEvent _syncEvent;
        
        private uint _x;
        private uint _y;

        private int _xResolution;
        private int _yResolution;
        private float _scaleFactor;
        private int _xOffset;
        private int _yOffset;
        private bool _renderCompleteFrame;

        private bool _fullScreen;
        private bool _throttleFramerate;
        private bool _dataSwitchMappingEnabled;

        private int _displayListIndex;
        private List<Vector> _displayList;
        private const int _displayListSize = 100000;        // Considerably more than a real Imlac could ever hope to draw in a single frame.

        private System.Threading.ReaderWriterLockSlim _lock;        

        private uint _frame;

        // Framerate management
        FrameTimer _frameTimer;
        
    }
}
