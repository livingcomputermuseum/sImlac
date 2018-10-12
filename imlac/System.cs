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

using imlac.IO;
using imlac.Debugger;
using System.IO.Ports;
using System.IO;
using imlac.IO.TTYChannels;

namespace imlac
{
    public enum SystemExecutionState
    {
        Debugging,
        Halted,
        SingleStep,
        SingleFrame,
        UntilDisplayStart,
        Running,
        Quit
    }

    public class ImlacSystem
    {
        public ImlacSystem()
        {
            _memory = new Memory(this);
            _paperTapeReader = new PaperTapeReader(this);
            _tty = new TTY(this);
            _keyboard = new Keyboard(this);
            _clock = new AddressableClock(this);
            _interruptFacility = new InterruptFacility(this);
            _displayProcessor = new DisplayProcessor(this);
            _processor = new Processor(this);

            // Register IOT devices
            _processor.RegisterDeviceIOTs(_displayProcessor);
            _processor.RegisterDeviceIOTs(_paperTapeReader);
            _processor.RegisterDeviceIOTs(_tty);
            _processor.RegisterDeviceIOTs(_keyboard);
            _processor.RegisterDeviceIOTs(_clock);
            _processor.RegisterDeviceIOTs(_interruptFacility);
        }

        public void Reset()
        {
            _paperTapeReader.Reset();
            _tty.Reset();
            _keyboard.Reset();
            _clock.Reset();
            _interruptFacility.Reset();
            _displayProcessor.Reset();
            _processor.Reset();
        }

        public void Shutdown()
        {
            _display.Shutdown();
        }

        public void AttachConsole(IImlacConsole console)
        {
            _display = console;
        }

        public Memory Memory
        {
            get { return _memory; }
        }

        public Processor Processor
        {
            get { return _processor; }
        }

        public DisplayProcessor DisplayProcessor
        {
            get { return _displayProcessor; }
        }

        public IImlacConsole Display
        {
            get { return _display; }
        }

        public PaperTapeReader PaperTapeReader
        {
            get { return _paperTapeReader; }
        }

        public TTY TTY
        {
            get { return _tty; }
        }

        public Keyboard Keyboard
        {
            get { return _keyboard; }
        }

        public AddressableClock Clock
        {
            get { return _clock; }
        }

        public InterruptFacility InterruptFacility
        {
            get { return _interruptFacility; }
        }

        public void SingleStep()
        {
            _processor.Clock();
            _displayProcessor.Clock();
            _paperTapeReader.Clock();
            _tty.Clock();
            _keyboard.Clock();
            _clock.Clock();

            // interrupts last so that devices that raise interrupts get clocked first
            _interruptFacility.Clock();
        }

        //
        // Debugger commands follow
        //
        [DebuggerFunction("reset", "Resets the Imlac system (does not clear memory)")]
        private SystemExecutionState ResetSystem()
        {
            Reset();
            Console.WriteLine("System reset.");
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("edit memory", "Provides a simple memory editor", "<address>")]
        private SystemExecutionState EditMemory(ushort address)
        {
            MemoryOperation(address);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("go", "Begins execution at the specified address", "<address>")]
        private SystemExecutionState Go(ushort address)
        {
            Processor.PC = address;
            Processor.State = ProcessorState.Running;
            return SystemExecutionState.Running;
        }

        [DebuggerFunction("go", "Begins execution at the current PC")]
        private SystemExecutionState Go()
        {
            Processor.State = ProcessorState.Running;
            return SystemExecutionState.Running;
        }

        [DebuggerFunction("set pc", "Sets the Processor's PC to the specified address", "<address>")]
        private SystemExecutionState SetPC(ushort address)
        {
            Processor.PC = address;
            return SystemExecutionState.SingleStep;
        }

        [DebuggerFunction("step", "Executes a single instruction cycle at the current PC")]
        private SystemExecutionState StepProcessor()
        {
            Processor.State = ProcessorState.Running;
            return SystemExecutionState.SingleStep;
        }

        [DebuggerFunction("step frame end", "Runs until the end of the current frame")]
        private SystemExecutionState RunFrameEnd()
        {
            Processor.State = ProcessorState.Running;
            return SystemExecutionState.SingleFrame;
        }

        [DebuggerFunction("step frame start", "Runs until the start of the next frame")]
        private SystemExecutionState RunFrameStart()
        {
            Processor.State = ProcessorState.Running;
            return SystemExecutionState.UntilDisplayStart;
        }

