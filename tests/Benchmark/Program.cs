﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nito.AsyncEx;
using Pipelines.Sockets.Unofficial.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmark
{
    internal static class Program
    {
        static void Main() => BenchmarkRunner.Run<LockBenchmarks>();

        public static int AssertIs(this int actual, int expected)
        {
            if (actual != expected) throw new InvalidOperationException($"expected {expected} but was {actual}");
            return actual;
        }
    }

    [MemoryDiagnoser, CoreJob, ClrJob]
    public class LockBenchmarks
    {
        const int TIMEOUTMS = 2000;
        private readonly MutexSlim _mutexSlim = new MutexSlim(TIMEOUTMS);
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly AsyncSemaphore _asyncSemaphore = new AsyncSemaphore(1);
        private readonly object _syncLock = new object();

        const int PER_TEST = 5 * 1024;

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public int Monitor_Sync()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                bool haveLock = false;
                Monitor.TryEnter(_syncLock, TIMEOUTMS, ref haveLock);
                try
                {
                    if (haveLock) count++;
                }
                finally
                {
                    if (haveLock) Monitor.Exit(_syncLock);
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public int SemaphoreSlim_Sync()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                if (_semaphoreSlim.Wait(TIMEOUTMS))
                {
                    try // make sure we measure the expected try/finally usage
                    {
                        count++;
                    }
                    finally
                    {
                        _semaphoreSlim.Release();
                    }
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public async ValueTask<int> SemaphoreSlim_Async()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                if (await _semaphoreSlim.WaitAsync(TIMEOUTMS))
                {
                    try // make sure we measure the expected try/finally usage
                    {
                        count++;
                    }
                    finally
                    {
                        _semaphoreSlim.Release();
                    }
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public async ValueTask<int> SemaphoreSlim_Async_HotPath()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                var awaitable = _semaphoreSlim.WaitAsync(TIMEOUTMS);
                if (awaitable.IsCompleted)
                {
                    if (awaitable.Result)
                    {
                        count++;
                        _semaphoreSlim.Release();
                    }
                }
                else
                {
                    if (await awaitable)
                    {
                        count++;
                        _semaphoreSlim.Release();
                    }
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public int MutexSlim_Sync()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                using (var token = _mutexSlim.TryWait())
                {
                    if (token) count++;
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public async ValueTask<int> MutexSlim_Async()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                using (var token = await _mutexSlim.TryWaitAsync())
                {
                    if (token) count++;
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public async ValueTask<int> MutexSlim_Async_HotPath()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                var awaitable = _mutexSlim.TryWaitAsync();
                if (awaitable.IsCompletedSuccessfully)
                {
                    using (var token = awaitable.Result)
                    {
                        if (token) count++;
                    }
                }
                else
                {
                    using (var token = await awaitable)
                    {
                        if (token) count++;
                    }
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public int AsyncSemaphore_Sync()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                _asyncSemaphore.Wait();
                try
                {
                    count++;
                }
                finally
                { // to make useful comparison
                    _asyncSemaphore.Release();
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public async ValueTask<int> AsyncSemaphore_Async()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                await _asyncSemaphore.WaitAsync();
                try
                {
                    count++;
                }
                finally
                { // to make useful comparison
                    _asyncSemaphore.Release();
                }
            }
            return count.AssertIs(PER_TEST);
        }

        [Benchmark(OperationsPerInvoke = PER_TEST)]
        public async ValueTask<int> AsyncSemaphore_Async_HotPath()
        {
            int count = 0;
            for (int i = 0; i < PER_TEST; i++)
            {
                var pending = _asyncSemaphore.WaitAsync();
                if (pending.Status != TaskStatus.RanToCompletion)
                {
                    await pending;
                }
                try
                {
                    count++;
                }
                finally
                { // to make useful comparison
                    _asyncSemaphore.Release();
                }
            }
            return count.AssertIs(PER_TEST);
        }
    }
}
