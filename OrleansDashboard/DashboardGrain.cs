﻿using Orleans;
using Orleans.Concurrency;
using Orleans.Placement;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrleansDashboard
{
    [Reentrant]
    [PreferLocalPlacement]
    public class DashboardGrain : Grain, IDashboardGrain
    {
        private DashboardCounters Counters { get; set; }
        private DateTime StartTime { get; set; }
        private List<GrainTraceEntry> history = new List<GrainTraceEntry>();


        private ISiloDetailsProvider siloDetailsProvider;

      

        private async Task Callback(object _)
        {
            var metricsGrain = this.GrainFactory.GetGrain<IManagementGrain>(0);
            var activationCountTask = metricsGrain.GetTotalActivationCount();
            var simpleGrainStatsTask = metricsGrain.GetSimpleGrainStatistics();
            var siloDetailsTask = siloDetailsProvider.GetSiloDetails();

            await Task.WhenAll(activationCountTask,  simpleGrainStatsTask, siloDetailsTask);

            RecalculateCounters(activationCountTask.Result, siloDetailsTask.Result, simpleGrainStatsTask.Result);
        }

        internal void RecalculateCounters(int activationCount, SiloDetails[] hosts,
            IList<SimpleGrainStatistic> simpleGrainStatistics)
        {
            this.Counters.TotalActivationCount = activationCount;

            this.Counters.TotalActiveHostCount = hosts.Count(x => x.SiloStatus == SiloStatus.Active);
            this.Counters.TotalActivationCountHistory.Enqueue(activationCount);
            this.Counters.TotalActiveHostCountHistory.Enqueue(this.Counters.TotalActiveHostCount);

            while (this.Counters.TotalActivationCountHistory.Count > Dashboard.HistoryLength)
            {
                this.Counters.TotalActivationCountHistory.Dequeue();
            }
            while (this.Counters.TotalActiveHostCountHistory.Count > Dashboard.HistoryLength)
            {
                this.Counters.TotalActiveHostCountHistory.Dequeue();
            }

            // TODO - whatever max elapsed time
            var elapsedTime = Math.Min((DateTime.UtcNow - this.StartTime).TotalSeconds, 100);

            this.Counters.Hosts = hosts;

            var aggregatedTotals = this.history
                .GroupBy(x => new GrainSiloKey(x.Grain, x.SiloAddress))
                .ToDictionary(g => g.Key, g => new AggregatedGrainTotals
                {
                    TotalAwaitTime = g.Sum(x => x.ElapsedTime),
                    TotalCalls = g.Sum(x => x.Count),
                    TotalExceptions = g.Sum(x => x.ExceptionCount)
                });

            this.Counters.SimpleGrainStats = simpleGrainStatistics.Select(x =>
            {
                var grainName = TypeFormatter.Parse(x.GrainType);
                var siloAddress = x.SiloAddress.ToParsableString();
                AggregatedGrainTotals totals;
                if (!aggregatedTotals.TryGetValue(new GrainSiloKey(grainName, siloAddress), out totals))
                {
                    totals = new AggregatedGrainTotals();
                }
                return new SimpleGrainStatisticCounter
                {
                    ActivationCount = x.ActivationCount,
                    GrainType = grainName,
                    SiloAddress = x.SiloAddress.ToParsableString(),
                    TotalAwaitTime = totals.TotalAwaitTime,
                    TotalCalls = totals.TotalCalls,
                    TotalExceptions = totals.TotalExceptions,
                    TotalSeconds = elapsedTime
                };
            }).ToArray();
        }

        public override Task OnActivateAsync()
        {
            // note: normally we would use dependency injection
            // but since we do not have access to the registered services collection 
            // from within a bootstrapper we do it this way:
            // first try to resolve from the container, if not present in container
            // then instantiate the default
            this.siloDetailsProvider =
                (this.ServiceProvider.GetService(typeof(ISiloDetailsProvider)) as ISiloDetailsProvider)
                ?? new MembershipTableSiloDetailsProvider(this.GrainFactory);


            this.Counters = new DashboardCounters();
            this.RegisterTimer(this.Callback, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            this.StartTime = DateTime.UtcNow;
            return base.OnActivateAsync();
        }

        public Task<DashboardCounters> GetCounters()
        {
            return Task.FromResult(this.Counters);
        }

        public Task<Dictionary<string, Dictionary<string, GrainTraceEntry>>> GetGrainTracing(string grain)
        {
            var results = new Dictionary<string, Dictionary<string, GrainTraceEntry>>();

            foreach (var historicValue in this.history.Where(x => x.Grain == grain))
            {
                var grainMethodKey = $"{grain}.{historicValue.Method}";
                if (!results.ContainsKey(grainMethodKey))
                {
                    results.Add(grainMethodKey, new Dictionary<string, GrainTraceEntry>());
                }
                var grainResults = results[grainMethodKey];

                var key = historicValue.Period.ToPeriodString();
                if (!grainResults.ContainsKey(key)) grainResults.Add(key, new GrainTraceEntry
                {
                    Grain = historicValue.Grain,
                    Method = historicValue.Method,
                    Period = historicValue.Period
                });
                var value = grainResults[key];
                value.Count += historicValue.Count;
                value.ElapsedTime += historicValue.ElapsedTime;
                value.ExceptionCount += historicValue.ExceptionCount;
            }

            return Task.FromResult(results);
        }

        public Task<Dictionary<string, GrainTraceEntry>> GetClusterTracing()
        {
            var results = new Dictionary<string, GrainTraceEntry>();

            foreach (var historicValue in this.history)
            {
                var key = historicValue.Period.ToPeriodString();
                if (!results.ContainsKey(key)) results.Add(key, new GrainTraceEntry
                {
                    Period = historicValue.Period,
                });
                var value = results[key];
                value.Count += historicValue.Count;
                value.ElapsedTime += historicValue.ElapsedTime;
                value.ExceptionCount += historicValue.ExceptionCount;
            }

            return Task.FromResult(results);
        }

        public Task<Dictionary<string, GrainTraceEntry>> GetSiloTracing(string address)
        {
            var results = new Dictionary<string, GrainTraceEntry>();

            foreach (var historicValue in this.history.Where(x => x.SiloAddress == address))
            {
                var key = historicValue.Period.ToPeriodString();
                if (!results.ContainsKey(key)) results.Add(key, new GrainTraceEntry
                {
                    Period = historicValue.Period,
                });
                var value = results[key];
                value.Count += historicValue.Count;
                value.ElapsedTime += historicValue.ElapsedTime;
                value.ExceptionCount += historicValue.ExceptionCount;
            }

            return Task.FromResult(results);
        }

        public Task Init()
        {
            // just used to activate the grain
            return TaskDone.Done;
        }

        public Task SubmitTracing(string siloIdentity, GrainTraceEntry[] grainTrace)
        {
            var now = DateTime.UtcNow;
            foreach (var entry in grainTrace)
            {
                // sync clocks
                entry.Period = now;
            }

            // fill in any previously captured methods which aren't in this reporting window
            var allGrainTrace = new List<GrainTraceEntry>(grainTrace);
            var values = this.history.Where(x => x.SiloAddress == siloIdentity).GroupBy(x => x.GrainAndMethod).Select(x => x.First());
            foreach (var value in values)
            {
                if (!grainTrace.Any(x => x.GrainAndMethod == value.GrainAndMethod))
                {
                    allGrainTrace.Add(new GrainTraceEntry
                    {
                        Count = 0,
                        ElapsedTime = 0,
                        Grain = value.Grain,
                        Method = value.Method,
                        Period = now,
                        SiloAddress = siloIdentity
                    });
                }
            }

            var retirementWindow = DateTime.UtcNow.AddSeconds(-100);
            history.AddRange(allGrainTrace);
            history.RemoveAll(x => x.Period < retirementWindow);

            return TaskDone.Done;
        }
    }
}