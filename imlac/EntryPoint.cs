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
            _startupArgs = args;

            _console = new SDLConsole(0.5f);
            _imlacSystem = new ImlacSystem();
            _imlacSystem.AttachConsole(_console);
            _imlacSystem.Reset();
         
            _state = SystemExecutionState.Debugging;

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnBreak);

            PrintHerald();

            //
            // Start the debugger / execution thread
            //
            _debuggerThread = new System.Threading.Thread(new System.Threading.ThreadStart(DebuggerThread));
            _debuggerThread.Start();

            //
            // Show the display; this will return when the display window is closed.
            //
            _console.Show();

            //
            // Kill the system if it's still running.
            //
            _debuggerThread.Abort();
        }

        private static void DebuggerThread()
        {
            //
            // Wait for the display to be ready.
            //
            _console.WaitForSync();

            ConsoleExecutor debuggerPrompt = new ConsoleExecutor(_imlacSystem);

            if (_startupArgs.Length > 0)
            {
                //
                // Assume arg 0 is a script file to be executed.
                //
                Console.WriteLine("Executing startup script '{0}'", _startupArgs[0]);

                try
                {
                    _state = debuggerPrompt.ExecuteScript(_imlacSystem, _startupArgs[0]);
                }
                catch (Exception e)
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
                            _state = debuggerPrompt.Prompt(_imlacSystem);
                            break;

                        case SystemExecutionState.SingleStep:
                            _imlacSystem.SingleStep();
                            _imlacSystem.Display.RenderCurrent(false);
                            _state = SystemExecutionState.Debugging;
                            break;

                        case SystemExecutionState.SingleFrame:
                            _imlacSystem.SingleStep();

                            if (_imlacSystem.DisplayProcessor.FrameLatch)
                            {
                                Console.WriteLine("Frame completed.");
                                _state = SystemExecutionState.Debugging;
                            }
                            break;

                        case SystemExecutionState.UntilDisplayStart:
                            _imlacSystem.SingleStep();

                            if (_imlacSystem.DisplayProcessor.State == ProcessorState.Running)
                            {
                                Console.WriteLine("Display started.");
                                _state = SystemExecutionState.Debugging;
                            }
                            break;

                        case SystemExecutionState.SingleDisplayOperation:
                            _imlacSystem.SingleStep();

                            if (_imlacSystem.DisplayProcessor.DisplayDrawLatch)
                            {
                                _imlacSystem.Display.RenderCurrent(false);
                                Console.WriteLine("Display operation completed.");
                                _state = SystemExecutionState.Debugging;
                            }
                            break;

                        case SystemExecutionState.Running:
                            _imlacSystem.SingleStep();

                            if (_imlacSystem.Processor.State == ProcessorState.Halted)
                            {
                                Console.WriteLine("Main processor halted at {0}", Helpers.ToOctal(_imlacSystem.Processor.PC));
                                _state = SystemExecutionState.Debugging;
                            }
                            else if (_imlacSystem.Processor.State == ProcessorState.BreakpointHalt)
                            {
                                Console.WriteLine(
                                    "Breakpoint hit: {0} at address {1}",
                                    BreakpointManager.GetBreakpoint(_imlacSystem.Processor.BreakpointAddress),
                                    Helpers.ToOctal(_imlacSystem.Processor.BreakpointAddress));
                                _state = SystemExecutionState.Debugging;
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    if (!(e is System.Threading.ThreadAbortException))
                    {
                        Console.WriteLine("Internal error during execution: {0}", e.Message);
                        _state = SystemExecutionState.Debugging;
                    }
                }
            }

            // We are exiting, shut things down.
            //
            _imlacSystem.Shutdown();
        }

        private static void OnBreak(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("User break.");
            _state = SystemExecutionState.Debugging;
            e.Cancel = true;

            // Flush console input.
            while(Console.KeyAvailable)
            {
                Console.ReadKey();
            }

        }

        private static void PrintHerald()
        {
            Console.WriteLine("sImlac v0.3.  (c) 2016-2020 Living Computers: Museum+Labs");
            Console.WriteLine();
        }

        private static string[] _startupArgs;
        private static SDLConsole _console;
        private static ImlacSystem _imlacSystem;
        private static SystemExecutionState _state;

        private static System.Threading.Thread _debuggerThread;
    }
}