        [DebuggerFunction("set bootstrap", "Loads the specified bootstrap into memory at 40", "<bootstrap>")]
        private SystemExecutionState SetBootstrap(string bootstrap)
        {
            LoadMemory(Paths.BuildBootPath(bootstrap), 0x20, 0x20);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("save memory", "Saves the specified range of memory to a file", "<file> <start> <length>")]
        private SystemExecutionState SaveMemoryContents(string file, ushort start, ushort length)
        {
            SaveMemory(file, start, length);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("load memory", "Loads the specified range of memory from a file", "<file> <start> <length>")]
        private SystemExecutionState LoadMemoryContens(string file, ushort start, ushort length)
        {
            LoadMemory(file, start, length);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("disassemble", "Disassembles the specified range of memory", "<mode> <start> <length>")]
        private SystemExecutionState DisassembleProcessor(DisassemblyMode mode, ushort start, ushort length)
        {
            Disassemble(mode, start, length);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("disassemble", "Disassembles the specified range of memory", "<start> <length>")]
        private SystemExecutionState DisassembleProcessor(ushort start, ushort length)
        {
            Disassemble(DisassemblyMode.Processor, start, length);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set data switch register", "Sets the data switch register to the specified value", "<value>")]
        private SystemExecutionState SetDataSwitchRegister(ushort value)
        {
            Processor.DS = value;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("show data switch register", "Displays the data switch register")]
        private SystemExecutionState ShowDataSwitchRegister()
        {
            Console.WriteLine(Helpers.ToOctal(Processor.DS));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("show memory", "Displays memory contents", "<start> <length>")]
        private SystemExecutionState DisplayMemory(ushort start, ushort length)
        {
            DumpMemory(start, length);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set memory size 4kw", "Sets memory size to 4KW")]
        private SystemExecutionState SetMemorySize4kw()
        {
            _memory.SetMemorySize(0x1000);
            return SystemExecutionState.Debugging;
        }


        [DebuggerFunction("set memory size 8kw", "Sets memory size to 8KW")]
        private SystemExecutionState SetMemorySize8kw()
        {
            _memory.SetMemorySize(0x2000);
            return SystemExecutionState.Debugging;
        }


        [DebuggerFunction("set memory size 16kw", "Sets memory size to 16KW")]
        private SystemExecutionState SetMemorySize16kw()
        {
            _memory.SetMemorySize(0x4000);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set logging", "Sets the logging configuration", "<loglevel>")]
        private SystemExecutionState SetLogging(LogType value)
        {
            Trace.TraceLevel = value;
            Trace.TraceOn = value != 0;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("show logging", "Shows the logging configuration")]
        private SystemExecutionState ShowLogging()
        {
            Console.WriteLine("{0}", Trace.TraceLevel);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("attach tty port", "Attaches the Imlac's TTY to a physical serial port", "<port> <rate> <parity> <databits> <stopbits>")]
        private SystemExecutionState AttachTTY(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            SerialPort ttyPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
            ttyPort.Open();
            TTY.SetChannel(new SerialDataChannel(ttyPort));

            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("attach tty file", "Attaches the Imlac's TTY input to a data file", "<file>")]
        private SystemExecutionState AttachTTY(string fileName)
        {
            FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            StreamDataChannel channel = new StreamDataChannel(fileStream);
            TTY.SetChannel(channel);

            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("attach tty telnet", "Attaches the Imlac's TTY to a raw telnet port", "<host> <port>")]
        private SystemExecutionState AttachTTY(string host, ushort port)
        {
            TTY.SetChannel(new TelnetDataChannel(host, port));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("detach tty", "Detaches the Imlac's TTY from host inputs")]
        private SystemExecutionState DetachTTY()
        {
            TTY.SetChannel(new NullDataChannel());
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("attach ptr file", "Attaches the Imlac's PTR input to a data file", "<file>")]
        private SystemExecutionState AttachPTR(string fileName)
        {
            PaperTapeReader.LoadTape(fileName);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set display scale", "Sets the scaling factor for the Imlac display", "<scaleFactor>")]
        private SystemExecutionState SetDisplayScale(float scale)
        {
            Display.SetScale(scale);
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set display mode fullscreen", "Sets the Imlac display to fullscreen")]
        private SystemExecutionState SetDisplayModeFullscreen()
        {
            Display.FullScreen = true;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set display mode windowed", "Sets the Imlac display to windowed")]
        private SystemExecutionState SetDisplayModeWindowed()
        {
            Display.FullScreen = false;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set framerate throttle", "Enables or disables framerate throttling to 40Hz", "<throttle>")]
        private SystemExecutionState SetThrottleFramerate(bool throttle)
        {
            Display.ThrottleFramerate = throttle;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set breakpoint execution", "Sets an execution breakpoint at the specified location", "<address>")]
        private SystemExecutionState SetBreakpointExecution(ushort address)
        {
            BreakpointManager.SetBreakpoint(new BreakpointEntry(BreakpointType.Execution, address));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set breakpoint read", "Sets a read breakpoint at the specified location", "<address>")]
        private SystemExecutionState SetBreakpointRead(ushort address)
        {
            BreakpointManager.SetBreakpoint(new BreakpointEntry(BreakpointType.Read, address));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set breakpoint write", "Sets a write breakpoint at the specified location", "<address>")]
        private SystemExecutionState SetBreakpointWrite(ushort address)
        {
            BreakpointManager.SetBreakpoint(new BreakpointEntry(BreakpointType.Write, address));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set breakpoint display", "Sets a display breakpoint at the specified location", "<address>")]
        private SystemExecutionState SetBreakpointDisplay(ushort address)
        {
            BreakpointManager.SetBreakpoint(new BreakpointEntry(BreakpointType.Display, address));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("clear breakpoint", "Clears the breakpoint at the specified location", "<address>")]
        private SystemExecutionState ClearBreakpoint(ushort address)
        {
            BreakpointManager.SetBreakpoint(new BreakpointEntry(BreakpointType.None, address));
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("enable breakpoints", "Enables debugger breakpoints")]
        private SystemExecutionState EnableBreakpoints()
        {
            BreakpointManager.BreakpointsEnabled = true;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("disable breakpoints", "Disables debugger breakpoints")]
        private SystemExecutionState DisableBreakpoints()
        {
            BreakpointManager.BreakpointsEnabled = false;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("show breakpoints", "Displays defined breakpoints.")]
        private SystemExecutionState ShowBreakpoints()
        {
            bool set = false;
            foreach (BreakpointEntry e in BreakpointManager.EnumerateBreakpoints())
            {
                set = true;
                Console.WriteLine("Address {0}, break on {1}", Helpers.ToOctal(e.Address), e.Type);
            }

            if (!set)
            {
                Console.WriteLine("No breakpoints are currently defined.");
            }

            Console.WriteLine("\nBreakpoints are {0} globally.", BreakpointManager.BreakpointsEnabled ? "enabled" : "disabled");

            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("enable data switch mappings", "Enables mapping of keyboard keys to Data Switches")]
        private SystemExecutionState EnableDataSwitchMappings()
        {
            Display.DataSwitchMappingEnabled = true;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("disable data switch mappings", "disable mapping of keyboard keys to Data Switches")]
        private SystemExecutionState DisableDataSwitchMappings()
        {
            Display.DataSwitchMappingEnabled = false;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set data switch mapping", "Maps a Data Switch to a key", "<switch number> <key>")]
        private SystemExecutionState SetDataSwitchMapping(uint switchNumber, VKeys key)
        {
            if (switchNumber > 15)
            {
                Console.WriteLine("Invalid value for data switch number");
            }
            else
            {
                Display.MapDataSwitch(switchNumber, key);
            }
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("show data switch mapping", "Shows data switch key mapping for specified switch number", "<switch number>")]
        private SystemExecutionState ShowDataSwitchMapping(uint switchNumber)
        {
            if (switchNumber > 15)
            {
                Console.WriteLine("Invalid value for data switch number");
            }
            else
            {
                Console.WriteLine(Display.GetDataSwitchMapping(switchNumber));
            }
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("set data switch mode", "Sets the mode for keyboard->data switch mapping", "<mode>")]
        private SystemExecutionState SetDataSwitchMode(DataSwitchMappingMode mode)
        {
            Display.DataSwitchMode = mode;
            return SystemExecutionState.Debugging;
        }

        [DebuggerFunction("show data switch mode", "Displays the mode for keyboard->data switch mapping")]
        private SystemExecutionState ShowDataSwitchMode()
        {
            Console.WriteLine(Display.DataSwitchMode);
            return SystemExecutionState.Debugging;
        }

        public enum DisassemblyMode
        {
            Processor,
            DisplayProcessor,
            DisplayIncrement,
            DisplayAuto
        }
        
        public void PrintProcessorStatus()
        {
            Console.WriteLine("PC={0} AC={1} MB={2} - {3}\n{4}",
                Helpers.ToOctal(Processor.PC),
                Helpers.ToOctal(Processor.AC),
                Helpers.ToOctal(Memory.Fetch(Processor.PC)),
                Processor.State,
                Processor.Disassemble(Processor.PC));

            Console.WriteLine("DPC={0} DT={1} DPCE={2} X={3} Y={4}\nMode={5} HalfWord={6}",
                Helpers.ToOctal(DisplayProcessor.PC),
                Helpers.ToOctal(DisplayProcessor.DT),
                Helpers.ToOctal(DisplayProcessor.DPCEntry),
                DisplayProcessor.X,
                DisplayProcessor.Y,
                DisplayProcessor.Mode,
                DisplayProcessor.Half,
                DisplayProcessor.State);
        }

        private void MemoryOperation(ushort address)
        {
            bool done = false;

            while (!done)
            {
                try
                {
                    Console.Write("{0}\\{1} ", Helpers.ToOctal(address), Helpers.ToOctal(Memory.Fetch(address)));
                    string dataString = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(dataString))
                    {
                        ushort value = Helpers.GetUshortForOctalString(dataString);
                        Memory.Store(address, value);
                    }

                    address++;
                }
                catch
                {
                    done = true;
                }
            }
        }

        private void DumpMemory(ushort startAddress, int count)
        {
            while (count > 0)
            {
                Console.Write("{0}: ", Helpers.ToOctal(startAddress));
                for (ushort address = startAddress; address < startAddress + 4; address++)
                {
                    Console.Write("{0} ", Helpers.ToOctal(Memory.Fetch(address)));
                }

                for (ushort address = startAddress; address < startAddress + 4; address++)
                {
                    ushort word = Memory.Fetch(address);
                    Console.Write("{0}{1} ", GetPrintableChar((char)(word >> 8)), GetPrintableChar((char)(word & 0xff)));
                }

                Console.WriteLine();

                startAddress += 4;
                count -= 4;
            }
        }

        private static char GetPrintableChar(char c)
        {
            if (char.IsLetterOrDigit(c) ||
                char.IsPunctuation(c) ||
                char.IsSymbol(c))
            {
                return c;
            }
            else
            {
                return '.';
            }
        }

        private void Disassemble(DisassemblyMode mode, ushort startAddress, ushort length)
        {
            if (startAddress > Memory.Size)
            {
                throw new InvalidOperationException(String.Format("Start address must be less than the size of system memory ({0}).", Helpers.ToOctal(Memory.Size)));
            }

            ushort endAddress = (ushort)Math.Min(Memory.Size - 1, startAddress + length);

            for (ushort address = startAddress; address < endAddress; address++)
            {
                string disassembly = string.Empty;
                try
                {
                    switch (mode)
                    {
                        case DisassemblyMode.Processor:
                            disassembly = Processor.Disassemble(address);
                            break;

                        case DisassemblyMode.DisplayAuto:
                            disassembly = DisplayProcessor.Disassemble(address, DisplayProcessorMode.Indeterminate);
                            break;

                        case DisassemblyMode.DisplayProcessor:
                            disassembly = DisplayProcessor.Disassemble(address, DisplayProcessorMode.Processor);
                            break;

                        case DisassemblyMode.DisplayIncrement:
                            disassembly = DisplayProcessor.Disassemble(address, DisplayProcessorMode.Increment);
                            break;
                    }
                }
                catch
                {
                    // this can happen if the data is not a valid instruction
                    disassembly = "<invalid instruction>";
                }

                Console.WriteLine("{0}\\{1} {2}", Helpers.ToOctal(address), Helpers.ToOctal(Memory.Fetch(address)), disassembly);
            }
        }

        private void SaveMemory(string path, ushort startAddress, ushort length)
        {
            if (startAddress > Memory.Size)
            {
                throw new InvalidOperationException(String.Format("Start address must be less than the size of system memory ({0}).", Helpers.ToOctal(Memory.Size)));
            }

            // clip end to top of memory
            ushort endAddress = (ushort)Math.Min(Memory.Size, startAddress + length);

            FileStream fs = File.OpenWrite(path);

            for (ushort i = startAddress; i < endAddress; i++)
            {
                ushort value = Memory.Fetch(i);

                fs.WriteByte((byte)(value >> 8));
                fs.WriteByte((byte)(value & 0xff));
            }

            fs.Close();
        }

        private void LoadMemory(string path, ushort startAddress, ushort length)
        {
            if (startAddress > Memory.Size)
            {
                throw new InvalidOperationException(String.Format("Start address must be less than the size of system memory ({0}).", Helpers.ToOctal(Memory.Size)));
            }

            // clip end to top of memory
            ushort endAddress = (ushort)Math.Min(Memory.Size, startAddress + length);

            FileStream fs = File.OpenRead(path);

            for (ushort i = startAddress; i < endAddress; i++)
            {
                ushort value = (ushort)((fs.ReadByte() << 8) | (fs.ReadByte()));
                Memory.Store(i, value);
            }

            fs.Close();
        }

        private Processor _processor;
        private DisplayProcessor _displayProcessor;
        private IImlacConsole _display;
        private Memory _memory;
        private PaperTapeReader _paperTapeReader;
        private TTY _tty;
        private Keyboard _keyboard;
        private InterruptFacility _interruptFacility;
        private AddressableClock _clock;
    }
}
