using System;
using System.Threading;

namespace FiveRealmsInstallerTests
{
    public static class MutexFixtures
    {
        // Keep a handle alive after its owning thread exits. The next process
        // must recover the abandoned mutex rather than misreport a busy setup.
        public static Mutex Abandoned(string name)
        {
            Mutex result = null;
            Exception failure = null;
            var owner = new Thread(delegate()
            {
                try
                {
                    result = new Mutex(false, name);
                    if (!result.WaitOne(0))
                        failure = new InvalidOperationException("Test mutex is already busy: " + name);
                }
                catch (Exception error) { failure = error; }
            });
            owner.IsBackground = true;
            owner.Start();
            if (!owner.Join(5000)) throw new TimeoutException("Mutex fixture thread did not finish.");
            if (failure != null)
            {
                if (result != null) result.Dispose();
                throw failure;
            }
            return result;
        }
    }
}
