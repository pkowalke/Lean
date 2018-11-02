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

    public class _QCWhatPortfolioConstrModel : PortfolioConstructionModel
    {
        private List<Symbol> _removedSymbols;

        private IDictionary<String, String> _alpha_universe;

        private List<AlphaModelData> _alphaModels = new List<AlphaModelData>();

        private String _previouslyUsedAlphaModel = "";

        private Decimal _currentCash;

        private Scheduling.IDateRule _rebalanceDate;
        private Scheduling.ITimeRule _rebalanceTime;

        private bool _Mom_Based_Alpha_initialized = false;
        private String _Mom_Based_Alpha_Name = "";
        private Int32 _Mom_Based_Alpha_DefaultNumberOfTargets = 0;

        private bool _SafeHeaven_Alpha_initialized = false;
        private String _SafeHeaven_Alpha_Name = "";
        private Int32 _SafeHeaven_Alpha_DefaultNumberOfTargets = 0;

        //Methods and properties

        public _QCWhatPortfolioConstrModel(IDictionary<String, String> alphaUniverse, Decimal startingCash, Scheduling.IDateRule rebalanceDate, Scheduling.ITimeRule rebalanceTime)
        {
            _alpha_universe = alphaUniverse;

            _currentCash = startingCash;

            _rebalanceDate = rebalanceDate;

            _rebalanceTime = rebalanceTime;
        }

        public void InitMomBasedPortfolioConstruction(QCAlgorithmFramework algorithm, String alphaModelName, Int32 defaultNumberOfTargets)
        {
            _Mom_Based_Alpha_Name = alphaModelName;

            _alphaModels.Add(
                new AlphaModelData(algorithm, alphaModelName)
                );

            _Mom_Based_Alpha_DefaultNumberOfTargets = defaultNumberOfTargets;

            _Mom_Based_Alpha_initialized = true;
        }

        public void InitSaveHeavenConstantBasedPortfolioConstruction(QCAlgorithmFramework algorithm, String alphaModelName, Int32 defaultNumberOfTargets)
        {
            _SafeHeaven_Alpha_Name = alphaModelName;

            _alphaModels.Add(
                new AlphaModelData(algorithm, alphaModelName)
                );

            _SafeHeaven_Alpha_DefaultNumberOfTargets = defaultNumberOfTargets;

            _SafeHeaven_Alpha_initialized = true;
        }

        public IEnumerable<IPortfolioTarget> CreateTargetsMomBased(QCAlgorithmFramework algorithm, Insight[] insights, Int32 numberOfTargets, Decimal targetHoldings, Decimal minimumMagnitude)
        {
            List<PortfolioTarget> targets = new List<PortfolioTarget>();

            // if no new insights than return nothing new
            if (insights.Length == 0)
            {
                return targets;
            }

            AlphaModelData amd = _alphaModels.Single<AlphaModelData>(x => x.Name == _Mom_Based_Alpha_Name);

            // step 1 update active insights with new insights for this alpha model
            //amd._1_UpdateActiveInsights(insights);
            amd.ActiveTargets.RemoveAll(x => (from ii in insights join ams in amd.AlphaModelSecurities on ii.Symbol equals ams.Symbol select ii.Symbol).Contains(x.Symbol));
            amd.ActiveInsights.AddRange(from ii in insights join ams in amd.AlphaModelSecurities on ii.Symbol equals ams.Symbol select ii);

            // step 2 remove insights that are expired and the exchange was open for those securities since expired
            //amd._2_PruneExpiredInsightsForWhichExchangeWasOpen(algorithm);
            amd.ActiveInsights.RemoveAll(x =>
                (from ai in amd.ActiveInsights
                 where ai.IsExpired(algorithm.UtcTime) &&
                 algorithm.Securities[ai.Symbol.Value].Exchange.Hours.GetNextMarketOpen(ai.CloseTimeUtc.ConvertFromUtc(algorithm.TimeZone), extendedMarket: false).CompareTo(algorithm.Time) < 0
                 select ai).Contains(x));

            // step 3 create new targets based on specification for this alpha model
            //amd._3_UpdateNewTargets(algorithm, numberOfTargets, targetHoldings);
            bool QuantitiesOverZero = false;

            for (int j = 0; !QuantitiesOverZero; j++)
            {
                algorithm.Debug(String.Format("Time: {0}. Attempting to create list of new targets with count: {1}.",
                        algorithm.Time.ToString(),
                        (numberOfTargets - j).ToString()));

                targets =
                (from ai in amd.ActiveInsights
                 where ai.Magnitude > 0 &&
                 ai.Direction == InsightDirection.Up &&
                 ai.Magnitude >= (double)minimumMagnitude
                 orderby ai.Magnitude descending
                 select new PortfolioTarget(ai.Symbol, Math.Floor(((targetHoldings / (numberOfTargets - j)) - 5) / algorithm.Securities[ai.Symbol.Value].Close)))
                     .Take<PortfolioTarget>(numberOfTargets - j)
                     .ToList<PortfolioTarget>();

                List<PortfolioTarget> newTargetsWithQuantityLessThanOne = targets.Where(x => x.Quantity < 1m).Select(y => y).ToList();

                if (newTargetsWithQuantityLessThanOne.Count > 0)
                {
                    algorithm.Error(String.Format("Time: {0}. {1} of symbols added to new targets had quantity less than 1. Try with less targets?",
                        algorithm.Time.ToString(),
                        newTargetsWithQuantityLessThanOne.Count.ToString()));

                    targets = new List<PortfolioTarget>();
                }

                else if (targets.Count == numberOfTargets - j)
                {
                    if (targets.Count == 0)
                    {
                        algorithm.Error(String.Format("Time: {0}. Can't add even single share of top pick in the list.",
                        algorithm.Time.ToString()));

                        targets = new List<PortfolioTarget>();

                        break;
                    }

                    algorithm.Debug(String.Format("Time: {0}. Adding {1} symbols to new targets.",
                        algorithm.Time.ToString(),
                        targets.Count.ToString()));

                    QuantitiesOverZero = true;
                }

                else if (targets.Count < numberOfTargets - j)
                {
                    if (targets.Count == 0)
                    {
                        algorithm.Error(String.Format("Time: {0}. All potential targets have negative insight direction. Liquidating!",
                        algorithm.Time.ToString()));

                        targets = new List<PortfolioTarget>();

                        break;
                    }

                    algorithm.Error(String.Format("Time: {0}. There are not enough insights with positive direction to create requested number of targets. Try with less targets?",
                        algorithm.Time.ToString()));
                }
            }

            // step 4 add flat targets for securities invested currently in this alpha model
            //amd._x_UpdateNewTargetsWithFlatTargets(algorithm);
            targets.AddRange(from s in amd.AlphaModelSecurities
                             where !targets.Select(x => x.Symbol.Value).Contains(s.Symbol.Value) &&
                             s.Holdings.Quantity != 0
                             select (new PortfolioTarget(s.Symbol, 0)));

            // step 5 update active targets with new targets
            //amd._4_UpdateActiveTargetsWithNewTargets();
            amd.ActiveTargets =
                    (from nt in targets
                     join at in amd.ActiveTargets on nt.Symbol.Value equals at.Symbol.Value into ant
                     from at in ant.DefaultIfEmpty()
                     select (at != null && at.Quantity == nt.Quantity) ? at : nt)
                     .ToList<PortfolioTarget>();

            targets = amd.ActiveTargets;

            return targets;
        }

        public IEnumerable<IPortfolioTarget> CreateTargetsSafeHeaven(QCAlgorithmFramework algorithm, Insight[] insights, Int32 numberOfTargets, Decimal targetHoldings)
        {
            List<PortfolioTarget> targets = new List<PortfolioTarget>();

            // if no new insights than return nothing new
            if (insights.Length == 0)
            {
                return targets;
            }

            AlphaModelData amd = _alphaModels.Single<AlphaModelData>(x => x.Name == _SafeHeaven_Alpha_Name);

            // step 1 update active insights with new insights for this alpha model
            //amd._1_UpdateActiveInsights(insights);
            amd.ActiveTargets.RemoveAll(x => (from ii in insights join ams in amd.AlphaModelSecurities on ii.Symbol equals ams.Symbol select ii.Symbol).Contains(x.Symbol));
            amd.ActiveInsights.AddRange(from ii in insights join ams in amd.AlphaModelSecurities on ii.Symbol equals ams.Symbol select ii);

            // step 2 remove insights that are expired and the exchange was open for those securities since expired
            //amd._2_PruneExpiredInsightsForWhichExchangeWasOpen(algorithm);
            amd.ActiveInsights.RemoveAll(x =>
                (from ai in amd.ActiveInsights
                 where ai.IsExpired(algorithm.UtcTime) &&
                 algorithm.Securities[ai.Symbol.Value].Exchange.Hours.GetNextMarketOpen(ai.CloseTimeUtc.ConvertFromUtc(algorithm.TimeZone), extendedMarket: false).CompareTo(algorithm.Time) < 0
                 select ai).Contains(x));

            // step 3 create new targets based on specification for this alpha model
            //amd._3_UpdateNewTargets(algorithm, numberOfTargets, targetHoldings);
            bool QuantitiesOverZero = false;

            for (int j = 0; !QuantitiesOverZero; j++)
            {
                algorithm.Debug(String.Format("Time: {0}. Attempting to create list of new targets with count: {1}.",
                        algorithm.Time.ToString(),
                        (numberOfTargets - j).ToString()));

                targets =
                (from ai in amd.ActiveInsights
                 where ai.Direction == InsightDirection.Flat
                 select new PortfolioTarget(ai.Symbol, Math.Floor(((targetHoldings / (numberOfTargets - j)) - 5) / algorithm.Securities[ai.Symbol.Value].Close)))
                     .Take<PortfolioTarget>(numberOfTargets - j)
                     .ToList<PortfolioTarget>();

                List<PortfolioTarget> newTargetsWithQuantityLessThanOne = targets.Where(x => x.Quantity < 1m).Select(y => y).ToList();

                if (newTargetsWithQuantityLessThanOne.Count > 0)
                {
                    algorithm.Error(String.Format("Time: {0}. {1} of symbols added to new targets had quantity less than 1. Try with less targets?",
                        algorithm.Time.ToString(),
                        newTargetsWithQuantityLessThanOne.Count.ToString()));

                    targets = new List<PortfolioTarget>();
                }

                else if (targets.Count == numberOfTargets - j)
                {
                    if (targets.Count == 0)
                    {
                        algorithm.Error(String.Format("Time: {0}. Can't add even single share of top pick in the list.",
                        algorithm.Time.ToString()));

                        targets = new List<PortfolioTarget>();

                        break;
                    }

                    algorithm.Debug(String.Format("Time: {0}. Adding {1} symbols to new targets.",
                        algorithm.Time.ToString(),
                        targets.Count.ToString()));

                    QuantitiesOverZero = true;
                }

                else if (targets.Count < numberOfTargets - j)
                {
                    if (targets.Count == 0)
                    {
                        algorithm.Error(String.Format("Time: {0}. All potential targets have negative insight direction. Liquidating!",
                        algorithm.Time.ToString()));

                        targets = new List<PortfolioTarget>();

                        break;
                    }

                    algorithm.Error(String.Format("Time: {0}. There are not enough insights with positive direction to create requested number of targets. Try with less targets?",
                        algorithm.Time.ToString()));
                }
            }

            // step 4 add flat targets for securities invested currently in this alpha model
            //amd._x_UpdateNewTargetsWithFlatTargets(algorithm);
            targets.AddRange(from s in amd.AlphaModelSecurities
                             where !targets.Select(x => x.Symbol.Value).Contains(s.Symbol.Value) &&
                             s.Holdings.Quantity != 0
                             select (new PortfolioTarget(s.Symbol, 0)));

            // step 5 update active targets with new targets
            //amd._4_UpdateActiveTargetsWithNewTargets();
            amd.ActiveTargets =
                    (from nt in targets
                     join at in amd.ActiveTargets on nt.Symbol.Value equals at.Symbol.Value into ant
                     from at in ant.DefaultIfEmpty()
                     select (at != null && at.Quantity == nt.Quantity) ? at : nt)
                     .ToList<PortfolioTarget>();

            targets = amd.ActiveTargets;

            return targets;
        }

        public override IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithmFramework algorithm, Insight[] insights)
        {
            List<IPortfolioTarget> targets = new List<IPortfolioTarget>();

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

            if (_rebalanceDate.GetDates(algorithm.Time.Date, algorithm.Time.Date).Count() == 1 &&
                    _rebalanceTime.CreateUtcEventTimes(new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(h => h.Hour).Contains(algorithm.Time.Hour) &&
                    _rebalanceTime.CreateUtcEventTimes(new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(m => m.Minute).Contains(algorithm.Time.Minute))
            {
                algorithm.Debug(String.Format("Time: {0}. * PRIOR TO REBALANCE * Cash: {1}, Holdings {2}, Total {3}.",
                        algorithm.Time.ToString(),
                        _currentCash.ToString(),
                        algorithm.Securities.Values.Sum(x => x.Holdings.HoldingsValue).ToString(),
                        (algorithm.Securities.Values.Sum(x => x.Holdings.HoldingsValue) + _currentCash).ToString()
                        ));

                //in the future there will be mechanism here to call the methods returning targets for alphas with correct
                //number of stocks that are expected. this is fixed number for now. Similarly for target holdings for alpha.
                IPortfolioTarget[] MomBasedTargets =
                    CreateTargetsMomBased(
                    algorithm,
                    insights,
                    (_currentCash > 10000) ? (int)Math.Floor(((algorithm.Securities.Values.Sum(x => x.Holdings.HoldingsValue) + _currentCash) * _Mom_Based_Alpha_DefaultNumberOfTargets) / 10000m) : _Mom_Based_Alpha_DefaultNumberOfTargets,
                    (algorithm.Securities.Values.Sum(x => x.Holdings.HoldingsValue) + _currentCash),
                    0.05m // 0.025%
                    )
                    .ToArray();

                /*IPortfolioTarget [] SafeHeavenTargets = 
                     CreateTargetsSafeHeaven(
                    algorithm,
                    insights,
                    _SafeHeaven_Alpha_DefaultNumberOfTargets,
                    (algorithm.Securities.Values.Sum(x => x.Holdings.HoldingsValue) + _currentCash)
                    )
                    .ToArray();*/

                targets.AddRange(
                        MomBasedTargets
                        );

                /*if (MomBasedTargets.Count() > _Mom_Based_Alpha_DefaultNumberOfTargets - 4)
                {
                    

                    if (_previouslyUsedAlphaModel != _Mom_Based_Alpha_Name)
                    {
                        targets.AddRange(
                            from s in algorithm.Securities.Values
                            where !targets.Select(x => x.Symbol).Contains(s.Symbol) &&
                            s.Holdings.Quantity != 0
                            select (new PortfolioTarget(s.Symbol, 0)));
                    }

                    _previouslyUsedAlphaModel = _Mom_Based_Alpha_Name;
                }
                else
                {
                    targets.AddRange(
                        SafeHeavenTargets
                        );

                    if (_previouslyUsedAlphaModel != _SafeHeaven_Alpha_Name)
                    {
                        targets.AddRange(
                            from s in algorithm.Securities.Values
                            where !targets.Select(x => x.Symbol).Contains(s.Symbol) &&
                            s.Holdings.Quantity != 0
                            select (new PortfolioTarget(s.Symbol, 0)));
                    }

                    _previouslyUsedAlphaModel = _SafeHeaven_Alpha_Name;
                }*/



            }

            return targets;
        }

        public void AddOrderEvent(QCAlgorithmFramework algorithm, Orders.OrderEvent orderEvent)
        {
            Orders.OrderTicket ot = algorithm.Transactions.GetOrderTicket(orderEvent.OrderId);

            Orders.OrderEvent[] oes = ot.OrderEvents.OrderByDescending(x => x.UtcTime).ToArray();

            Orders.OrderEvent[] previousPartialFills = new Orders.OrderEvent[] { };

            for (int i = 0; i < oes.Count(); i++)
            {
                if (oes[i].Status == Orders.OrderStatus.PartiallyFilled &&
                    oes[i] != orderEvent &&
                    oes[i].UtcTime < orderEvent.UtcTime)
                {
                    previousPartialFills = previousPartialFills.Union(new Orders.OrderEvent[1] { oes[i] }).OrderByDescending(x => x.UtcTime).ToArray();
                }
            }

            //if partial fill, apply the current partial fill don't add fee yet
            if (orderEvent.Status == Orders.OrderStatus.PartiallyFilled)
            {
                _currentCash += -1m * orderEvent.FillQuantity * orderEvent.FillPrice;
            }

            //if canceled, just apply order fee
            if (orderEvent.Status == Orders.OrderStatus.Canceled)
            {
                _currentCash += -1m * orderEvent.OrderFee;
            }

            //if filled, add previous partial fills, retract them and apply total order value with fee
            if (orderEvent.Status == Orders.OrderStatus.Filled)
            {
                _currentCash += previousPartialFills.Sum(x => x.FillQuantity * x.FillPrice);

                _currentCash += -1m * (ot.QuantityFilled * ot.AverageFillPrice) - orderEvent.OrderFee;
            }
        }

        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            // Get removed symbol and invalidate them in the insight collection and remove active targets with them
            _removedSymbols = changes.RemovedSecurities.Select(x => x.Symbol).ToList();

            foreach (AlphaModelData pcamd in _alphaModels)
            {
                foreach (Insight i in pcamd.ActiveInsights) if (_removedSymbols.Select(s => s.Value).Contains(i.Symbol.Value)) pcamd.ActiveInsights.ToList().Remove(i);
                foreach (PortfolioTarget pt in pcamd.ActiveTargets) if (_removedSymbols.Select(s => s.Value).Contains(pt.Symbol.Value)) pcamd.ActiveTargets.ToList().Remove(pt);

                pcamd.AlphaModelSecurities =
                    (from us in algorithm.UniverseManager[_alpha_universe[pcamd.Name]].Members.Values
                     join ps in algorithm.Securities.Values
                     on us.Symbol equals ps.Symbol
                     select ps)
                    .ToArray<Securities.Security>();
            }
        }

        public class AlphaModelData
        {
            private readonly String _alphaName = "";

            private Securities.Security[] _alphaModelSecurities;

            private List<Insight> _activeInsights = new List<Insight>();

            private List<PortfolioTarget> _activeTargets = new List<PortfolioTarget>();

            /// <summary>
            /// Create new data for alpha model.
            /// </summary>
            /// <param name="algorithm">The algorithm framework object</param>
            /// <param name="alphaUniverse">The dictionary containing alpha name - universe kvp</param>
            /// <param name="alphaName">The name of the alpha</param>
            /// <param name="minimumMagnitude">The minimum value of the magnitude to include in the portfolio targets (value = 10 means 10% daily increase prognosis)</param>
            public AlphaModelData(QCAlgorithmFramework algorithm, String alphaName)
            {
                _alphaName = alphaName;
            }

            public List<PortfolioTarget> ActiveTargets
            {
                get { return _activeTargets; }

                set { _activeTargets = value; }
            }

            public List<Insight> ActiveInsights
            {
                get { return _activeInsights; }
            }

            public Securities.Security[] AlphaModelSecurities
            {
                get { return _alphaModelSecurities; }
                set { _alphaModelSecurities = value; }
            }

            public String Name { get { return _alphaName; } }
        }
    }
}