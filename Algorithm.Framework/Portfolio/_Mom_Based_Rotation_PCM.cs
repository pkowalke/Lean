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
        private readonly InsightCollection _insightCollection = new InsightCollection();

        private DateTime _rebalancingTime;
        private readonly TimeSpan _rebalancingPeriod;
        private List<Symbol> _removedSymbols;        
        private DateTime? _nextExpiryTime;
        private List<IPortfolioTarget> _currentTargets = new List<IPortfolioTarget>();

        /// <summary>
        /// Initialize a new instance of <see cref="EqualWeightingPortfolioConstructionModel"/>
        /// </summary>
        /// <param name="resolution">Rebalancing frequency</param>
        public _Mom_Based_Rotation_PCM(Resolution resolution = Resolution.Daily)
        {
            _rebalancingPeriod = resolution.ToTimeSpan();
        }

        /// <summary>
        /// Create portfolio targets from the specified insights
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="insights">The insights to create portoflio targets from</param>
        /// <returns>An enumerable of portoflio targets to be sent to the execution model</returns>
        public override IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithmFramework algorithm, Insight[] insights)
        {
            List<IPortfolioTarget> targets = new List<IPortfolioTarget>();

            if (((_Mom_Based_Rotation_QCFA)(algorithm)).IsRebalancingNow)
            {
                List<Insight> topInsights;

                bool QuantitiesOverZero = false;

                for (int j = 0; !QuantitiesOverZero; j++)
                {
                    topInsights =
                        (from i in insights
                         where i.Magnitude > 0 &&
                         i.Direction == InsightDirection.Up
                         orderby i.Magnitude descending
                         select i)
                         .Take<Insight>(((_Mom_Based_Rotation_QCFA)(algorithm)).NumberOfTopMomentumStocks - j)
                         .ToList<Insight>();

                    if ((topInsights.Count) != (((_Mom_Based_Rotation_QCFA)(algorithm)).NumberOfTopMomentumStocks-j))
                    {
                        algorithm.Error(String.Format("Time: {0}. There are not enough insights with positive direction to create requested number of targets. Liquidating!!!",
                            algorithm.Time.ToString()));

                        return GetZeroTargetsForAllInvestedHoldings(algorithm);
                    }

                    foreach (Insight i in topInsights)
                    {
                        decimal OrderQuantity = Math.Floor((0.95m*algorithm.Portfolio.TotalPortfolioValue/topInsights.Count)/algorithm.Securities[i.Symbol.Value].Close).SmartRounding();

                        if (OrderQuantity > 0)
                        {
                            targets.Add(new PortfolioTarget(i.Symbol, OrderQuantity));
                        }
                        else
                        {
                            algorithm.Error(String.Format("Time: {0}. The calculated qty for symbol: {1} would have been less than 1. I will try skipping lowest scoring symbol to free up buying power.",
                            algorithm.Time.ToString(),
                            i.Symbol.Value));
                            targets.Clear();
                            break;
                        }
                    }

                    if (targets.Count == (((_Mom_Based_Rotation_QCFA)(algorithm)).NumberOfTopMomentumStocks - j))
                    {
                        if (targets.Count == 0)
                        {
                            algorithm.Error(String.Format("Time: {0}. Can't add even single share of top pick in the list.",
                            algorithm.Time.ToString()));
                        }

                        algorithm.Debug(String.Format("Time: {0}. Adding {1} symbols to targets.",
                            algorithm.Time.ToString(),
                            targets.Count.ToString()));

                        QuantitiesOverZero = true;
                    }
                }

                //adjust targets which already exist
                targets = AdjustExistingTargets(algorithm, targets.ToArray()).ToList();
                targets = IncludeAlsoZeroTargetsForExcluded(algorithm, targets.ToArray()).ToList();
                _currentTargets = targets;
            }
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
            return _currentTargets;
        }

        public IEnumerable<IPortfolioTarget> GetZeroTargetsForAllInvestedHoldings(QCAlgorithmFramework algorithm)
        {
            List<IPortfolioTarget> targets = new List<IPortfolioTarget>();

            foreach (Securities.Security s in algorithm.Portfolio.Securities.Values)
            {
                if (s.Invested)
                {
                    targets.Add(new PortfolioTarget(s.Symbol, 0));
                }
            }

            return targets;
        }

        public IEnumerable<IPortfolioTarget> IncludeAlsoZeroTargetsForExcluded(QCAlgorithmFramework algorithm, IPortfolioTarget[] targets)
        {
            return
                (from h in GetZeroTargetsForAllInvestedHoldings(algorithm)
                 join t in targets
                 on 1 equals 1
                 where t.Symbol.Value != h.Symbol.Value
                 select h)
                 .Union(targets)
                 .OrderBy(x => x.Quantity);
        }

        public IEnumerable<IPortfolioTarget> AdjustExistingTargets(QCAlgorithmFramework algorithm, IPortfolioTarget [] targets)
        {
            List<PortfolioTarget> newTargets = new List<PortfolioTarget>();

            foreach (PortfolioTarget pt in targets)
            {
                if (algorithm.Securities[pt.Symbol.Value].Invested)
                {
                    newTargets.Add(new PortfolioTarget(pt.Symbol, algorithm.Securities[pt.Symbol.Value].Holdings.Quantity));
                }
                else
                {
                    newTargets.Add(new PortfolioTarget(pt.Symbol, pt.Quantity));
                }
            }

            return newTargets;
        }


        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            // Get removed symbol and invalidate them in the insight collection
            _removedSymbols = changes.RemovedSecurities.Select(x => x.Symbol).ToList();
            _insightCollection.Clear(_removedSymbols.ToArray());
        }
    }
}