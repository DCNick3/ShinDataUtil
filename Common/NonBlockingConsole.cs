using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ShinDataUtil
{
    /// <summary>
    /// A somewhat hacky way to make console writes faster by introducing separate worker thread
    /// </summary>
    public static class NonBlockingConsole
    {
        private static readonly BlockingCollection<object?> Queue = new BlockingCollection<object?>();
        private static readonly Thread Thread;
        
        static NonBlockingConsole()
        {
            Thread = new Thread(
                () =>
                {
                    var exit = false;
                    while (!exit)
                    {
                        var value = Queue.Take();
                        switch (value)
                        {
                            case string s:
                                Console.WriteLine(s);
                                break;
                            case TaskCompletionSource<object?> t:
                                t.SetResult(null);
                                break;
                            default:
                                exit = true;
                                break;
                        }
                    }
                })
            {
                IsBackground = true
            };
            Thread.Start();
        }

        public static void WriteLine(string value) => Queue.Add(value);
        public static void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));

        public static void Flush()
        {
            var tcs = new TaskCompletionSource<object?>();
            Queue.Add(tcs);
            tcs.Task.Wait();
        }

        public static void Stop()
        {
            Queue.Add(null);
            Thread.Join();
        }
    }
}