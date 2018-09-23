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
        private readonly int _momentumPeriod;

        private readonly Resolution _momentumResolution;
        private readonly Resolution _resolution;

        private readonly Dictionary<Symbol, SymbolData> _symbolDataBySymbol;

        private List<Insight> _previousInsights = new List<Insight>();

        private readonly int _didNotGetDataTimesThreshold = 6;
        private readonly int _exchangeClosedThreshold = 6;

        public _Mom_Based_Rotation_AM(int momentumPeriod, Resolution momentumResolution, Resolution resolution)
        {
            if (momentumResolution != Resolution.Daily) throw new NotImplementedException("Resolution of momentum must be Daily.");
            if (resolution != Resolution.Minute) throw new NotImplementedException("Resolution must be minute.");

            _momentumPeriod = momentumPeriod;
            _momentumResolution = momentumResolution; // must be daily
            _resolution = resolution; //must be minute
            _symbolDataBySymbol = new Dictionary<Symbol, SymbolData>();
            Name = $"{nameof(_Mom_Based_Rotation_AM)}({momentumPeriod},{resolution})";
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
            // if data is null than update was called by scheduler this it is emit time
            if (data == null)
            {
                
                List<String> stocksWithData = new List<string>();
                List<String> stocksWithoutData = new List<string>();
                List<String> stocksWithExchangeOpen = new List<string>();
                List<String> stocksWithExchangeClosed = new List<string>();
                List<String> stocksNotConsolidated = new List<string>();

                List<String> stocksWithoutDataBlacklist = new List<string>();
                List<String> stocksWithExchangeClosedBlacklist = new List<string>();

                foreach (SymbolData sd in _symbolDataBySymbol.Values)
                {
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

                    if (!sd.IsReady())
                    {
                        stocksNotConsolidated.Add(sd.Security.Symbol.Value);
                    }
                }

                //all exchanges closed is non-existent condition
                if (stocksNotConsolidated.Count == _symbolDataBySymbol.Count ||
                    stocksWithoutData.Count == _symbolDataBySymbol.Count ||
                    stocksWithExchangeClosed.Count == _symbolDataBySymbol.Count)
                {
                    algorithm.Error(String.Format(
                        "Time: {0}. All stocks were not consolidated ({1}) or all without data ({2}) or all with exchange not open ({3}).",
                        algorithm.Time.ToString(),
                        stocksNotConsolidated.Count.ToString(),
                        stocksWithoutData.Count.ToString(),
                        stocksWithExchangeClosed.Count.ToString()
                        ));

                    if (stocksWithoutDataBlacklist.Count == _symbolDataBySymbol.Count ||
                        stocksWithExchangeClosedBlacklist.Count == _symbolDataBySymbol.Count)
                    {
                        // if all stocks had exchange closed or no data too many times, than return no insights
                        _previousInsights = new List<Insight>();
                        return _previousInsights; 
                    }

                    // i f all stocks had exchange closed or no data but threshold was not met for all than return previous insights
                    return _previousInsights;
                }

                // carry over insights that have exchange closed or missing data, but did not hit threshold in that
                List<Insight> insightsToBeCarriedOver =
                    (from pi in _previousInsights
                     join closedExch in stocksWithExchangeClosed
                     on pi.Symbol.Value equals closedExch
                     join noData in stocksWithoutData
                     on pi.Symbol.Value equals noData
                     join noCons in stocksNotConsolidated
                     on pi.Symbol.Value equals noCons
                     join sd in _symbolDataBySymbol.Values
                     on pi.Symbol.Value equals sd.Security.Symbol.Value
                     where sd.ConsecutiveExchangeClosed <= _exchangeClosedThreshold &&
                     sd.ConsecutiveMissingData <= _didNotGetDataTimesThreshold
                     select pi)
                     .ToList();

                List<Insight> insightsNew =
                    (from sd in _symbolDataBySymbol.Values
                     join inData in stocksWithData
                     on sd.Security.Symbol.Value equals inData
                     join openExch in stocksWithExchangeOpen
                     on sd.Security.Symbol.Value equals openExch
                     where 
                     !stocksNotConsolidated.Contains(sd.Security.Symbol.Value) &&
                     sd.IsReady() &&
                     sd.TBConsolidator.Consolidated.Price > 0
                     orderby sd.MOM descending
                     select Insight.Price(sd.Security.Symbol, sd.Security.Exchange.Hours.GetNextMarketOpen(sd.Security.Exchange.Hours.GetNextMarketClose(algorithm.Time, false), false).Subtract(algorithm.Time), (sd.MOM>0)?InsightDirection.Up: (sd.MOM < 0)?InsightDirection.Down: InsightDirection.Flat, (double)(100*(sd.MOM/_momentumPeriod)), null))
                    .ToList();

                _previousInsights = new List<Insight>();
                _previousInsights.AddRange(insightsToBeCarriedOver);
                _previousInsights.AddRange(insightsNew);

                if (_previousInsights.Count() != _symbolDataBySymbol.Count) algorithm.Error(String.Format("Time: {0}. Returning {1} insights for {2} symbols.",
                        algorithm.Time.ToString(),
                        _previousInsights.Count.ToString(),
                        _symbolDataBySymbol.Count.ToString()));

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
            // clean up data for removed securities
            foreach (var removed in changes.RemovedSecurities)
            {
                SymbolData data;
                if (_symbolDataBySymbol.TryGetValue(removed.Symbol, out data))
                {
                    _symbolDataBySymbol.Remove(removed.Symbol);
                    algorithm.SubscriptionManager.RemoveConsolidator(removed.Symbol, data.TBConsolidator);
                }
            }

            // initialize data for added securities
            var addedSymbols = new List<Symbol>();
            foreach (var added in changes.AddedSecurities)
            {
                if (!_symbolDataBySymbol.ContainsKey(added.Symbol))
                {
                    var symbolData = new SymbolData(algorithm, added, _momentumPeriod, _momentumResolution);
                    _symbolDataBySymbol[added.Symbol] = symbolData;
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
                    if (_symbolDataBySymbol.TryGetValue(bar.Symbol, out symbolData))
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
                algorithm.Schedule.On(algorithm.DateRules.EveryDay(), algorithm.TimeRules.AfterMarketOpen(Security.Symbol, -480, false), ScanUpdateConsolidator);

                ConsecutiveMissingData = 0;
                ConsecutiveExchangeClosed = 0;
            }

            public bool IsReady()
            {
                return (RollingWindow.IsReady &&
                    MOM != -9999999999m) ? true : false;
            }

            private void ScanUpdateConsolidator()
            {
                TBConsolidator.Scan(Algorithm.Time);
            }
        }
    }
}