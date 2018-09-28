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
    public class _Mom_Based_Rotation_AM : AlphaModel
    {
        IDictionary<String, String> _alpha_universe;

        private List<Insight> _previousInsights = new List<Insight>();

        //variables specific to Mom_Based_Alpha
        private bool _Mom_Based_Alpha_initialized = false;
        private int _Mom_Based_Alpha_momentumPeriod;
        private Resolution _Mom_Based_Alpha_momentumResolution;
        private Resolution _Mom_Based_Alpha_resolution;
        private Dictionary<Symbol, SymbolData> _Mom_Based_Alpha_SymbolDataBySymbol;
        private List<Insight> _Mom_Based_Alpha_previousInsights = new List<Insight>();
        private Scheduling.IDateRule _Mom_Based_Alpha_rebalanceDate;
        private Scheduling.ITimeRule _Mom_Based_Alpha_rebalanceTime;

        private readonly int _didNotGetDataTimesThreshold = 6;
        private readonly int _exchangeClosedThreshold = 6;

        public _Mom_Based_Rotation_AM(IDictionary<String, String> alphaUniverse)
        {
            _alpha_universe = alphaUniverse;

            Name = $"{nameof(_Mom_Based_Rotation_AM)}";

            Name +=
                String.Concat(
                (from au in _alpha_universe
                 select String.Format("*{0}-{1}*", au.Key, au.Value))
                .ToArray());
        }

        public bool IsInitializedMomBased()
        {
            return _Mom_Based_Alpha_initialized;
        }

        public void InitMomBasedInsights(int momentumPeriod, Resolution momentumResolution, Resolution resolution, Scheduling.IDateRule rebalanceDate, Scheduling.ITimeRule rebalanceTime)
        {
            if (momentumResolution != Resolution.Daily) throw new NotImplementedException("Resolution of momentum must be Daily.");
            if (resolution != Resolution.Minute) throw new NotImplementedException("Resolution must be minute.");
            _Mom_Based_Alpha_momentumPeriod = momentumPeriod;
            _Mom_Based_Alpha_momentumResolution = momentumResolution; // must be daily
            _Mom_Based_Alpha_resolution = resolution; //must be minute
            _Mom_Based_Alpha_rebalanceDate = rebalanceDate;
            _Mom_Based_Alpha_rebalanceTime = rebalanceTime;
            _Mom_Based_Alpha_SymbolDataBySymbol = new Dictionary<Symbol, SymbolData>();
            _Mom_Based_Alpha_initialized = true;
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

            foreach (SymbolData sd in _Mom_Based_Alpha_SymbolDataBySymbol.Values)
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

                        algorithm.Error(String.Format(
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

                        algorithm.Error(String.Format(
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
            }

            //all exchanges closed is non-existent condition
            if (stocksNotConsolidated.Count == _Mom_Based_Alpha_SymbolDataBySymbol.Count ||
                stocksWithoutData.Count == _Mom_Based_Alpha_SymbolDataBySymbol.Count ||
                stocksWithExchangeClosed.Count == _Mom_Based_Alpha_SymbolDataBySymbol.Count)
            {
                algorithm.Error(String.Format(
                    "Time: {0}. All stocks were not consolidated ({1}) or all without data ({2}) or all with exchange not open ({3}).",
                    algorithm.Time.ToString(),
                    stocksNotConsolidated.Count.ToString(),
                    stocksWithoutData.Count.ToString(),
                    stocksWithExchangeClosed.Count.ToString()
                    ));

                if (stocksWithoutDataBlacklist.Count == _Mom_Based_Alpha_SymbolDataBySymbol.Count ||
                    stocksWithExchangeClosedBlacklist.Count == _Mom_Based_Alpha_SymbolDataBySymbol.Count)
                {
                    // if all stocks had exchange closed or no data too many times, than return no insights
                    _Mom_Based_Alpha_previousInsights = new List<Insight>();
                    return _Mom_Based_Alpha_previousInsights;
                }

                // i f all stocks had exchange closed or no data but threshold was not met for all than return previous insights
                return _Mom_Based_Alpha_previousInsights;
            }

            // carry over insights that have exchange closed or missing data, but did not hit threshold in that
            List<Insight> insightsToBeCarriedOver =
                (from pi in _Mom_Based_Alpha_previousInsights
                 join closedExch in stocksWithExchangeClosed
                 on pi.Symbol.Value equals closedExch
                 join noData in stocksWithoutData
                 on pi.Symbol.Value equals noData
                 join noCons in stocksNotConsolidated
                 on pi.Symbol.Value equals noCons
                 join sd in _Mom_Based_Alpha_SymbolDataBySymbol.Values
                 on pi.Symbol.Value equals sd.Security.Symbol.Value
                 where sd.ConsecutiveExchangeClosed <= _exchangeClosedThreshold &&
                 sd.ConsecutiveMissingData <= _didNotGetDataTimesThreshold
                 select pi)
                 .ToList();

            List<Insight> insightsNew =
                (from sd in _Mom_Based_Alpha_SymbolDataBySymbol.Values
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
                     (double)(100 * (sd.MOM / _Mom_Based_Alpha_momentumPeriod)),
                     null,
                     "Mom_Based_Alpha"))
                .ToList();

            _Mom_Based_Alpha_previousInsights = new List<Insight>();
            _Mom_Based_Alpha_previousInsights.AddRange(insightsToBeCarriedOver);
            _Mom_Based_Alpha_previousInsights.AddRange(insightsNew);

            if (_Mom_Based_Alpha_previousInsights.Count() != _Mom_Based_Alpha_SymbolDataBySymbol.Count)
            {
                algorithm.Error(String.Format("Time: {0}. Returning {1} insights for {2} symbols.",
                    algorithm.Time.ToString(),
                    _previousInsights.Count.ToString(),
                    _Mom_Based_Alpha_SymbolDataBySymbol.Count.ToString()));
            }

            return _Mom_Based_Alpha_previousInsights;
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
            // update was called by scheduler at is rebalance time
            if (_Mom_Based_Alpha_rebalanceDate.GetDates(algorithm.Time.Date, algorithm.Time.Date).Count() == 1 &&
                    _Mom_Based_Alpha_rebalanceTime.CreateUtcEventTimes( new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(h => h.Hour).Contains(algorithm.Time.Hour) &&
                    _Mom_Based_Alpha_rebalanceTime.CreateUtcEventTimes( new List<DateTime>() { algorithm.Time.Date.ConvertToUtc(algorithm.TimeZone, false).Date }).Select(dtl => dtl.ConvertFromUtc(algorithm.TimeZone, false)).Select(m => m.Minute).Contains(algorithm.Time.Minute))
            {
                _previousInsights = UpdateMomBasedInsights(algorithm, data).ToList();

                return _previousInsights;
            }
            else return new List<Insight>();
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            IReadOnlyList<Security> removed =
                (from c in changes.RemovedSecurities
                 join us in algorithm.UniverseManager["_MOM_BASED_ROTATION_SM-MOM_BASED_ALPHA_UNIVERSE-EQUITY-USA 2T"].Members
                 on c.Symbol equals us.Key
                 select c)
                 .ToList<Security>();

            IReadOnlyList<Security> added =
                (from c in changes.AddedSecurities
                 join us in algorithm.UniverseManager["_MOM_BASED_ROTATION_SM-MOM_BASED_ALPHA_UNIVERSE-EQUITY-USA 2T"].Members
                 on c.Symbol equals us.Key
                 select c)
                 .ToList<Security>();

            // clean up data for removed securities
            foreach (var r in removed)
            {
                SymbolData data;
                if (_Mom_Based_Alpha_SymbolDataBySymbol.TryGetValue(r.Symbol, out data))
                {
                    _Mom_Based_Alpha_SymbolDataBySymbol.Remove(r.Symbol);
                    algorithm.SubscriptionManager.RemoveConsolidator(r.Symbol, data.TBConsolidator);
                }
            }

            // initialize data for added securities
            var addedSymbols = new List<Symbol>();
            foreach (var a in added)
            {
                if (!_Mom_Based_Alpha_SymbolDataBySymbol.ContainsKey(a.Symbol))
                {
                    var symbolData = new SymbolData(algorithm, a, _Mom_Based_Alpha_momentumPeriod, _Mom_Based_Alpha_momentumResolution);
                    _Mom_Based_Alpha_SymbolDataBySymbol[a.Symbol] = symbolData;
                    addedSymbols.Add(symbolData.Security.Symbol);
                }
            }

            if (addedSymbols.Count > 0)
            {
                //warmup our indicators by pushing history through the consolidators
                algorithm.History(addedSymbols, _Mom_Based_Alpha_momentumPeriod+1, _Mom_Based_Alpha_momentumResolution)
                .PushThrough(bar =>
                {
                    SymbolData symbolData;
                    if (_Mom_Based_Alpha_SymbolDataBySymbol.TryGetValue(bar.Symbol, out symbolData))
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