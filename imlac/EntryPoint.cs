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

using imlac.Debugger;
using System;

namespace imlac
{
    class EntryPoint
    {
        static void Main(string[] args)
        {
            ImlacSystem system = new ImlacSystem();
            ConsoleExecutor debuggerPrompt =
                new ConsoleExecutor(system);

            _state = SystemExecutionState.Debugging;

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnBreak);

            PrintHerald();

            if (args.Length > 0)
            {
                //
                // Assume arg 0 is a script file to be executed.
                //
                Console.WriteLine("Executing startup script '{0}'", args[0]);

                try
                {
                    _state = debuggerPrompt.ExecuteScript(system, args[0]);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error parsing script: {0}", e.Message);
                }
            }

            while (_state != SystemExecutionState.Quit)
            {
                try
                {
                    switch (_state)
                    {
                        case SystemExecutionState.Halted:
                        case SystemExecutionState.Debugging:
                            _state = debuggerPrompt.Prompt(system);
                            break;

                        case SystemExecutionState.SingleStep:
                            system.SingleStep();
                            system.Display.RenderCurrent(false);
                            _state = SystemExecutionState.Debugging;
                            break;

                        case SystemExecutionState.SingleFrame:
                            system.SingleStep();

                            if (system.DisplayProcessor.FrameLatch)
                            {
                                Console.WriteLine("Frame completed.");
                                _state = SystemExecutionState.Debugging;
                            }
                            break;

                        case SystemExecutionState.UntilDisplayStart:
                            system.SingleStep();

                            if (system.DisplayProcessor.State == ProcessorState.Running)
                            {
                                Console.WriteLine("Display started.");
                                _state = SystemExecutionState.Debugging;
                            }
                            break;

                        case SystemExecutionState.Running:
                            system.SingleStep();

                            if (system.Processor.State == ProcessorState.Halted)
                            {
                                Console.WriteLine("Main processor halted at {0}", Helpers.ToOctal(system.Processor.PC));
                                _state = SystemExecutionState.Debugging;
                            }
                            else if (system.Processor.State == ProcessorState.BreakpointHalt)
                            {
                                Console.WriteLine(
                                    "Breakpoint hit: {0} at address {1}",
                                    BreakpointManager.GetBreakpoint(system.Processor.BreakpointAddress),
                                    Helpers.ToOctal(system.Processor.BreakpointAddress));
                                _state = SystemExecutionState.Debugging;
                            }
                            break;
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Internal error during execution: {0}", e.Message);
                    _state = SystemExecutionState.Debugging;
                }
            }
        }

        static void OnBreak(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("User break.");
            _state = SystemExecutionState.Debugging;
            e.Cancel = true;
        }

        static void PrintHerald()
        {
            Console.WriteLine("sImlac v0.1.  (c) 2016, 2017 Living Computers: Museum+Labs");
            Console.WriteLine();
        }

        private static SystemExecutionState _state;
    }
}
