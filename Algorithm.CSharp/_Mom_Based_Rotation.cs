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
using QuantConnect.Indicators;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{

    /// <meta name="tag" content="MOM" />
    /// <meta name="tag" content="Momentum" />
    public class _Mom_Based_Rotation : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private readonly DateTime _startDate = new DateTime(2007, 12, 01);
        private readonly DateTime _endDate = new DateTime(2011, 12, 31);
        private readonly Resolution _resolution = Resolution.Daily;

        private readonly Decimal _backtestCash = 10000m;

        private readonly String[] _symbolStrs =
        {
            "AMZN",  //"Amazon.com"
			"NFLX",  //"Netflix"
            "NVDA",  //"NVIDIA"
			"MSFT",  //"Microsoft"
			"BA",  //"Boeing"
			"CSCO",  //"Cisco"
			"AAPL",  //"Apple"
			"V",  //"Visa"
			"HD",  //"Home Depot"
			"UNH",  //"UnitedHealth"
			"BAC",  //"Bank of America"
			"JPM",  //"JPMorgan"
			"GOOGL",  //"Alphabet A"
			"INTC",  //"Intel"
			"PFE",  //"Pfizer"
			"WMT",  //"Walmart"
			//"BRKb",  //"Berkshire Hathaway B"
			"VZ",  //"Verizon"
			"DIS",  //"Walt Disney"
			"WFC",  //"Wells Fargo&Co"
			"JNJ",  //"J&J"
			"XOM",  //"Exxon Mobil"
			"CVX",  //"Chevron"
			"C",  //"Citigroup"
			"CMCSA",  //"Comcast"
			"FB",  //"Facebook"
			"ORCL",  //"Oracle"
			"T",  //"AT&T"
			"BABA",  //"Alibaba"
			"TSLA",  //"Tesla"
			"IVV" //Shares of SP500
        };
        private readonly int _numberOfTopStocks = 4;

        private Dictionary<String, Momentum> _momentum = new Dictionary<String, Momentum>();
        private readonly int _momentumPeriod = 6 * 21;

        public override void Initialize()
        {
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_backtestCash);
            SetWarmup(_momentumPeriod);

            foreach (String s in _symbolStrs)
            {
                AddEquity(s, _resolution, Market.USA, fillDataForward: true, leverage: 0, extendedMarketHours: false);
                _momentum.Add(s, MOM(Symbol(s), _momentumPeriod, _resolution));
            }

            Schedule.On(DateRules.EveryDay("BA"), TimeRules.BeforeMarketClose("BA", 1), Rebalance);
        }

        public void Rebalance()
        {
            if (IsWarmingUp) return;

            List<String> investedHoldings =
                (from ih in Portfolio
                 where ih.Value.Invested == true
                 select ih.Value.Symbol.Value.ToString())
                .ToList<String>();

            List<String> topPicks =
                (from top_pair in _momentum
                 orderby top_pair.Value descending
                 select top_pair.Key.ToString())
                .Take(_numberOfTopStocks)
                .ToList<String>();

            List<String> liquidateHoldings =
                (from ih in investedHoldings
                 where !topPicks.Contains(ih)
                 select ih.ToString())
                .ToList<String>();

            List<String> acquireHoldings =
                (from tp in topPicks
                 where !investedHoldings.Contains(tp)
                 select tp.ToString())
                .ToList<String>();

            foreach (var s in liquidateHoldings) Liquidate(s.ToString());

            Double remainingCash = Convert.ToDouble(Portfolio.Cash);
            Double totalPortfolioValue = Convert.ToDouble(Portfolio.TotalPortfolioValue);

            foreach (var s in acquireHoldings) SetHoldings(s.ToString(), (1.0 * remainingCash / totalPortfolioValue) / Convert.ToDouble(acquireHoldings.Count()));
        }

        public override void OnData(Slice data)
        {
            ;
        }

        public void OnData(Dividends slice)
        {
            ;
        }

        public void OnData(Splits slice)
        {
            ;
        }

        public void OnData(Delistings slice)
        {
            ;
        }

        public void OnData(SymbolChangedEvents slice)
        {
            ;
        }

        public bool CanRunLocally { get; } = true;

        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "263.153%"},
            {"Drawdown", "2.200%"},
            {"Expectancy", "0"},
            {"Net Profit", "1.663%"},
            {"Sharpe Ratio", "4.41"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0.007"},
            {"Beta", "76.118"},
            {"Annual Standard Deviation", "0.192"},
            {"Annual Variance", "0.037"},
            {"Information Ratio", "4.354"},
            {"Tracking Error", "0.192"},
            {"Treynor Ratio", "0.011"},
            {"Total Fees", "$3.26"}
        };
    }
}
