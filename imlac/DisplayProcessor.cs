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

using imlac.IO;
using imlac.Debugger;

namespace imlac
{
    public enum DisplayProcessorMode
    {
        Indeterminate,
        Processor,
        Increment,
        MediumVector,
        CompactAddressing,
    }   

    public enum ImmediateHalf
    {
        First,
        Second
    }

    /// <summary>
    /// DisplayProcessorBase is an abstract class providing the logic sharable between the
    /// PDS-1 and PDS-4's display processors.
    /// </summary>
    public abstract class DisplayProcessorBase : IIOTDevice
    {
        public DisplayProcessorBase(ImlacSystem system)
        {
            _system = system;
            _mem = _system.Memory;
            _dtStack = new Stack<ushort>(8);
            InitializeCache();
        }

        public virtual void Reset()
        {
            _state = ProcessorState.Halted;
            _halted = true;
            _mode = DisplayProcessorMode.Processor;
            _immediateHalf = ImmediateHalf.First;
            _immediateWord = 0;
            _pc = 0;
            _dpcEntry = 0;
            _block = 0;
            _dtStack.Clear();
            _x = 0;
            _y = 0;
            _scale = 1.0f;

            _dadr = false;
            
            _system.Display.MoveAbsolute(0, 0, DrawingMode.Off);

            _clocks = 0;
            _frameLatch = false;
        }

        public ushort PC
        {
            get { return _pc; }
            set 
            { 
                _pc = value;

                // block is set whenever DPC is set by the main processor
                // only on non-MIT modded systems
                if (!Configuration.MITMode)
                {
                    _block = (ushort)(value & 0x3000);
                }

                if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DPC set to {0} (block {1})", Helpers.ToOctal(_pc), Helpers.ToOctal(_block));
            }
        }

        public ProcessorState State
        {
            get { return _state; }
            set 
            { 
                _state = value;

                if (_state == ProcessorState.Halted)
                {
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Display processor halted.");
                }
                else
                {
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Display processor started.");
                }
            }
        }

        public bool DisplayHalted
        {
            get { return _halted; }
        }

        public DisplayProcessorMode Mode
        {
            get { return _mode; }
        }

        public ImmediateHalf Half
        {
            get { return _immediateHalf; }
        }

        public ushort DT
        {
            get 
            {
                if (_dtStack.Count > 0)
                {
                    return _dtStack.Peek();
                }
                else
                {
                    return 0;
                }
            }
        }

        public bool FrameLatch
        {
            get { return _frameLatch; }
        }

        public int X
        {
            get { return _x; }
            set 
            { 
                _x = value & 0x7ff;
            }
        }

        public int Y
        {
            get { return _y; }
            set 
            { 
                _y = value & 0x7ff;
            }
        }

        public ushort DPCEntry
        {
            get { return _dpcEntry; }
        }

        //
        // Push and Pop are used on the PDS-4 to allow the main processor control of the 
        // DPC stack.  (Called the MDS on the PDS-4)
        //
        public void Push()
        {
            _dtStack.Push((ushort)(_pc + 1));
            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DT stack push {0}, depth is now {1}", Helpers.ToOctal((ushort)(_pc + 1)), _dtStack.Count);
        }

        public void Pop()
        {
            if (_dtStack.Count > 0)
            {
                _pc = _dtStack.Pop();
                if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DT stack pop {0}, depth is now {1}", Helpers.ToOctal(_pc), _dtStack.Count);
            }
            else
            {
                if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DT stack empty on pop!  Leaving DPC undisturbed at {0}", Helpers.ToOctal(_pc));
            }
        }

        public virtual void StartProcessor()
        {
            State = ProcessorState.Running;
            // MIT DADR bit gets reset when display is started.
            _dadr = false;
            _halted = false;
        }

        public virtual void HaltProcessor()
        {
            State = ProcessorState.Halted;
            _halted = true;
        }

        public abstract void InitializeCache();

        public abstract void InvalidateCache(ushort address);

        public abstract string Disassemble(ushort address, DisplayProcessorMode mode, out int length);

