using System;
using System.Threading;

namespace Tawop
{
    internal static class OperationControl
    {
        [ThreadStatic]
        private static CancellationToken cancellation;

        [ThreadStatic]
        private static Action<string> progress;

        internal static IDisposable Enter(CancellationToken token, Action<string> notify)
        {
            token.ThrowIfCancellationRequested();
            var scope = new Scope(cancellation, progress);
            cancellation = token;
            progress = notify;
            return scope;
        }

        internal static void Check()
        {
            cancellation.ThrowIfCancellationRequested();
        }

        internal static void Report(string message)
        {
            Check();
            if (progress != null)
            {
                progress(message);
            }

            Check();
        }

        internal static bool Wait(int milliseconds)
        {
            return cancellation.WaitHandle.WaitOne(milliseconds);
        }

        internal static void Recovery(string message)
        {
            if (progress != null)
            {
                try
                {
                    progress(message);
                }
                catch
                {
                }
            }
        }

        internal static CancellationToken Token
        {
            get { return cancellation; }
        }

        private sealed class Scope : IDisposable
        {
            private readonly CancellationToken previousCancellation;
            private readonly Action<string> previousProgress;

            internal Scope(CancellationToken token, Action<string> notify)
            {
                previousCancellation = token;
                previousProgress = notify;
            }

            public void Dispose()
            {
                cancellation = previousCancellation;
                progress = previousProgress;
            }
        }
    }
}
