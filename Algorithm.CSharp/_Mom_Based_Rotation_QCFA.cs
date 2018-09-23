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
using System.Threading;

using QuantConnect.Algorithm.Framework;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Risk;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Scheduling;


namespace QuantConnect.Algorithm.Framework
{

    /// <meta name="tag" content="MOM" />
    /// <meta name="tag" content="Momentum" />
    public class _Mom_Based_Rotation_QCFA : QCAlgorithmFramework
    {
        private readonly DateTime _startDate = new DateTime(2018, 01, 01);
        private readonly DateTime _endDate = new DateTime(2018, 09, 18);
        private readonly Resolution _resolution = Resolution.Minute;

        private readonly Decimal _backtestCash = 10000m;

        private readonly IReadOnlyList<String> _symbolStrs = new List<String>
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

        private readonly int _momentumPeriod = 6 * 21;
        private readonly Resolution _momentumResolution = Resolution.Daily;
        private readonly int _numberOfTopStocks = 6;

        private readonly int _emitHour = 8;
        private readonly int _emitMinute = 30;
        private readonly DayOfWeek [] _emitDaysOfWeek = new DayOfWeek [] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        private Scheduling.ITimeRule _emitTime;
        private Scheduling.IDateRule _emitDate;
        private readonly DateTime _rebalanceTime = new DateTime(001, 01, 01, 09, 32, 00, 000); //set hour and min

        public override void Initialize()
        {
            SetBrokerageModel(Brokerages.BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);

            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_backtestCash);
            SetTimeZone(TimeZones.NewYork);

            _emitTime = TimeRules.At(_emitHour, _emitMinute);
            _emitDate = DateRules.Every(_emitDaysOfWeek);

            UniverseSettings.Resolution = _resolution;
            UniverseSettings.ExtendedMarketHours = false;
            UniverseSettings.FillForward = true;

            SetUniverseSelection(new _Mom_Based_Rotation_SM(_symbolStrs.Select(s => QuantConnect.Symbol.Create(s, SecurityType.Equity, Market.USA)), this.UniverseSettings, this.SecurityInitializer));
            SetAlpha(new _Mom_Based_Rotation_AM(_momentumPeriod, _momentumResolution, _resolution));
            SetRiskManagement(new _Mom_Based_Rotation_RM(0.1m));
            SetPortfolioConstruction(new _Mom_Based_Rotation_PCM());
            SetExecution(new _Mom_Based_Rotation_EM());

            Schedule.On(_emitDate, _emitTime, EmitInsights);            
        }

        public void EmitInsights()
        {
            //if market is open today for at least single security send insights
            if ((from s in Portfolio.Securities.Values
                 where s.Exchange.DateIsOpen(Time.Date)
                 select s)
                 .Count() >= 1)
            {
                Execution.Execute(this,
                RiskManagement.ManageRisk(this,
                    PortfolioConstruction.CreateTargets(this,
                    Alpha.Update(this, null).ToArray<Insight>()).ToArray<IPortfolioTarget>()).ToArray<IPortfolioTarget>());
            }
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
