using System;
using System.Threading;

namespace ElasticKilla.Core.Lockers
{
    public struct ReadLockCookie : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;

        public ReadLockCookie(ReaderWriterLockSlim rwl)
        {
            (_lock = rwl)?.EnterReadLock();
        }
        
        public void Dispose()
        {
            if (_lock != null && _lock.IsReadLockHeld)
                _lock?.ExitReadLock();
        }
    }
}