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
using System.Linq;
using System.Collections.Generic;

using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    public class _QCWhatMomentumAlphaModel : AlphaModel
    {
        private readonly Symbol[] _universeSymbols = null;
        private HashSet<Security> _securities;
        private IDictionary<Symbol, SymbolData> _symbolDataBySymbol;
        private IDictionary<Symbol, DateTime> _insightsTimeBySymbol;
        private IDictionary<Symbol, TimeSpan> _insightsLastPeriodBySymbol;

        private readonly Resolution _universeResolution;
        private readonly Resolution _momentumResolution;
        private readonly Int32 _momentumPeriod;

        public _QCWhatMomentumAlphaModel(Int32 momentumPeriod = 265, Resolution universeResolution = Resolution.Minute, Resolution momentumResolution = Resolution.Daily, String alphaModelName = "MomentumBasedAlpha", Symbol[] universeSymbols = null)
        {
            if (momentumResolution.ToTimeSpan() < universeResolution.ToTimeSpan())
                throw new System.NotSupportedException("Momentum resolution must be larger than universe resolution.");

            if (momentumResolution != Resolution.Daily)
                throw new System.NotSupportedException("Momentum resolution must be Daily.");

            if (universeSymbols != null) _universeSymbols = universeSymbols;

            _momentumResolution = momentumResolution;
            _universeResolution = universeResolution;
            _momentumPeriod = momentumPeriod;

            Name = alphaModelName;

            _securities = new HashSet<Security>();

            _symbolDataBySymbol = new Dictionary<Symbol, SymbolData>();

            _insightsTimeBySymbol = new Dictionary<Symbol, DateTime>();
        }

        public override IEnumerable<Insight> Update(QCAlgorithmFramework algorithm, Slice data)
        {
            foreach (SymbolData sd in _symbolDataBySymbol.Values)
            {
                if (ShouldEmitInsight(algorithm.UtcTime, sd.Security.Symbol))
                {
                    sd.ScanUpdateConsolidator();

                    if (sd.IsReady())
                    {
                        //checks that the data is fresh as in it was consolidated
                        //the day following the day it was generated
                        if (sd.TBConsolidator.Consolidated.EndTime.Date == sd.Security.Exchange.Hours.GetPreviousTradingDay(algorithm.Time.Date).AddDays(1).Date)
                        {
                            yield return Insight.Price(
                            SetInsightTimeAndPeriod(sd.Security.Symbol, sd.TBConsolidator.Consolidated.EndTime.ToUniversalTime(), _momentumResolution.ToTimeSpan()),
                            Insight.ComputeCloseTime(sd.Security.Exchange.Hours, sd.TBConsolidator.Consolidated.EndTime.ToUniversalTime(), _momentumResolution.ToTimeSpan()),
                            (sd.MOM > 0) ? InsightDirection.Up : (sd.MOM < 0) ? InsightDirection.Down : InsightDirection.Flat,
                            Math.Abs((double)(100 * (sd.MOM / _momentumPeriod))),
                            null,
                            Name
                            );
                        }
                    }
                }
            }
        }

        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            // first if array of universe symbols was supplied than build relevant changes
            // based on those universe symbols
            SecurityChanges relevantChanges;

            if (_universeSymbols != null)
            {
                IList<Security> added = new List<Security>();
                IList<Security> removed = new List<Security>();

                foreach (Symbol u in _universeSymbols)
                {
                    added = added.Union(
                        (from c in changes.AddedSecurities
                         join us in algorithm.UniverseManager[u].Members
                         on c.Symbol equals us.Key
                         select c)
                        ).ToList();

                    removed = removed.Union(
                        (from c in changes.RemovedSecurities
                         join us in algorithm.UniverseManager[u].Members
                         on c.Symbol equals us.Key
                         select c)
                        ).ToList();
                }

                relevantChanges = new SecurityChanges(added, removed);
            }
            else
            {
                relevantChanges = changes;
            }

            NotifiedSecurityChanges.UpdateCollection(_securities, relevantChanges);

            foreach (var added in relevantChanges.AddedSecurities)
            {
                _symbolDataBySymbol.Add(added.Symbol, new SymbolData(algorithm, added, _momentumPeriod, _momentumResolution));
            }

            if (relevantChanges.AddedSecurities.Count > 0)
            {
                //warmup our indicators by pushing history through the consolidators
                algorithm.History(relevantChanges.AddedSecurities.Select(security => security.Symbol), _momentumPeriod + 1, _momentumResolution)
                .PushThrough(bar =>
                {
                    SymbolData symbolData;
                    if (_symbolDataBySymbol.TryGetValue(bar.Symbol, out symbolData))
                    {
                        symbolData.TBConsolidator.Update(bar);
                    }
                });
            }

            // this will allow the insight to be re-sent when the security re-joins the universe
            foreach (var removed in relevantChanges.RemovedSecurities)
            {
                _symbolDataBySymbol.Remove(removed.Symbol);
                _insightsTimeBySymbol.Remove(removed.Symbol);
                _insightsLastPeriodBySymbol.Remove(removed.Symbol);
            }
        }

        protected virtual bool ShouldEmitInsight(DateTime utcTime, Symbol symbol)
        {
            DateTime generatedTimeUtc;
            if (_insightsTimeBySymbol.TryGetValue(symbol, out generatedTimeUtc))
            {
                // we previously emitted a insight for this symbol, check it's period to see
                // if we should emit another insight
                if (utcTime - generatedTimeUtc < _insightsLastPeriodBySymbol[symbol])
                {
                    return false;
                }
            }


            // we either haven't emitted a insight for this symbol or the previous
            // insight's period has expired, so emit a new insight now for this symbol
            return true;
        }

        protected Symbol SetInsightTimeAndPeriod(Symbol symbol, DateTime utcTime, TimeSpan period)
        {
            try
            {
                _insightsLastPeriodBySymbol[symbol] = period;
                _insightsTimeBySymbol[symbol] = utcTime;

                return symbol;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private class SymbolData
        {
            QCAlgorithmFramework Algorithm;
            public Security Security;
            public RollingWindow<TradeBar> RollingWindow;
            public TradeBarConsolidator TBConsolidator;
            public Decimal MOM;

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

                //setup consolidator to manually check for readiness (new data) every morning
                algorithm.Schedule.On(algorithm.DateRules.EveryDay(), algorithm.TimeRules.At(00, 00, 01, TimeZones.Utc), ScanUpdateConsolidator);
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
