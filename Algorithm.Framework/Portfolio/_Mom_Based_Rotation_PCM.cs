/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Algorithm.Framework;
using QuantConnect.Algorithm;

namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Provides an implementation of <see cref="IPortfolioConstructionModel"/> that gives equal weighting to all
    /// securities. The target percent holdings of each security is 1/N where N is the number of securities. For
    /// insights of direction <see cref="InsightDirection.Up"/>, long targets are returned and for insights of direction
    /// <see cref="InsightDirection.Down"/>, short targets are returned.
    /// </summary>
    public class _Mom_Based_Rotation_PCM : PortfolioConstructionModel
    {
        private List<Symbol> _removedSymbols;

        private List<PortfolioConstructionAlphaModelData> _alphaModels = new List<PortfolioConstructionAlphaModelData>();

        public _Mom_Based_Rotation_PCM(QCAlgorithmFramework algorithm, PortfolioConstructionAlphaModelData mom_Based_Rotation_PCAMD)
        {
            _alphaModels.Add(mom_Based_Rotation_PCAMD);
        }

        /// <summary>
        /// Create portfolio targets from the specified insights
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="insights">The insights to create portoflio targets from</param>
        /// <returns>An enumerable of portoflio targets to be sent to the execution model</returns>
        public override IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithmFramework algorithm, Insight[] insights)
        {
            List<PortfolioTarget> targets = new List<PortfolioTarget>();

            // if no new insights and no removed symbols than return nothing new
            if (insights.Length == 0 &&
                _removedSymbols.Count() == 0)
            {
                return targets;
            }

            // Create flatten target for each security that was removed from the universe
            // and remove insights for removed securities that might have sneaked in
            if (_removedSymbols.Count != 0)
            {
                targets.AddRange(_removedSymbols.Select(symbol => new PortfolioTarget(symbol, 0)));
                foreach (Insight i in insights) if (_removedSymbols.Select(s => s.Value).Contains(i.Symbol.Value)) insights.ToList().Remove(i);
                _removedSymbols = null;
            }

            foreach (PortfolioConstructionAlphaModelData pcamd in _alphaModels)
            {
                algorithm.Debug(String.Format("Time: {0}. Model: {1}, current cash: {2}, current holdings: {3}.",
                            algorithm.Time.ToString(),
                            pcamd.Name.ToString(),
                            pcamd.CurrentAlphaModelCash.ToString(),
                            pcamd.GetCurrentAlphaModelHoldingsValue(algorithm).ToString()));

                pcamd.UpdateActiveInsights(insights);
                pcamd.PruneExpiredInsightsForWhichExchangeWasOpen(algorithm);
                pcamd.UpdateNewTargets(algorithm);
                pcamd.UpdateNewTargetsWithFlatTargets(algorithm);

                algorithm.Debug(String.Format("Time: {0}. New Targets are ready.",
                            algorithm.Time.ToString()));

                if (pcamd.RebalancingDate.GetDates(algorithm.Time.Date, algorithm.Time.Date).Count() == 1 &&
                    pcamd.RebalancingTime.CreateUtcEventTimes(new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(h => h.Hour).Contains(algorithm.Time.Hour) &&
                    pcamd.RebalancingTime.CreateUtcEventTimes(new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(m => m.Minute).Contains(algorithm.Time.Minute))
                {
                    pcamd.UpdateNewTargetsWithIsSubmitted();
                    pcamd.UpdateActiveTargetsWithNewTargets();

                    targets.AddRange(pcamd.ActiveTargets.Select(t => t.Target));
                }
            }

            return targets;

            /*var targets = new List<IPortfolioTarget>();

            if (algorithm.UtcTime <= _nextExpiryTime &&
                algorithm.UtcTime <= _rebalancingTime &&
                insights.Length == 0 &&
                _removedSymbols == null)
            {
                return targets;
            }

            _insightCollection.AddRange(insights);

            // Create flatten target for each security that was removed from the universe
            if (_removedSymbols != null)
            {
                var universeDeselectionTargets = _removedSymbols.Select(symbol => new PortfolioTarget(symbol, 0));
                targets.AddRange(universeDeselectionTargets);
                _removedSymbols = null;
            }

            // Get insight that haven't expired of each symbol that is still in the universe
            // check for expiration on next minute
            var activeInsights = _insightCollection.GetActiveInsights(algorithm.UtcTime.AddMinutes(1));

            // Get the last generated active insight for each symbol
            var lastActiveInsights = from insight in activeInsights
                                     group insight by insight.Symbol into g
                                     select g.OrderBy(x => x.GeneratedTimeUtc).Last();

            // give equal weighting to each security
            var count = lastActiveInsights.Count(x => x.Direction != InsightDirection.Flat);
            var percent = count == 0 ? 0 : 1m / count;

            var errorSymbols = new HashSet<Symbol>();

            foreach (var insight in lastActiveInsights)
            {
                var target = PortfolioTarget.Percent(algorithm, insight.Symbol, (int) insight.Direction * percent);
                if (target != null)
                {
                    targets.Add(target);
                }
                else
                {
                    errorSymbols.Add(insight.Symbol);
                }
            }

            // Get expired insights and create flatten targets for each symbol
            var expiredInsights = _insightCollection.RemoveExpiredInsights(algorithm.UtcTime);

            var expiredTargets = from insight in expiredInsights
                                 group insight.Symbol by insight.Symbol into g
                                 where !_insightCollection.HasActiveInsights(g.Key, algorithm.UtcTime) && !errorSymbols.Contains(g.Key)
                                 select new PortfolioTarget(g.Key, 0);

            targets.AddRange(expiredTargets);

            _nextExpiryTime = _insightCollection.GetNextExpiryTime();
            _rebalancingTime = algorithm.UtcTime.Add(_rebalancingPeriod);

            return targets;
            */
            //return _currentTargets;
        }

        /// <summary>
        /// public function that can be called from event handler in order to update event data for all Alpha Models
        /// handled by this Portfolio Construction Model
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="orderEvent">The order event to be added to the tracked portfolio target</param>
        public void AddOrderEvent(QCAlgorithmFramework algorithm, Orders.OrderEvent orderEvent)
        {
            foreach (PortfolioConstructionAlphaModelData pcamd in _alphaModels)
            {
                //if the orderEvent symbol is a symbol associated with the current
                //alpha model then proceed

                ; //do check this.name.Mom_Based_Alpha

                Orders.OrderTicket ot = algorithm.Transactions.GetOrderTicket(orderEvent.OrderId);

                Orders.OrderEvent [] oes = ot.OrderEvents.OrderByDescending(x => x.UtcTime).ToArray();

                Orders.OrderEvent[] previousPartialFills = new Orders.OrderEvent[] { };

                for (int i =0; i <  oes.Count(); i++)
                {
                    if (oes[i].Status == Orders.OrderStatus.PartiallyFilled &&
                        oes[i] != orderEvent &&
                        oes[i].UtcTime < orderEvent.UtcTime)
                    {
                        previousPartialFills = previousPartialFills.Union(new Orders.OrderEvent[1] { oes[i] }).OrderByDescending(x => x.UtcTime).ToArray();
                    }
                }

                //if partial fill, add previous partial fills, retract them and apply the current partial fill
                //don't add fee yet
                if (orderEvent.Status == Orders.OrderStatus.PartiallyFilled)
                {
                    ; // do
                }

                //if canceled, just apply order fee
                if (orderEvent.Status == Orders.OrderStatus.Canceled)
                {
                    ; // do
                }

                //if filled, add previous partial fills, retract them and apply total order value with fee
                if (orderEvent.Status == Orders.OrderStatus.Filled)
                {
                    ; // do
                }

                //currently it is assumed that if the order contained symbol and quantity equal to target
                //than it is a match. In the future the PortfolioTarget class can be overloaded in order
                //to add tracking information that can be used in Execution Model and passed back for identification.
                /*PortfolioConstructionAlphaModelData.PortfolioTargetTracking orderTarget =
                    (from ptt in pcamd.ActiveTargets
                     where ptt.Target.Symbol.Value == algorithm.Transactions.GetOrderById(orderEvent.OrderId).Symbol.Value &&
                     ptt.OrderQuantity == algorithm.Transactions.GetOrderById(orderEvent.OrderId).Quantity
                     select ptt)
                    .Single();

                orderTarget.AddOrderEvent(algorithm, orderEvent);

                if (orderEvent.Status == Orders.OrderStatus.Filled ||
                    orderEvent.Status == Orders.OrderStatus.Canceled)
                {
                    pcamd.UpdateCash(-1 * algorithm.Transactions.GetOrderById(orderEvent.OrderId).Value + -1 * orderEvent.OrderFee);
                }*/
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            // Get removed symbol and invalidate them in the insight collection and remove active targets with them
            _removedSymbols = changes.RemovedSecurities.Select(x => x.Symbol).ToList();

            foreach (PortfolioConstructionAlphaModelData pcamd in _alphaModels)
            {
                foreach (Insight i in pcamd.ActiveInsights) if (_removedSymbols.Select(s => s.Value).Contains(i.Symbol.Value)) pcamd.ActiveInsights.ToList().Remove(i);
                foreach (PortfolioConstructionAlphaModelData.PortfolioTargetTracking ptt in pcamd.ActiveTargets) if (_removedSymbols.Select(s => s.Value).Contains(ptt.Target.Symbol.Value)) pcamd.ActiveTargets.ToList().Remove(ptt);
            }
        }

        public class PortfolioConstructionAlphaModelData
        {
            private readonly String _alphaName = "";

            private readonly Int32 _startingCash = 0;

            private Decimal _currentCash = 0;

            private readonly Int32 _numberOfTopStocks = 0;

            private readonly Scheduling.IDateRule _rebalancingDate;

            private readonly Scheduling.ITimeRule _rebalancingTime;

            private List<Insight> _activeInsights = new List<Insight>();

            private List<PortfolioTargetTracking> _activeTargets = new List<PortfolioTargetTracking>();

            private List<PortfolioTargetTracking> _newTargets = new List<PortfolioTargetTracking>();

            public PortfolioConstructionAlphaModelData(String alphaName, Int32 startingCash, Int32 numberOfTopStocks, Scheduling.IDateRule rebalancingDate, Scheduling.ITimeRule rebalancingTime)
            {
                _alphaName = alphaName;
                _startingCash = startingCash;
                _currentCash = _startingCash;
                _numberOfTopStocks = numberOfTopStocks;
                _rebalancingDate = rebalancingDate;
                _rebalancingTime = rebalancingTime;
            }

            public List<PortfolioTargetTracking> ActiveTargets
            {
                get { return _activeTargets; }
            }

            public List<Insight> ActiveInsights
            {
                get { return _activeInsights; }
            }

            public String Name { get { return _alphaName; } }

            public Scheduling.IDateRule RebalancingDate
            {
                get { return _rebalancingDate; }
            }

            public Scheduling.ITimeRule RebalancingTime
            {
                get { return _rebalancingTime; }
            }

            /*public Decimal CurrentAlphaModelCash
            {
                get { return _currentCash; }
                set { _currentCash = value; }
            }*/

            /*public Decimal GetCurrentAlphaModelCostOfHoldings(QCAlgorithmFramework algorithm)
            {
                return _activeTargets.Sum(x => (x.Target.Quantity != 0) ? x.TotalCost : 0m);
            }*/

            /*public Decimal GetCurrentAlphaModelHoldingsValue(QCAlgorithmFramework algorithm)
            {
                return _activeTargets.Sum(x => (x.Target.Quantity != 0) ? x.GetCurrentValueOfHoldingInPortfolio(algorithm) : 0m);
            }*/

            public void UpdateActiveInsights(IEnumerable<Insight> incomingInsights)
            {
                //add insights for symbols which are not yet in active insights
                //replace insights for symbols for which new insights are available

                _activeInsights.RemoveAll(x => incomingInsights.Select(s => s.Symbol.Value).Contains(x.Symbol.Value));
                _activeInsights.AddRange(incomingInsights);
            }

            public void PruneExpiredInsightsForWhichExchangeWasOpen(QCAlgorithmFramework algorithm)
            {
                //those expired insights for which exchange was already open since they expired
                //(means there really is no insight)
                _activeInsights.RemoveAll(x =>
                (from ai in _activeInsights
                 where ai.IsExpired(algorithm.UtcTime) &&
                 algorithm.Securities[ai.Symbol.Value].Exchange.Hours.GetNextMarketOpen(ai.CloseTimeUtc.ConvertFromUtc(algorithm.TimeZone), extendedMarket: false).CompareTo(algorithm.Time) < 0
                 select ai).Contains(x));
            }

            /// <summary>
            /// Updates the amount of cash available for this Alpha Model in the Portfolio Construction Model
            /// </summary>
            /// <param name="amount">The amount that the current cash is changed by (+-).</param>
            public void UpdateCash(Decimal amount)
            {
                _currentCash += amount;
            }

            public Decimal CalculateTargetQuantity(QCAlgorithmFramework algorithm, Symbol symbol, Int32 numberOfHoldingsInAlphaModel)
            {
                decimal OrderQuantity =
                    Math.Floor((0.95m * (GetCurrentAlphaModelHoldingsValue(algorithm) + CurrentAlphaModelCash) / numberOfHoldingsInAlphaModel) / algorithm.Securities[symbol.Value].Close).SmartRounding();

                return OrderQuantity;
            }

            public void UpdateNewTargets(QCAlgorithmFramework algorithm)
            {
                _newTargets = new List<PortfolioTargetTracking>();

                bool QuantitiesOverZero = false;

                for (int j = 0; !QuantitiesOverZero; j++)
                {
                    algorithm.Debug(String.Format("Time: {0}. Attempting to create list of new targets with count: {1}.",
                            algorithm.Time.ToString(),
                            (_numberOfTopStocks - j).ToString()));

                    _newTargets =
                    (from ai in _activeInsights
                     where ai.Magnitude > 0 &&
                     ai.Direction == InsightDirection.Up
                     orderby ai.Magnitude descending
                     select new PortfolioTargetTracking(algorithm, ai.Symbol, CalculateTargetQuantity(algorithm, ai.Symbol, _numberOfTopStocks - j)))
                         .Take<PortfolioTargetTracking>(_numberOfTopStocks - j)
                         .ToList<PortfolioTargetTracking>();

                    if ((_newTargets.Count) != _numberOfTopStocks - j)
                    {
                        algorithm.Error(String.Format("Time: {0}. There are not enough insights with positive direction to create requested number of targets. Emptying the list of new targets!!!",
                            algorithm.Time.ToString()));

                        _newTargets = new List<PortfolioTargetTracking>();
                    }

                    List<PortfolioTargetTracking> newTargetsWithQuantityLessThanOne = _newTargets.Where(x => x.Target.Quantity < 1m).Select(y => y).ToList();

                    if (newTargetsWithQuantityLessThanOne.Count > 0)
                    {
                        algorithm.Error(String.Format("Time: {0}. {1} of symbols added to new targets had quantity less than 1. Try with less targets?",
                            algorithm.Time.ToString(),
                            newTargetsWithQuantityLessThanOne.Count.ToString()));

                        algorithm.Debug(String.Format("Time: {0}. Current cash: {1}, current holdings: {2}.",
                            algorithm.Time.ToString(),
                            _currentCash.ToString(),
                            GetCurrentAlphaModelHoldingsValue(algorithm).ToString()));

                        _newTargets = new List<PortfolioTargetTracking>();

                        break;
                    }

                    if (_newTargets.Count == _numberOfTopStocks - j)
                    {
                        if (_newTargets.Count == 0)
                        {
                            algorithm.Error(String.Format("Time: {0}. Can't add even single share of top pick in the list.",
                            algorithm.Time.ToString()));
                        }

                        algorithm.Debug(String.Format("Time: {0}. Adding {1} symbols to new targets.",
                            algorithm.Time.ToString(),
                            _newTargets.Count.ToString()));

                        algorithm.Debug(String.Format("Time: {0}. Current cash: {1}, current holdings: {2}.",
                            algorithm.Time.ToString(),
                            _currentCash.ToString(),
                            GetCurrentAlphaModelHoldingsValue(algorithm).ToString()));

                        QuantitiesOverZero = true;
                    }
                }
            }

            public void UpdateNewTargetsWithFlatTargets(QCAlgorithmFramework algorithm)
            {
                //check for securities which have active targets, but do not have new targets
                //add these securities with targets of 0 to new targets unless there is existing
                //target of 0 in active targets
                _newTargets.AddRange(from at in _activeTargets
                                     where !_newTargets.Select(x => x.Target.Symbol.Value).Contains(at.Target.Symbol.Value) &&
                                     at.Target.Quantity != 0
                                     select (new PortfolioTargetTracking(algorithm, at.Target.Symbol, 0)));
            }

            public void UpdateNewTargetsWithIsSubmitted()
            {
                foreach (PortfolioTargetTracking ptt in _newTargets)
                {
                    ptt.IsSubmitted = true;
                }
            }

            public void UpdateActiveTargetsWithNewTargets()
            {
                //if active quantity of active and new target is the same then select the active target, but
                //if quantity is different than select the new target
                _activeTargets =
                    (from nt in _newTargets
                     join at in _activeTargets on nt.Target.Symbol.Value equals at.Target.Symbol.Value into ant
                     from at in ant.DefaultIfEmpty()
                     select (at != null && at.Target.Quantity == nt.Target.Quantity) ? at : nt)
                     .ToList<PortfolioTargetTracking>();
            }

            public Int32 GetOrderQuantityForPortfolioTarget(QCAlgorithmFramework algorithm, PortfolioTarget target)
            {
                return (target.Quantity > 0 || target.Quantity < 0) ?
                        Convert.ToInt32(target.Quantity - algorithm.Securities[target.Symbol.Value].Holdings.Quantity) :
                        Convert.ToInt32(-1 * algorithm.Securities[target.Symbol.Value].Holdings.Quantity);
            }

            public class PortfolioTargetTracking
            {
                private PortfolioTarget _target;
                private bool _isSubmitted;
                private List<Orders.OrderEvent> _orderEvents = new List<Orders.OrderEvent>();
                //private Decimal _totalCost = -9999999999m;
                private Decimal _orderQuantity = 0;

                public PortfolioTargetTracking(QCAlgorithmFramework algorithm, Symbol symbol, Decimal quantity)
                    : this(algorithm, symbol, quantity, false) {}

                public PortfolioTargetTracking(QCAlgorithmFramework algorithm, Symbol symbol, Decimal quantity, bool isSubmitted)
                {
                    _target = new PortfolioTarget(symbol, quantity);

                    _isSubmitted = isSubmitted;

                    _orderQuantity = (quantity > 0 || quantity < 0) ?
                        quantity - algorithm.Securities[symbol.Value].Holdings.Quantity :
                        -1m * algorithm.Securities[symbol.Value].Holdings.Quantity;
                }

                public bool IsSubmitted { get { return _isSubmitted; } set { _isSubmitted = value; } }

                /*public Decimal GetCurrentValueOfHoldingInPortfolio(QCAlgorithmFramework algorithm)
                {
                    Orders.Order o = (IsOrderEventAvailable()) ? algorithm.Transactions.GetOrderById(GetLastOrderEvent().OrderId) : null;

                    return
                            (IsOrderEventAvailable())?o.GetValue(algorithm.Securities[_target.Symbol]):0m;
                }*/

                public PortfolioTarget Target { get { return _target; } }

                /*public Decimal TotalCost
                {
                    get
                    {
                        return (_totalCost != -9999999999m) ?
                          _totalCost :
                          0;
                    }
                }*/

                /*public void AddOrderEvent(QCAlgorithmFramework algorithm, Orders.OrderEvent orderEvent)
                {
                    _orderEvents.Add(orderEvent);

                    if (orderEvent.Status == Orders.OrderStatus.Filled || orderEvent.Status == Orders.OrderStatus.Canceled)
                    {
                        _totalCost =
                            algorithm.Transactions.GetOrderById(orderEvent.OrderId).Value +
                            orderEvent.OrderFee;
                    }
                    else if (orderEvent.Status == Orders.OrderStatus.PartiallyFilled)
                    {
                        _totalCost +=
                            orderEvent.FillPrice * orderEvent.FillQuantity +
                            orderEvent.OrderFee;
                    }
                }*/

                /*public Orders.OrderEvent GetLastOrderEvent()
                {
                    return 
                        (from oe in _orderEvents
                         orderby oe.UtcTime descending
                         select oe)
                         .Take<Orders.OrderEvent>(1)
                         .Single<Orders.OrderEvent>();
                }*/

                /*public bool IsOrderEventAvailable()
                {
                    return
                        (_orderEvents.Count > 0) ? true : false;
                }*/

                public Decimal OrderQuantity
                {
                    get { return _orderQuantity; }
                }
            }
        }
    }
}