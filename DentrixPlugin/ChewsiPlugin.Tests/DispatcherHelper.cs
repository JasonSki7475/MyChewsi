using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace ChewsiPlugin.Tests
{
    /// <summary>
    /// Helper class to process Dispatcher's queue in unit tests
    /// Based on this: http://blog.rees.biz/2011/01/unit-testing-worker-threads-and-wpf.html
    /// </summary>
    public static class DispatcherHelper
    {
        private static readonly DispatcherOperationCallback ExitFrameCallback = ExitFrame;

        /// <summary>
        /// Processes all UI messages currently in the message queue.
        /// </summary>
        private static void DoEvents()
        {
            // Create new nested message pump.
            var nestedFrame = new DispatcherFrame();

            // Dispatch a callback to the current message queue, when getting called, 
            // this callback will end the nested message loop.
            // note that the priority of this callback should be lower than the that of UI event messages.
            DispatcherOperation exitOperation = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, ExitFrameCallback, nestedFrame);

            // pump the nested message loop, the nested message loop will immediately 
            // process the messages left inside the message queue.
            Dispatcher.PushFrame(nestedFrame);

            // If the "exitFrame" callback doesn't get finished, Abort it.
            if (exitOperation.Status != DispatcherOperationStatus.Completed)
            {
                exitOperation.Abort();
            }
        }

        /// <summary>
        /// Processes all UI messages currently in the message queue.
        /// </summary>
        /// <param name="dispatcher">The dispatcher onto which to push a new frame.</param>
        public static void DoEvents(Dispatcher dispatcher)
        {
            // Create new nested message pump.
            var nestedFrame = new DispatcherFrame();
            Debug.Assert(dispatcher == nestedFrame.Dispatcher, "Multiple dispatchers are running");

            // Dispatch a callback to the current message queue, when getting called, 
            // this callback will end the nested message loop.
            // note that the priority of this callback should be lower than the that of UI event messages.
            DispatcherOperation exitOperation = dispatcher.BeginInvoke(DispatcherPriority.Background, ExitFrameCallback, nestedFrame);

            // pump the nested message loop, the nested message loop will immediately 
            // process the messages left inside the message queue.
            Dispatcher.PushFrame(nestedFrame);

            // If the "exitFrame" callback doesn't get finished, Abort it.
            if (exitOperation.Status != DispatcherOperationStatus.Completed)
            {
                exitOperation.Abort();
            }
        }

        private static object ExitFrame(object state)
        {
            var frame = state as DispatcherFrame;
            if (frame == null)
            {
                throw new ArgumentOutOfRangeException("state parameter is of the wrong type.");
            }

            // Exit the nested message loop.
            frame.Continue = false;
            return null;
        }

        public static void WaitWithDispatcher(int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                DoEvents();
                Thread.Sleep(100);
            }
            sw.Stop();
        }
    }
}
