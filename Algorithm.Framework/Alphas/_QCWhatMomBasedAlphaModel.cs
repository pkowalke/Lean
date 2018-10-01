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

using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Alpha model that uses historical returns to create insights
    /// </summary>
    public class _QCWhatMomBasedAlphaModel : AlphaModel
    {
        private IDictionary<String, String> _alphaUniverse;

        private Dictionary<Symbol, SymbolData> _SymbolDataBySymbol;

        private List<Insight> _previousInsights = new List<Insight>();

        private int _momentumPeriod;
        private Resolution _momentumResolution;
        private Resolution _resolution;
        private readonly int _didNotGetDataTimesThreshold = 6; //this is set only in here, not carried over from Initialize
        private readonly int _exchangeClosedThreshold = 6; //this is set only in here, not carried over from Initialize

        private Scheduling.IDateRule _rebalanceDate;
        private Scheduling.ITimeRule _rebalanceTime;        

        public _QCWhatMomBasedAlphaModel(IDictionary<String, String> alphaUniverse, String alphaModelName, int momentumPeriod, Resolution momentumResolution, Resolution resolution, Scheduling.IDateRule rebalanceDate, Scheduling.ITimeRule rebalanceTime)
        {
            _alphaUniverse = alphaUniverse;

            if (momentumResolution != Resolution.Daily) throw new NotImplementedException("Resolution of momentum must be Daily.");
            if (resolution != Resolution.Minute) throw new NotImplementedException("Resolution must be minute.");
            Name = alphaModelName;
            _momentumPeriod = momentumPeriod;
            _momentumResolution = momentumResolution; // must be daily
            _resolution = resolution; //must be minute
            _rebalanceDate = rebalanceDate;
            _rebalanceTime = rebalanceTime;
            _SymbolDataBySymbol = new Dictionary<Symbol, SymbolData>();
        }

        public IEnumerable<Insight> UpdateMomBasedInsights(QCAlgorithmFramework algorithm, Slice data)
        {
            List<String> stocksWithData = new List<string>();
            List<String> stocksWithoutData = new List<string>();
            List<String> stocksWithExchangeOpen = new List<string>();
            List<String> stocksWithExchangeClosed = new List<string>();
            List<String> stocksNotConsolidated = new List<string>();

            List<String> stocksWithoutDataBlacklist = new List<string>();
            List<String> stocksWithExchangeClosedBlacklist = new List<string>();

            foreach (SymbolData sd in _SymbolDataBySymbol.Values)
            {
                // Just in case (taught by problems with scheduler firing events at the right time in backtest)
                // scan the consolidator for changes.
                sd.ScanUpdateConsolidator();

                // Mark as missing data where consolidator did not consolidate on the day following previous trading day
                if (sd.TBConsolidator.Consolidated.EndTime.Date == sd.Security.Exchange.Hours.GetPreviousTradingDay(algorithm.Time.Date).AddDays(1).Date)
                {
                    stocksWithData.Add(sd.Security.Symbol.Value);
                    sd.ConsecutiveMissingData = 0;
                }
                else
                {
                    stocksWithoutData.Add(sd.Security.Symbol.Value);
                    sd.ConsecutiveMissingData++;

                    if (sd.ConsecutiveMissingData > _didNotGetDataTimesThreshold)
                    {
                        stocksWithoutDataBlacklist.Add(sd.Security.Symbol.Value);

                        algorithm.Debug(String.Format(
                            "Time: {0}. Stock: {1} hitting maximum consecutive runs without data ({2}). Data missing {3} times.",
                            algorithm.Time.ToString(),
                            sd.Security.Symbol.Value,
                            _didNotGetDataTimesThreshold.ToString(),
                            sd.ConsecutiveMissingData.ToString()
                            ));
                    }
                }

                // Mark as closed exchange if particular symbol's exchange happens to be closed.
                if (sd.Security.Exchange.DateIsOpen(algorithm.Time.Date))
                {
                    stocksWithExchangeOpen.Add(sd.Security.Symbol.Value);
                    sd.ConsecutiveExchangeClosed = 0;
                }
                else
                {
                    stocksWithExchangeClosed.Add(sd.Security.Symbol.Value);
                    sd.ConsecutiveExchangeClosed++;

                    if (sd.ConsecutiveExchangeClosed > _exchangeClosedThreshold)
                    {
                        stocksWithExchangeClosedBlacklist.Add(sd.Security.Symbol.Value);

                        algorithm.Debug(String.Format(
                            "Time: {0}. Stock: {1} hitting maximum consecutive runs with closed Exchange ({2}). Occured {3} times.",
                            algorithm.Time.ToString(),
                            sd.Security.Symbol.Value,
                            _exchangeClosedThreshold.ToString(),
                            sd.ConsecutiveExchangeClosed.ToString()
                            ));
                    }
                }

                // Mark as not consolidated if the initial consolidation did not occur
                if (!sd.IsReady())
                {
                    stocksNotConsolidated.Add(sd.Security.Symbol.Value);
                }

                // Print out stocks that were "blacklisted" due to not having data or having exchange closed
                // too many consecutive times
                if (stocksWithoutDataBlacklist.Contains(sd.Security.Symbol.Value))
                {
                    algorithm.Error(String.Format(
                            "Time: {0}. Stock: {1} has been blacklisted for not having new data!",
                            algorithm.Time.ToString(),
                            sd.Security.Symbol.Value
                            ));
                }

                if (stocksWithExchangeClosedBlacklist.Contains(sd.Security.Symbol.Value))
                {
                    algorithm.Error(String.Format(
                            "Time: {0}. Stock: {1} has been blacklisted for its exchange being closed during runs!",
                            algorithm.Time.ToString(),
                            sd.Security.Symbol.Value
                            ));
                }
            }

            //if ALL stocks are blacklisted for not having new data or their exchange being closed
            //then return no insights.
            if (stocksNotConsolidated.Count == _SymbolDataBySymbol.Count ||
                stocksWithoutData.Count == _SymbolDataBySymbol.Count ||
                stocksWithExchangeClosed.Count == _SymbolDataBySymbol.Count)
            {
                algorithm.Error(String.Format(
                    "Time: {0}. All stocks were not consolidated ({1}) or all without data ({2}) or all with exchange not open ({3}).",
                    algorithm.Time.ToString(),
                    stocksNotConsolidated.Count.ToString(),
                    stocksWithoutData.Count.ToString(),
                    stocksWithExchangeClosed.Count.ToString()
                    ));

                if (stocksWithoutDataBlacklist.Count == _SymbolDataBySymbol.Count ||
                    stocksWithExchangeClosedBlacklist.Count == _SymbolDataBySymbol.Count)
                {
                    algorithm.Error(String.Format(
                    "Time: {0}. Threshhold reached for all stocks! I will return no new insights! All stocks were not consolidated ({1}) or all without data ({2}) or all with exchange not open ({3}).",
                    algorithm.Time.ToString(),
                    stocksNotConsolidated.Count.ToString(),
                    stocksWithoutData.Count.ToString(),
                    stocksWithExchangeClosed.Count.ToString()
                    ));

                    // if all stocks had exchange closed or no data too many times, than return no insights
                    _previousInsights = new List<Insight>();
                    return _previousInsights;
                }

                // i f all stocks had exchange closed or no data but threshold was not met for all than return previous insights
                return _previousInsights;
            }


            // carry over insights that have exchange closed or missing data or did not consolidate, but did not hit threshold in that
            List<Insight> closedExchOrNoDataOrNoCons =
                ((from pi1 in _previousInsights
                 join closedExch in stocksWithExchangeClosed
                 on pi1.Symbol.Value equals closedExch
                 select pi1)
                 .Union(
                 (from pi2 in _previousInsights
                  join noData in stocksWithoutData
                  on pi2.Symbol.Value equals noData
                  select pi2))
                  .Union(
                    (from pi3 in _previousInsights
                    join noCons in stocksNotConsolidated
                    on pi3.Symbol.Value equals noCons
                    select pi3)))
                    .Distinct()
                  .ToList();

            List<Insight> insightsToBeCarriedOver =
                (from ceondonc in closedExchOrNoDataOrNoCons
                 join sd in _SymbolDataBySymbol.Values
                 on ceondonc.Symbol.Value equals sd.Security.Symbol.Value
                 where sd.ConsecutiveExchangeClosed <= _exchangeClosedThreshold &&
                 sd.ConsecutiveMissingData <= _didNotGetDataTimesThreshold
                 select ceondonc)
                 .ToList();

            //new insights created for stocks with open exchange, data, that consolidated, that have SymbolData ready,
            //that have positive price in the consolidator
            List<Insight> insightsNew =
                (from sd in _SymbolDataBySymbol.Values
                 join inData in stocksWithData
                 on sd.Security.Symbol.Value equals inData
                 join openExch in stocksWithExchangeOpen
                 on sd.Security.Symbol.Value equals openExch
                 where
                 !stocksNotConsolidated.Contains(sd.Security.Symbol.Value) &&
                 sd.IsReady() &&
                 sd.TBConsolidator.Consolidated.Price > 0
                 orderby sd.MOM descending
                 select Insight.Price(
                     sd.Security.Symbol,
                     new DateTime(algorithm.Time.Year, algorithm.Time.Month, algorithm.Time.Day, 23, 59, 59, 999),
                     (sd.MOM > 0) ? InsightDirection.Up : (sd.MOM < 0) ? InsightDirection.Down : InsightDirection.Flat,
                     Math.Abs((double)(100 * (sd.MOM / _momentumPeriod))),
                     null,
                     Name))
                .ToList();

            _previousInsights = new List<Insight>();
            _previousInsights.AddRange(insightsToBeCarriedOver);
            _previousInsights.AddRange(insightsNew);

            algorithm.Error(String.Format("Time: {0}. Returning {1} insights for {2} symbols.",
                algorithm.Time.ToString(),
                _previousInsights.Count.ToString(),
                _SymbolDataBySymbol.Count.ToString()));

            return _previousInsights;
        }

        /// <summary>
        /// Updates this alpha model with the latest data from the algorithm.
        /// 
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="data">The new data available</param>
        /// <returns>The new insights generated</returns>
        public override IEnumerable<Insight> Update(QCAlgorithmFramework algorithm, Slice data)
        {
            // if update was called by scheduler at is rebalance time
            if (_rebalanceDate.GetDates(algorithm.Time.Date, algorithm.Time.Date).Count() == 1 &&
                    _rebalanceTime.CreateUtcEventTimes( new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(h => h.Hour).Contains(algorithm.Time.Hour) &&
                    _rebalanceTime.CreateUtcEventTimes( new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(m => m.Minute).Contains(algorithm.Time.Minute))
            {
                return UpdateMomBasedInsights(algorithm, data).ToList();
            }
            else
                return new List<Insight>();
        }

        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            String universeName =
                (from kvp in _alphaUniverse
                 where kvp.Key == Name
                 select kvp.Value)
                 .Single();

            IReadOnlyList < Security > removed =
                (from c in changes.RemovedSecurities
                 join us in algorithm.UniverseManager[universeName].Members
                 on c.Symbol equals us.Key
                 select c)
                 .ToList<Security>();

            IReadOnlyList<Security> added =
                (from c in changes.AddedSecurities
                 join us in algorithm.UniverseManager[universeName].Members
                 on c.Symbol equals us.Key
                 select c)
                 .ToList<Security>();

            // clean up data for removed securities
            foreach (var r in removed)
            {
                SymbolData data;
                if (_SymbolDataBySymbol.TryGetValue(r.Symbol, out data))
                {
                    _SymbolDataBySymbol.Remove(r.Symbol);
                    algorithm.SubscriptionManager.RemoveConsolidator(r.Symbol, data.TBConsolidator);
                }
            }

            // initialize data for added securities
            var addedSymbols = new List<Symbol>();
            foreach (var a in added)
            {
                if (!_SymbolDataBySymbol.ContainsKey(a.Symbol))
                {
                    var symbolData = new SymbolData(algorithm, a, _momentumPeriod, _momentumResolution);
                    _SymbolDataBySymbol[a.Symbol] = symbolData;
                    addedSymbols.Add(symbolData.Security.Symbol);
                }
            }

            if (addedSymbols.Count > 0)
            {
                //warmup our indicators by pushing history through the consolidators
                algorithm.History(addedSymbols, _momentumPeriod+1, _momentumResolution)
                .PushThrough(bar =>
                {
                    SymbolData symbolData;
                    if (_SymbolDataBySymbol.TryGetValue(bar.Symbol, out symbolData))
                    {
                        symbolData.TBConsolidator.Update(bar);
                    }
                });
            }
        }

        /// <summary>
        /// Contains data specific to a symbol required by this model
        /// </summary>
        private class SymbolData
        {
            QCAlgorithmFramework Algorithm;
            public Security Security;
            public RollingWindow<TradeBar> RollingWindow;
            public TradeBarConsolidator TBConsolidator;
            public Decimal MOM;
            public int ConsecutiveMissingData;
            public int ConsecutiveExchangeClosed;

            public SymbolData(QCAlgorithmFramework algorithm, Security security, int momentumPeriod, Resolution momentumResolution)
            {
                Algorithm = algorithm;

                Security = security;

                RollingWindow = new RollingWindow<TradeBar>(momentumPeriod);

                MOM = -9999999999m;

                TBConsolidator = new TradeBarConsolidator(TimeSpan.FromDays(1));

                TBConsolidator.DataConsolidated += (sender, consolidated) =>
                {
                    RollingWindow.Add(consolidated);

                    if (RollingWindow.IsReady)
                    {
                        MOM = (RollingWindow[0].Close - RollingWindow[momentumPeriod - 1].Close) / RollingWindow[momentumPeriod - 1].Close;
                    }
                    else
                    {
                        MOM = -9999999999m;
                    }
                };

                algorithm.SubscriptionManager.AddConsolidator(security.Symbol, TBConsolidator);

                //setup consolidator to manually check for readiness (new data) every morning 8 hours before market open for the symbol
                algorithm.Schedule.On(algorithm.DateRules.EveryDay(), algorithm.TimeRules.AfterMarketOpen(Security.Symbol, -240, false), ScanUpdateConsolidator);

                ConsecutiveMissingData = 0;
                ConsecutiveExchangeClosed = 0;
            }

            public bool IsReady()
            {
                return (RollingWindow.IsReady &&
                    MOM != -9999999999m) ? true : false;
            }

            public void ScanUpdateConsolidator()
            {
                TBConsolidator.Scan(Algorithm.Time);
            }
        }
    }
}