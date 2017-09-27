﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.IIS.Administration.WebServer.Monitoring
{
    using Microsoft.IIS.Administration.Monitoring;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    class WebServerMonitor : IWebServerMonitor, IDisposable
    {
        private IEnumerable<int> _allProcesses;
        private IEnumerable<int> _webserverProcesses;
        private IEnumerable<IPerfCounter> _webserverCounters;
        private readonly TimeSpan RefreshRate = TimeSpan.FromSeconds(1);
        private DateTime _lastCalculation;
        private CounterMonitor _counterMonitor;
        private WebServerSnapshot _snapshot = new WebServerSnapshot();
        private CounterProvider _provider = new CounterProvider();

        public void Dispose()
        {
            if (_counterMonitor != null) {
                _counterMonitor.Dispose();
                _counterMonitor = null;
            }
        }

        public async Task<IWebServerSnapshot> GetSnapshot()
        {
            await CalculateData();
            return _snapshot;
        }

        private async Task Initialize()
        {
            IEnumerable<int> webserverProcessIds = ProcessUtil.GetWebserverProcessIds();

            //
            // Set up tracking for change detection
            _allProcesses = Process.GetProcesses().Select(p => p.Id);
            _webserverProcesses = webserverProcessIds.OrderBy(id => id);
            _webserverCounters = GetWebserverCounters();

            //
            // Set up counters for data calculation
            var counters = new List<IPerfCounter>();
            counters.AddRange(GetMemoryCounters());
            counters.AddRange(GetCacheCounters());
            counters.AddRange(await ProcessUtil.GetProcessCounters(webserverProcessIds));
            counters.AddRange(_webserverCounters);

            //
            // Set up counter monitor for data retrieval
            _counterMonitor = new CounterMonitor(counters);
        }

        private async Task CalculateData()
        {
            if (_counterMonitor == null) {
                await Initialize();
            }

            if ((DateTime.UtcNow - _lastCalculation > RefreshRate) && HasChanged()) {
                await Reset();
            }

            if (_counterMonitor.Counters.Count() > 0) {
                await Query();
            }

            long bytesSentSec = 0;
            long bytesRecvSec = 0;
            long connectionAttemptsSec = 0;
            long totalConnectionAttempts = 0;
            long activeRequests = 0;
            long requestsSec = 0;
            long totalRequests = 0;
            long percentCpuTime = 0;
            long pageFaultsSec = 0;
            long handleCount = 0;
            long privateBytes = 0;
            long threadCount = 0;
            long privateWorkingSet = 0;
            long workingSet = 0;
            long IOReadSec = 0;
            long IOWriteSec = 0;
            long percent500 = 0;
            long availableBytes = 0;
            long fileCacheMemoryUsage = 0;
            long currentFilesCached = 0;
            long currentUrisCached = 0;
            long fileCacheHits = 0;
            long fileCacheMisses = 0;
            long outputCacheCurrentItems = 0;
            long outputCacheCurrentMemoryUsage = 0;
            long outputCacheTotalHits = 0;
            long outputCacheTotalMisses = 0;
            long totalFilesCached = 0;
            long totalUrisCached = 0;
            long uriCacheHits = 0;
            long uriCacheMisses = 0;

            foreach (IPerfCounter counter in _counterMonitor.Counters) {
                if (counter.CategoryName.Equals(WebSiteCounterNames.Category)) {
                    switch (counter.Name) {
                        case WebSiteCounterNames.BytesRecvSec:
                            bytesRecvSec += counter.Value;
                            break;
                        case WebSiteCounterNames.BytesSentSec:
                            bytesSentSec += counter.Value;
                            break;
                        case WebSiteCounterNames.ConnectionAttemptsSec:
                            connectionAttemptsSec += counter.Value;
                            break;
                        case WebSiteCounterNames.TotalConnectionAttempts:
                            totalConnectionAttempts += counter.Value;
                            break;
                        case WebSiteCounterNames.TotalMethodRequestsSec:
                            requestsSec += counter.Value;
                            break;
                        case WebSiteCounterNames.TotalOtherMethodRequestsSec:
                            requestsSec += counter.Value;
                            break;
                        case WebSiteCounterNames.TotalMethodRequests:
                            totalRequests += counter.Value;
                            break;
                        case WebSiteCounterNames.TotalOtherMethodRequests:
                            totalRequests += counter.Value;
                            break;
                        default:
                            break;
                    }
                }

                if (counter.CategoryName.Equals(WorkerProcessCounterNames.Category)) {
                    switch (counter.Name) {
                        case WorkerProcessCounterNames.ActiveRequests:
                            activeRequests += counter.Value;
                            break;
                        case WorkerProcessCounterNames.Percent500:
                            percent500 += counter.Value;
                            break;
                        default:
                            break;
                    }
                }

                if (counter.CategoryName.Equals(ProcessCounterNames.Category)) {
                    switch (counter.Name) {
                        case ProcessCounterNames.PercentCpu:
                            percentCpuTime += counter.Value;
                            break;
                        case ProcessCounterNames.HandleCount:
                            handleCount += counter.Value;
                            break;
                        case ProcessCounterNames.PrivateBytes:
                            privateBytes += counter.Value;
                            break;
                        case ProcessCounterNames.ThreadCount:
                            threadCount += counter.Value;
                            break;
                        case ProcessCounterNames.PrivateWorkingSet:
                            privateWorkingSet += counter.Value;
                            break;
                        case ProcessCounterNames.WorkingSet:
                            workingSet += counter.Value;
                            break;
                        case ProcessCounterNames.IOReadSec:
                            IOReadSec += counter.Value;
                            break;
                        case ProcessCounterNames.IOWriteSec:
                            IOWriteSec += counter.Value;
                            break;
                        case ProcessCounterNames.PageFaultsSec:
                            pageFaultsSec += counter.Value;
                            break;
                        default:
                            break;
                    }
                }

                if (counter.CategoryName.Equals(MemoryCounterNames.Category)) {
                    switch (counter.Name) {
                        case MemoryCounterNames.AvailableBytes:
                            availableBytes += counter.Value;
                            break;
                        default:
                            break;
                    }
                }

                if (counter.CategoryName.Equals(CacheCounterNames.Category)) {
                    switch (counter.Name) {
                        case CacheCounterNames.CurrentFileCacheMemoryUsage:
                            fileCacheMemoryUsage += counter.Value;
                            break;
                        case CacheCounterNames.CurrentFilesCached:
                            currentFilesCached += counter.Value;
                            break;
                        case CacheCounterNames.CurrentUrisCached:
                            currentUrisCached += counter.Value;
                            break;
                        case CacheCounterNames.FileCacheHits:
                            fileCacheHits += counter.Value;
                            break;
                        case CacheCounterNames.FileCacheMisses:
                            fileCacheMisses += counter.Value;
                            break;
                        case CacheCounterNames.OutputCacheCurrentItems:
                            outputCacheCurrentItems += counter.Value;
                            break;
                        case CacheCounterNames.OutputCacheCurrentMemoryUsage:
                            outputCacheCurrentMemoryUsage += counter.Value;
                            break;
                        case CacheCounterNames.OutputCacheTotalHits:
                            outputCacheTotalHits += counter.Value;
                            break;
                        case CacheCounterNames.OutputCacheTotalMisses:
                            outputCacheTotalMisses += counter.Value;
                            break;
                        case CacheCounterNames.TotalFilesCached:
                            totalFilesCached += counter.Value;
                            break;
                        case CacheCounterNames.TotalUrisCached:
                            totalUrisCached += counter.Value;
                            break;
                        case CacheCounterNames.UriCacheHits:
                            uriCacheHits += counter.Value;
                            break;
                        case CacheCounterNames.UriCacheMisses:
                            uriCacheMisses += counter.Value;
                            break;
                        default:
                            break;
                    }
                }
            }

            _snapshot.BytesRecvSec = bytesRecvSec;
            _snapshot.BytesSentSec = bytesSentSec;
            _snapshot.ConnectionAttemptsSec = connectionAttemptsSec;
            _snapshot.TotalConnectionAttempts = totalConnectionAttempts;
            _snapshot.ActiveRequests = activeRequests;
            _snapshot.RequestsSec = requestsSec;
            _snapshot.TotalRequests = totalRequests;
            _snapshot.PercentCpuTime = percentCpuTime;
            _snapshot.HandleCount = handleCount;
            _snapshot.PrivateBytes = privateBytes;
            _snapshot.ThreadCount = threadCount;
            _snapshot.PrivateWorkingSet = privateWorkingSet;
            _snapshot.WorkingSet = workingSet;
            _snapshot.IOReadSec = IOReadSec;
            _snapshot.IOWriteSec = IOWriteSec;
            _snapshot.FileCacheHits = fileCacheHits;
            _snapshot.FileCacheMisses = fileCacheMisses;
            _snapshot.PageFaultsSec = pageFaultsSec;
            _snapshot.Percent500 = percent500;
            _snapshot.AvailableBytes = availableBytes;
            _snapshot.FileCacheMemoryUsage = fileCacheMemoryUsage;
            _snapshot.CurrentFilesCached = currentFilesCached;
            _snapshot.CurrentUrisCached = currentUrisCached;
            _snapshot.FileCacheHits = fileCacheHits;
            _snapshot.FileCacheMisses = fileCacheMisses;
            _snapshot.OutputCacheCurrentItems = outputCacheCurrentItems;
            _snapshot.OutputCacheMemoryUsage = outputCacheCurrentMemoryUsage;
            _snapshot.OutputCacheTotalHits = outputCacheTotalHits;
            _snapshot.OutputCacheTotalMisses = outputCacheTotalMisses;
            _snapshot.TotalFilesCached = totalFilesCached;
            _snapshot.TotalUrisCached = totalUrisCached;
            _snapshot.UriCacheHits = uriCacheHits;
            _snapshot.UriCacheMisses = uriCacheMisses;


            _snapshot.ProcessCount = _webserverProcesses.Count();

            _lastCalculation = DateTime.UtcNow;
        }

        private async Task Reset()
        {
            Dispose();
            await Initialize();
        }

        private async Task Query()
        {
            for (int i = 0; i < 5; i++) {
                try {
                    await _counterMonitor.Refresh();
                    return;
                }
                catch (CounterNotFoundException) {
                    await Task.Delay(20);
                    await Reset();
                }
            }

            await _counterMonitor.Refresh();
        }

        private bool HasChanged()
        {

            if (!Enumerable.SequenceEqual(_allProcesses, Process.GetProcesses().Select(p => p.Id))) {

                if (!Enumerable.SequenceEqual(_webserverProcesses, ProcessUtil.GetWebserverProcessIds().OrderBy(id => id))) {
                    return true;
                }
            }

            return GetWebserverCounters().Count() != _webserverCounters.Count();
        }

        private IEnumerable<IPerfCounter> GetWebserverCounters()
        {
            List<IPerfCounter> counters = new List<IPerfCounter>();
            const string TotalInstance = "_Total";

            // Only use _total counter if instances are available
            if (_provider.GetInstances(WebSiteCounterNames.Category).Where(i => i != TotalInstance).Count() > 0) {
                counters.AddRange(_provider.GetCounters(WebSiteCounterNames.Category, TotalInstance));
            }

            if (_provider.GetInstances(WorkerProcessCounterNames.Category).Where(i => i != TotalInstance).Count() > 0) {
                counters.AddRange(_provider.GetCounters(WorkerProcessCounterNames.Category, TotalInstance));
            }

            return counters;
        }

        private IEnumerable<IPerfCounter> GetMemoryCounters()
        {
            return _provider.GetSingletonCounters(MemoryCounterNames.Category);
        }

        private IEnumerable<IPerfCounter> GetCacheCounters()
        {
            return _provider.GetSingletonCounters(CacheCounterNames.Category);
        }
    }
}