        public abstract void Clock();

        public abstract int[] GetHandledIOTs();

        public abstract void ExecuteIOT(int iotCode);

        protected int _x;
        protected int _y;
        protected float _scale;
        protected ushort _pc;
        protected ushort _block;
        protected Stack<ushort> _dtStack;
        protected ushort _dpcEntry;

        // MIT DADR (display addressing) flag.
        protected bool _dadr;

        protected ushort _immediateWord;
        protected ImmediateHalf _immediateHalf;

        protected int _clocks;        
        protected bool _frameLatch;

        protected ProcessorState _state;
        protected bool _halted;                           // The halted flag is set to indicate that the processor
                                                          // has previously halted itself and is independent of the 
                                                          // current state of the processor.
        protected DisplayProcessorMode _mode;
        protected ImlacSystem _system;
        protected Memory _mem;

        /// <summary>
        /// All display Opcode mnemonics, shared across implementations
        /// of DisplayInstructionBase, unfortunately.
        /// </summary>
        protected enum DisplayOpcode
        {
            // Basic instructions
            DLXA,       // Load X Accumulator
            DLYA,       // Load Y Accumulator
            DEIM,       // Enter Immediate Mode
            DJMS,       // Jump to subroutine
            DJMP,       // Jump to address
            DHLT,       // Halt display
            DNOP,       // No op
            DSTS,       // Set scale
            DSTB,       // Set block
            DDSP,       // Display intensification
            DIXM,       // Display increment X MSB
            DIYM,       // Display increment Y MSB
            DDXM,       // Display decrement X MSB
            DDYM,       // Display decrement Y MSB
            DRJM,       // Return jump
            DHVC,       // Display HV Sync    
            DLVH,       // Long vector
            DOPR,       // Generic Display OPR microinstruction

            // Optional extended instructions
            SGR1,
            ASG1,
            VIC1,
            MCI1,
            STI1,
            LPA1,

            // PDS-4 only instructions
            DMVM,       // Medium Vector Mode
            DCAM,       // Compact Addressing Mode
            DBLI,       // Blinking
            DFXY,       // Fast X/Y Mode
            DVIC,       // Variable Intensity
            DASG,       // Automatic Increment and Intensify
            DROR,       // Character Rotation/Reflection
            DARX,       // Add Relative X
            DARY,       // Add Relative Y
        }

        protected abstract class DisplayInstructionBase
        {
            public DisplayInstructionBase(ushort word, ushort address, DisplayProcessorMode mode)
            {
                _usageMode = mode;
                _word = word;
                _address = address;
                Decode();
            }

            public DisplayOpcode Opcode
            {
                get { return _opcode; }
            }

            public ushort Data
            {
                get { return _data; }
            }

            /// <summary>
            /// Set when the instruction is actually executed by the display processor.
            /// Used to aid in disassembly (since it provides the context needed to determine what type of 
            /// processor instruction it is)
            /// </summary>
            public DisplayProcessorMode UsageMode
            {
                get { return _usageMode; }
                set { _usageMode = value; }
            }

            /// <summary>
            /// Implementors provide a disassembly string representing this instruction.
            /// </summary>
            /// <param name="mode"></param>
            /// <returns></returns>
            public abstract string Disassemble(DisplayProcessorMode mode, Memory mem, out int length);

            /// <summary>
            /// Implemented to provide decoding of this instruction word.
            /// </summary>
            protected abstract void Decode();

            protected string DisassembleIncrement()
            {
                return DisassembleIncrementHalf(ImmediateHalf.First) + " | " + DisassembleIncrementHalf(ImmediateHalf.Second);
            }

