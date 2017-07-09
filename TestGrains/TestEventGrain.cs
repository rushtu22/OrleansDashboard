using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

namespace TestGrains
{
    public interface ITestEventGrain : IGrainWithStringKey
    {
        Task AddEvent(Event e);

        Task AddEventMarket(EventMarket em);

        Task AddEventMarketOutcome(EventMarketOutcome emo, long emId);

        Task<Event> GetEvent();
    }

    public class TestEventGrain : Grain, ITestEventGrain
    {
        public Event Event { get; set; }

        public override Task OnActivateAsync()
        {
            Event = new Event();
            Event.EventMarkets = new List<EventMarket>();

            return base.OnActivateAsync();
        }

        public Task AddEvent(Event e)
        {
            Event.Id = e.Id;
            Event.EventName = e.EventName;
            return TaskDone.Done;
        }

        public Task AddEventMarket(EventMarket em)
        {
            em.EventMarketOutcomes = new List<EventMarketOutcome>();
            Event.EventMarkets.Add(em);

            return TaskDone.Done;
        }

        public Task AddEventMarketOutcome(EventMarketOutcome emo, long emId)
        {
            var market = Event.EventMarkets.Find(em => em.Id == emId);

            if (market != null)
                market.EventMarketOutcomes.Add(emo);
            else
                throw new Exception("marketi ver vipove dzma");

            return TaskDone.Done;
        }

        public Task<Event> GetEvent()
        {
            return Task.FromResult(Event);
        }
    }

    #region HelperClass

    public class Event
    {
        public long Id { get; set; }

        public string EventName { get; set; }

        public List<EventMarket> EventMarkets { get; set; }

    }

    public class EventMarket
    {
        public long Id { get; set; }

        public string EventMarketName { get; set; }

        public List<EventMarketOutcome> EventMarketOutcomes { get; set; }
    }

    public class EventMarketOutcome
    {
        public long Id { get; set; }

        public string EventMarketOutcomeName { get; set; }


    }

    #endregion
}
