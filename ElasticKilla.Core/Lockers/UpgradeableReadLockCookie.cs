using System;
using System.Threading;

namespace ElasticKilla.Core.Lockers
{
    public struct UpgradeableReadLockCookie : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;

        public UpgradeableReadLockCookie(ReaderWriterLockSlim rwl)
        {
            (_lock = rwl)?.EnterUpgradeableReadLock();
        }

        public void Dispose()
        {
            _lock?.ExitUpgradeableReadLock();
        }
    }
}