            private string DisassembleIncrementHalf(ImmediateHalf half)
            {
                string ret = string.Empty;
                int halfWord = half == ImmediateHalf.First ? (_word & 0xff00) >> 8 : (_word & 0xff);

                // translate the half word to vector movements or escapes
                // special case for "Enter Immediate mode" halfword (030) in first half.
                if (half == ImmediateHalf.First && halfWord == 0x30)
                {
                    ret += "E";
                }
                else if ((halfWord & 0x80) == 0)
                {
                    if ((halfWord & 0x10) != 0)
                    {
                        ret += "IX ";
                    }

                    if ((halfWord & 0x08) != 0)
                    {
                        ret += "ZX ";
                    }

                    if (half == ImmediateHalf.Second &&
                        (halfWord & 0x04) != 0)
                    {
                        ret += "E PPM ";
                    }

                    if ((halfWord & 0x02) != 0)
                    {
                        ret += "IY ";
                    }

                    if ((halfWord & 0x01) != 0)
                    {
                        ret += "ZY ";
                    }

                    if ((halfWord & 0x40) != 0)
                    {
                        if ((halfWord & 0x20) != 0)
                        {
                            // escape and return
                            ret += "F RJM";
                        }
                        else
                        {
                            // Escape
                            ret += "F";
                        }
                    }
                }
                else
                {
                    int xSign = ((halfWord & 0x20) == 0) ? 1 : -1;
                    int xMag = (int)(((halfWord & 0x18) >> 3));

                    int ySign = (int)(((halfWord & 0x04) == 0) ? 1 : -1);
                    int yMag = (int)((halfWord & 0x03));

                    ret += String.Format("{0},{1} {2}", xMag * xSign, yMag * ySign, (halfWord & 0x40) == 0 ? "OFF" : "ON");
                }

                return ret;
            }

            protected string DisassembleProcessor(Memory mem, out int length)
            {
                length = 1;

                string ret = String.Empty;
                if (_opcode == DisplayOpcode.DOPR)
                {
                    string[] codes = { "INV0 ", "INV1 ", "INV2 ", "INV3 ", "DDSP ", "DRJM ", "DDYM ", "DDXM ", "DIYM ", "DIXM ", "DHVC ", "DHLT " };

                    for (int i = 4; i < 11; i++)
                    {
                        if ((_data & (0x01) << i) != 0)
                        {
                            if (!string.IsNullOrEmpty(ret))
                            {
                                ret += ",";
                            }

                            ret += codes[i];
                        }
                    }

                    // display halt if bit 4 is unset
                    if ((_data & 0x800) == 0)
                    {
                        ret += " DHLT ";
                    }

                    // F/C ops:
                    int f = (_data & 0xc) >> 2;
                    int c = _data & 0x3;

                    switch (f)
                    {
                        case 0x0:
                            // nothing
                            if (c == 1)
                            {
                                ret += String.Format("DADR");
                            }
                            break;

                        case 0x1:
                            ret += String.Format("DSTS {0}", c);
                            break;

                        case 0x2:
                            ret += String.Format("DSTB {0}", c);
                            break;

                        case 0x3:
                            ret += String.Format("DLPN {0}", c);
                            break;
                    }
                }
                else
                {
                    switch (_opcode)
                    {
                        case DisplayOpcode.DEIM:
                            ret = String.Format("DEIM | {0} {1}",
                                DisassembleIncrementHalf(ImmediateHalf.Second),
                                (_word & 0xff00) == 0x3800 ? "Enter PPM" : String.Empty);
                            break;

                        case DisplayOpcode.DLXA:
                            ret = String.Format("DLXA {0} ({1})", Helpers.ToOctal(_data), _data);
                            break;

                        case DisplayOpcode.DLYA:
                            ret = String.Format("DLXA {0} ({1})", Helpers.ToOctal(_data), _data);
                            break;

                        case DisplayOpcode.DJMS:
                            ret = String.Format("DJMS {0}", Helpers.ToOctal(_data));
                            break;

                        case DisplayOpcode.DJMP:
                            ret = String.Format("DJMP {0}", Helpers.ToOctal(_data));
                            break;

                        default:
                            ret = DisassembleExtended(mem, out length);
                            break;
                    }
                }

                return ret;
            }

            protected abstract string DisassembleExtended(Memory mem, out int length);

            protected DisplayOpcode _opcode;
            protected ushort _data;
            protected DisplayProcessorMode _usageMode;
            protected ushort _word;
            protected ushort _address;
        }

    }
}
