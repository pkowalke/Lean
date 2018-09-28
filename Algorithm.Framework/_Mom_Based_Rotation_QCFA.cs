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
using QuantConnect.Orders;
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
    public partial class _Mom_Based_Rotation_QCFA : QCAlgorithmFramework
    {
        private readonly DateTime _startDate = new DateTime(2018, 01, 01);
        private readonly DateTime _endDate = new DateTime(2018, 09, 19);
        private readonly Resolution _resolution = Resolution.Minute;

        private readonly Decimal _backtestCash = 50000m;

        /*private readonly IReadOnlyList<String> _symbolStrs = new List<String>
        {
            "AMZN",  //"Amazon.com"
            "AAPL",  //"Apple"
            "BA",  //"Boeing"
            "BABA",  //"Alibaba"
            "BAC",  //"Bank of America"
            //"BRKb",  //"Berkshire Hathaway B"
            "C",  //"Citigroup"
            "CMCSA",  //"Comcast"
            "CSCO",  //"Cisco"
            "CVX",  //"Chevron"
            "DIS",  //"Walt Disney"
            "FB",  //"Facebook"
            "GOOGL",  //"Alphabet A"
            "HD",  //"Home Depot"
            "INTC",  //"Intel"
            "IVV", //Shares of SP500
            "JNJ",  //"J&J"
            "JPM",  //"JPMorgan"
            "MSFT",  //"Microsoft"
            "NFLX",  //"Netflix"
            "NVDA",  //"NVIDIA"
            "ORCL",  //"Oracle"
            "PFE",  //"Pfizer"
            "T",  //"AT&T"
            "TSLA",  //"Tesla"
            "UNH",  //"UnitedHealth"
            "UNP", //Union Pacific Corp
            "V",  //"Visa"
            "VZ",  //"Verizon"
            "WFC",  //"Wells Fargo&Co"
            "WMT",  //"Walmart"
            "XOM"  //"Exxon Mobil"
        };*/

        private readonly IDictionary<String, List<Symbol>> _symbols = new Dictionary<string, List<Symbol>>()
        {
            {"Mom_Based_Alpha_Universe",
                new List<Symbol>
                {
                QuantConnect.Symbol.Create("AMZN", SecurityType.Equity, Market.USA),  //"Amazon.com" 
                QuantConnect.Symbol.Create("AAPL", SecurityType.Equity, Market.USA),  //"Apple"
                QuantConnect.Symbol.Create("BA", SecurityType.Equity, Market.USA),  //"Boeing"
                QuantConnect.Symbol.Create("BABA", SecurityType.Equity, Market.USA),  //"Alibaba"
                QuantConnect.Symbol.Create("BAC", SecurityType.Equity, Market.USA),  //"Bank of America"
                //QuantConnect.Symbol.Create("BRKb", SecurityType.Equity, Market.USA),  //"Berkshire Hathaway B"
                QuantConnect.Symbol.Create("C", SecurityType.Equity, Market.USA),  //"Citigroup"
                QuantConnect.Symbol.Create("CMCSA", SecurityType.Equity, Market.USA),  //"Comcast"
                QuantConnect.Symbol.Create("CSCO", SecurityType.Equity, Market.USA),  //"Cisco"
                QuantConnect.Symbol.Create("CVX", SecurityType.Equity, Market.USA),  //"Chevron"
                QuantConnect.Symbol.Create("DIS", SecurityType.Equity, Market.USA),  //"Walt Disney"
                QuantConnect.Symbol.Create("FB", SecurityType.Equity, Market.USA),  //"Facebook"
                QuantConnect.Symbol.Create("GOOGL", SecurityType.Equity, Market.USA),  //"Alphabet A"
                QuantConnect.Symbol.Create("HD", SecurityType.Equity, Market.USA),  //"Home Depot"
                QuantConnect.Symbol.Create("INTC", SecurityType.Equity, Market.USA),  //"Intel"
                QuantConnect.Symbol.Create("IVV", SecurityType.Equity, Market.USA), //Shares of SP500
                QuantConnect.Symbol.Create("JNJ", SecurityType.Equity, Market.USA),  //"J&J"
                QuantConnect.Symbol.Create("JPM", SecurityType.Equity, Market.USA),  //"JPMorgan"
                QuantConnect.Symbol.Create("MSFT", SecurityType.Equity, Market.USA),  //"Microsoft"
                QuantConnect.Symbol.Create("NFLX", SecurityType.Equity, Market.USA),  //"Netflix"
                QuantConnect.Symbol.Create("NVDA", SecurityType.Equity, Market.USA),  //"NVIDIA"
                QuantConnect.Symbol.Create("ORCL", SecurityType.Equity, Market.USA),  //"Oracle"
                QuantConnect.Symbol.Create("PFE", SecurityType.Equity, Market.USA),  //"Pfizer"
                QuantConnect.Symbol.Create("T", SecurityType.Equity, Market.USA),  //"AT&T"
                QuantConnect.Symbol.Create("TSLA", SecurityType.Equity, Market.USA),  //"Tesla"
                QuantConnect.Symbol.Create("UNH", SecurityType.Equity, Market.USA),  //"UnitedHealth"
                QuantConnect.Symbol.Create("UNP", SecurityType.Equity, Market.USA), //Union Pacific Corp
                QuantConnect.Symbol.Create("V", SecurityType.Equity, Market.USA),  //"Visa"
                QuantConnect.Symbol.Create("VZ", SecurityType.Equity, Market.USA),  //"Verizon"
                QuantConnect.Symbol.Create("WFC", SecurityType.Equity, Market.USA),  //"Wells Fargo&Co"
                QuantConnect.Symbol.Create("WMT", SecurityType.Equity, Market.USA),  //"Walmart"
                QuantConnect.Symbol.Create("XOM", SecurityType.Equity, Market.USA)  //"Exxon Mobil"
                }
            },

            {"Safe_Heaven_Universe",
                new List<Symbol>
                {
                QuantConnect.Symbol.Create("TIP", SecurityType.Equity, Market.USA),  //"iShares TIPS Bond" 
                }
            }
        };

        private readonly IDictionary<String, String> _alphaUniverse = new Dictionary<string, string>()
        {
            { "Mom_Based_Alpha", "_MOM_BASED_ROTATION_SM-MOM_BASED_ALPHA_UNIVERSE-EQUITY-USA 2T" }
        };

        private readonly int _momentumPeriod = 60;
        private readonly Resolution _momentumResolution = Resolution.Daily;
        private readonly int _numberOfTopStocks = 3;

        //actual rebalance of the portfolio or placement of orders based portfolio targets should happen at these times
        private readonly int _rebalanceHour = 8;
        private readonly int _rebalanceMinute = 30;
        private readonly DayOfWeek[] _rebalanceDaysOfWeek = new DayOfWeek[] { DayOfWeek.Wednesday };
        private Scheduling.ITimeRule _rebalanceTime;
        private Scheduling.IDateRule _rebalanceDate;

        private bool _isRebalancingNow = false;

        private Slice _lastNonEmptySlice;

        public IUniverseSelectionModel UniverseSelection;
        public _Mom_Based_Rotation_AM AlphaModel;
        public _Mom_Based_Rotation_PCM PortfolioConstruction;

        public override void Initialize()
        {
            SetBrokerageModel(Brokerages.BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);

            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_backtestCash);
            SetTimeZone(TimeZones.NewYork);

            _lastNonEmptySlice = new Slice(Time, new List<BaseData>());

            UniverseSettings.Resolution = _resolution;
            UniverseSettings.ExtendedMarketHours = false;
            UniverseSettings.FillForward = true;

            _rebalanceTime = TimeRules.At(_rebalanceHour, _rebalanceMinute, TimeZone);
            _rebalanceDate = DateRules.Every(_rebalanceDaysOfWeek);

            UniverseSelection = new _Mom_Based_Rotation_SM(_symbols, this.UniverseSettings, this.SecurityInitializer);
            AlphaModel = new _Mom_Based_Rotation_AM(_alphaUniverse);
            AlphaModel.InitMomBasedInsights(_momentumPeriod, _momentumResolution, _resolution, _rebalanceDate, _rebalanceTime);
            PortfolioConstruction =
                new _Mom_Based_Rotation_PCM(this,
                new _Mom_Based_Rotation_PCM.PortfolioConstructionAlphaModelData(
                    "Mom_Based_Alpha",
                    10000,
                    _numberOfTopStocks,
                    _rebalanceDate,
                    _rebalanceTime)
                    );

            SetUniverseSelection(UniverseSelection);
            SetAlpha(AlphaModel);
            //SetRiskManagement(new _Mom_Based_Rotation_RM(0.5m));
            SetPortfolioConstruction(PortfolioConstruction);
            SetExecution(new _Mom_Based_Rotation_EM());

            Schedule.On(_rebalanceDate, _rebalanceTime, CreateTargets);
        }

        public void CreateTargets()
        {
            //if market is open today for at least single security send insights
            if ((from s in Portfolio.Securities.Values
                 where s.Exchange.DateIsOpen(Time.Date)
                 select s)
                 .Count() >= 1)
            {
                Semaphore s = new Semaphore(1, 1);
                s.WaitOne();
                Thread.Sleep(1000); //sleep 1 secs in case to let processes clear out of the model
                _isRebalancingNow = true;

                OnFrameworkData(_lastNonEmptySlice);
                
                _isRebalancingNow = false;
                s.Release();
                s.Dispose();
            }
        }

        public override void OnData(Slice data)
        {
            if (data.HasData) _lastNonEmptySlice = data;
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

        // Override the base class event handler for order events
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            //update PCM with order statuses
            ((_Mom_Based_Rotation_PCM)(PortfolioConstruction)).AddOrderEvent(this, orderEvent);
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