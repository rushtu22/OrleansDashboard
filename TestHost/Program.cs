using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.TestingHost;
using OrleansDashboard;
using TestGrains;
using System.Collections.Generic;

namespace TestHost
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Starting silos");
            Console.WriteLine("Dashboard will listen on http://localhost:8080/");

            // Deploy 3 silos
            var options = new TestClusterOptions(3);
            options.ClusterConfiguration.Globals.RegisterDashboard();
            var cluster = new TestCluster(options);
            cluster.Deploy();

            // generate some calls to a test grain
            Console.WriteLine("All silos are up and running");

            var tokenSource = new CancellationTokenSource();
            var t = new Thread(() => CallGenerator(cluster.Client, tokenSource).Wait());
            t.Start();

            Console.ReadLine();
            tokenSource.Cancel();
            try
            {
                t.Join(TimeSpan.FromSeconds(3));
            }
            catch
            { }
            cluster.StopAllSilos();
        }

        private static async Task CallGenerator(IClusterClient client, CancellationTokenSource tokenSource)
        {
            var eventIdList = new List<long>();
            for (long i = 1; i < 1000; i++)
            {
                eventIdList.Add(i);
            }

            var eventOutcomeIdList = new List<long>();
            for (long i = 1; i < 100; i++)
            {
                eventOutcomeIdList.Add(i);
            }

            Parallel.ForEach(eventIdList, async e =>
             {
                 await AddEvent(client, e);
             });

            Parallel.ForEach(eventIdList, async e =>
            {
                var rand = new Random().Next(10000);
                await AddEventMarket(client, e, rand);
                Parallel.ForEach(eventOutcomeIdList, async o =>
                {
                    await AddEventMarketOutcome(client, e, rand, o);
                });
            });

            Parallel.ForEach(eventIdList, async e =>
            {
                await GetEvent(client, e);
            });

            while (!tokenSource.IsCancellationRequested)
            {

            }
        }

        private static async Task AddEvent(IClusterClient client, long id)
        {
            var testGrain = client.GetGrain<ITestEventGrain>(id.ToString());
            await testGrain.AddEvent(new Event
            {
                Id = id,
                EventName = $"Event {id}"
            });
        }

        private static async Task AddEventMarket(IClusterClient client, long eventId, long id)
        {
            var testGrain = client.GetGrain<ITestEventGrain>(eventId.ToString());
            await testGrain.AddEventMarket(new EventMarket
            {
                EventMarketName = $"Test EventMarket {id}",
                Id = id
            });
        }

        private static async Task AddEventMarketOutcome(IClusterClient client, long eventId, long eventMarketid, long id)
        {
            var testGrain = client.GetGrain<ITestEventGrain>(eventId.ToString());
            await testGrain.AddEventMarketOutcome(new EventMarketOutcome
            {
                EventMarketOutcomeName = $"Test EventMarketOutcome {id}",
                Id = id
            }, eventMarketid);
        }

        private static async Task<Event> GetEvent(IClusterClient client, long id)
        {
            var testGrain = client.GetGrain<ITestEventGrain>(id.ToString());
            return await testGrain.GetEvent();
        }
    }
}