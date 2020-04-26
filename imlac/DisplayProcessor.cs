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
            State = ProcessorState.Halted;
            _halted = false;
            _mode = DisplayProcessorMode.Processor;
            _pc = 0;
            _block = 0;
            _dtStack.Clear();
            X = 0;
            Y = 0;
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

        public bool IsHalted
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

            set { _frameLatch = value; }
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

        public abstract string Disassemble(ushort address, DisplayProcessorMode mode);

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
        protected const int _frameClocks40Hz = 13889;     // cycles per 1/40th of a second (rounded up)
        protected bool _frameLatch;

        protected ProcessorState _state;
        protected bool _halted;                           // The Halted flag is independent of the current state.
                                                          // (i.e. it is set when the processor gets halted, but can later be cleared
                                                          //  whiile the display remains halted.)           
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
            public DisplayInstructionBase(ushort word, DisplayProcessorMode mode)
            {
                _usageMode = mode;
                _word = word;
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
            public abstract string Disassemble(DisplayProcessorMode mode);

            /// <summary>
            /// Implemented to provide decoding of this instruction word.
            /// </summary>
            protected abstract void Decode();

            protected DisplayOpcode _opcode;
            protected ushort _data;
            protected DisplayProcessorMode _usageMode;
            protected ushort _word;
        }

    }
}
