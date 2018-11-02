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
    public partial class _QCWhatAlgo : QCAlgorithmFramework
    {
        private readonly DateTime _startDate = new DateTime(2007, 01, 01);
        private readonly DateTime _endDate = new DateTime(2018, 10, 04);
        private readonly Resolution _resolution = Resolution.Minute;

        private readonly Decimal _backtestCash = 10000;

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
                QuantConnect.Symbol.Create("BRKb", SecurityType.Equity, Market.USA),  //"Berkshire Hathaway B"
                QuantConnect.Symbol.Create("C", SecurityType.Equity, Market.USA),  //"Citigroup"
                QuantConnect.Symbol.Create("CMCSA", SecurityType.Equity, Market.USA),  //"Comcast"
                QuantConnect.Symbol.Create("CSCO", SecurityType.Equity, Market.USA),  //"Cisco"
                QuantConnect.Symbol.Create("CVX", SecurityType.Equity, Market.USA),  //"Chevron"
                QuantConnect.Symbol.Create("DIS", SecurityType.Equity, Market.USA),  //"Walt Disney"
                QuantConnect.Symbol.Create("FB", SecurityType.Equity, Market.USA),  //"Facebook"
                QuantConnect.Symbol.Create("GOOGL", SecurityType.Equity, Market.USA),  //"Alphabet A"
                QuantConnect.Symbol.Create("HD", SecurityType.Equity, Market.USA),  //"Home Depot"
                QuantConnect.Symbol.Create("INTC", SecurityType.Equity, Market.USA),  //"Intel"
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
                QuantConnect.Symbol.Create("XOM", SecurityType.Equity, Market.USA),  //"Exxon Mobil"
                
               	QuantConnect.Symbol.Create("NBB", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("NBD", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("DMB", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("DSM", SecurityType.Equity, Market.USA),  //""
                
                QuantConnect.Symbol.Create("SLP", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("AU", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("SPKE", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("MTL", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("VIRT", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("MUX", SecurityType.Equity, Market.USA),  //""
                
                QuantConnect.Symbol.Create("MKC", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("STZ", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("EL", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("COST", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("CLX", SecurityType.Equity, Market.USA),  //""
                QuantConnect.Symbol.Create("LMT", SecurityType.Equity, Market.USA),  //""
                

                
                }
            },

            {"Safe_Heaven_Universe",
                new List<Symbol>
                {
                QuantConnect.Symbol.Create("TIP", SecurityType.Equity, Market.USA),  //"iShares TIPS Bond" 
                QuantConnect.Symbol.Create("TLH", SecurityType.Equity, Market.USA),  //"iShares 10-20 Year Treasury Bond" //NBB, NBD, DSM, DMB for regular equity alternative
                QuantConnect.Symbol.Create("IVV", SecurityType.Equity, Market.USA), //"iShares S&P 500"

                }
            },
        };

        public readonly IDictionary<String, String> AlphaUniverse = new Dictionary<string, string>()
        {
            { "Mom_Based_Alpha", "_QCWHAT-MOM_BASED_ALPHA_UNIVERSE-EQUITY-USA 2T" },
            { "Save_Heaven_Constant_Alpha", "_QCWHAT-SAFE_HEAVEN_UNIVERSE-EQUITY-USA 2T" }
        };

        private readonly int _momentumPeriod = 275;
        private readonly Resolution _momentumResolution = Resolution.Daily;
        private readonly int _numberOfTopStocks = 5;

        //actual rebalance of the portfolio or placement of orders based portfolio targets should happen at these times
        private readonly int _rebalanceHour = 8;
        private readonly int _rebalanceMinute = 30;
        private readonly DayOfWeek[] _rebalanceDaysOfWeek = new DayOfWeek[] { DayOfWeek.Wednesday };
        private Scheduling.ITimeRule _rebalanceTime;
        private Scheduling.IDateRule _rebalanceDate;

        private bool _isRebalancingNow = false;

        private Slice _lastNonEmptySlice;

        public IUniverseSelectionModel UniverseSelection;
        public IAlphaModel CompositeAlphaModel;
        public _QCWhatPortfolioConstrModel PortfolioConstruction;

        //public _QCWhatMomBasedAlphaModel MomBasedRotationAlphaModel;
        public _QCWhatMomentumAlphaModel MomBasedRotationAlphaModel;

        public _QCWhatSafeHeavenConstantAlphaModel SaveHeavenConstantAlphaModel;

        public override void Initialize()
        {
            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.TotalReturn));
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

            UniverseSelection = new _QCWhatSelectionModel(_symbols, this.UniverseSettings, this.SecurityInitializer);
            SetUniverseSelection(UniverseSelection);

            //MomBasedRotationAlphaModel = new _QCWhatMomBasedAlphaModel(AlphaUniverse, "Mom_Based_Alpha", _momentumPeriod, _momentumResolution, _resolution, _rebalanceDate, _rebalanceTime);
            MomBasedRotationAlphaModel = new _QCWhatMomentumAlphaModel();
            SaveHeavenConstantAlphaModel = new _QCWhatSafeHeavenConstantAlphaModel(AlphaUniverse, "Save_Heaven_Constant_Alpha", InsightType.Price, InsightDirection.Flat, TimeSpan.FromDays(5), _rebalanceDate, _rebalanceTime);
            CompositeAlphaModel = new CompositeAlphaModel(
                MomBasedRotationAlphaModel,
                SaveHeavenConstantAlphaModel
                );
            SetAlpha(CompositeAlphaModel);

            PortfolioConstruction = new _QCWhatPortfolioConstrModel(AlphaUniverse, _backtestCash, _rebalanceDate, _rebalanceTime);
            PortfolioConstruction.InitMomBasedPortfolioConstruction(this, "Mom_Based_Alpha", _numberOfTopStocks);
            PortfolioConstruction.InitSaveHeavenConstantBasedPortfolioConstruction(this, "Save_Heaven_Constant_Alpha", 3);
            SetPortfolioConstruction(PortfolioConstruction);

            SetRiskManagement(new _QCWhatRiskMgmtModel(0.15m));

            SetExecution(new _QCWhatExecutionModel());

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
                OnFrameworkData(_lastNonEmptySlice);
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
            ((_QCWhatPortfolioConstrModel)(PortfolioConstruction)).AddOrderEvent(this, orderEvent);
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