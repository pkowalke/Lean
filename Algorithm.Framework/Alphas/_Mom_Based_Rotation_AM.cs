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
        private readonly int _numberOfTopStocks;
        private readonly Resolution _momentumResolution;
        private readonly Resolution _resolution;
        private readonly TimeSpan _predictionInterval;
        private readonly Dictionary<Symbol, SymbolData> _symbolDataBySymbol;

        public _Mom_Based_Rotation_AM(int numberOfTopStocks = 1, int momentumPeriod = 90, Resolution momentumResolution = Resolution.Daily, Resolution resolution = Resolution.Daily)
        {
            _numberOfTopStocks = numberOfTopStocks;
            _momentumPeriod = momentumPeriod; 
            _momentumResolution = momentumResolution; // must be daily
            _resolution = resolution; //must be minute
            _predictionInterval = TimeSpan.FromDays(1); //obsolete
            _symbolDataBySymbol = new Dictionary<Symbol, SymbolData>();
            Name = $"{nameof(_Mom_Based_Rotation_AM)}({momentumPeriod},{resolution})";
        }

        /// <summary>
        /// Updates this alpha model with the latest data from the algorithm.
        /// 
        /// Returns insights with positive direction (yes, even if momentum is negative)
        /// for top number of stocks sorted by momentum (_numberOfTopStocks variable)
        /// at 9:31AM ET
        /// security's exchange must be open at the time
        /// security must have CanEmit set to true (new MOM sample)
        /// 
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="data">The new data available</param>
        /// <returns>The new insights generated</returns>
        public override IEnumerable<Insight> Update(QCAlgorithmFramework algorithm, Slice data)
        {
                return
                (from sd in _symbolDataBySymbol.Values
                 where sd.MOM > 0 &&
                 sd.CanEmit() == true &&
                 sd.Security.Exchange.DateTimeIsOpen(algorithm.Time) == true
                 orderby sd.MOM descending
                 select Insight.Price(sd.Security.Symbol, sd.Security.Exchange.Hours.RegularMarketDuration, InsightDirection.Up, null, null))
                 .Take(_numberOfTopStocks)
                 .ToList();
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
                // warmup our indicators by pushing history through the consolidators
                algorithm.History(addedSymbols, _momentumPeriod, _momentumResolution)
                .PushThrough(bar =>
                {
                    SymbolData symbolData;
                    if (_symbolDataBySymbol.TryGetValue(bar.Symbol, out symbolData))
                    {
                        symbolData.TBConsolidator.Update(bar);
                    }
                });

               // algorithm.History(addedSymbols, _momentumPeriod, _momentumResolution)
               //.PushThrough(bar =>
               //{
               //    SymbolData symbolData;
               //    if (_symbolDataBySymbol.TryGetValue(bar.Symbol, out symbolData))
               //    {
               //        symbolData.MOM.Update(bar.EndTime, bar.Value);
               //    }
               //});
            }
        }

        /// <summary>
        /// Contains data specific to a symbol required by this model
        /// </summary>
        private class SymbolData
        {
            public Security Security;
            public TradeBarConsolidator TBConsolidator;
            //public IDataConsolidator Consolidator;
            public Momentum MOM;
            public long previous = 0;

            public SymbolData(QCAlgorithmFramework algorithm, Security security, int momentumPeriod, Resolution momentumResolution)
            {
                Security = security;

                TBConsolidator = new TradeBarConsolidator(TimeSpan.FromDays(1));
                algorithm.SubscriptionManager.AddConsolidator(security.Symbol, TBConsolidator);

                MOM = algorithm.MOM(security.Symbol, momentumPeriod, momentumResolution);
                algorithm.RegisterIndicator(security.Symbol, MOM, TBConsolidator);

                if (security.Symbol.Value == "AAPL" && previous == 0)
                {
                    MOM.Updated += (sender, updated) =>
                    {
                        if (TBConsolidator.Consolidated != null)
                        {
                            algorithm.Debug(String.Format("MOM Cons {0} Upd. Time: {1}, Price: {2}, MOM: {3}.",
                                TBConsolidator.Consolidated.Symbol.ToString(),
                                TBConsolidator.Consolidated.Time.ToString(),
                                TBConsolidator.Consolidated.Price.SmartRounding().ToString(),
                                MOM.Current.Value.SmartRounding().ToString()
                                ));
                        }
                    };

                    TBConsolidator.DataConsolidated += (sender, updated) =>
                    {
                        if (TBConsolidator.Consolidated != null)
                        {
                            algorithm.Log(String.Format("TB Cons {0} Upd. Time: {1}, Price: {2}",
                                TBConsolidator.Consolidated.Symbol.ToString(),
                                TBConsolidator.Consolidated.Time.ToString(),
                                TBConsolidator.Consolidated.Price.SmartRounding().ToString()
                                ));
                        }
                    };
                }

                //Consolidator = algorithm.ResolveConsolidator(security.Symbol, momentumResolution);
                //algorithm.SubscriptionManager.AddConsolidator(security.Symbol, Consolidator);
                //MOM = algorithm.MOM(security.Symbol, momentumPeriod, momentumResolution);
                //algorithm.RegisterIndicator(security.Symbol, MOM, Consolidator);
            }

            public bool CanEmit()
            {
                if (previous == MOM.Samples) return false;
                previous = MOM.Samples;
                return MOM.IsReady;
            }
        }
    }
}