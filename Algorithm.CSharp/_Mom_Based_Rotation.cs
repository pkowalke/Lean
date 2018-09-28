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

using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Securities;


namespace QuantConnect.Algorithm.CSharp
{

    /// <meta name="tag" content="MOM" />
    /// <meta name="tag" content="Momentum" />
    public class _Mom_Based_Rotation : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private readonly DateTime _startDate = new DateTime(2018, 08, 15);
        private readonly DateTime _endDate = new DateTime(2018, 09, 16);
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
        private readonly int _numberOfTopStocks = 2;

        private Dictionary<String, Momentum> _momentum = new Dictionary<String, Momentum>();
        private Dictionary<String, Decimal> _momentum2 = new Dictionary<String, Decimal>();
        private readonly int _momentumPeriod = 6 * 21;
        private Dictionary<String, RollingWindow<TradeBar>> _rws = new Dictionary<string, RollingWindow<TradeBar>>();

        public override void Initialize()
        {
            SetBrokerageModel(Brokerages.BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_backtestCash);
            SetWarmup(_momentumPeriod);

            foreach (String s in _symbolStrs)
            {
                
                AddEquity(s, _resolution, Market.USA, fillDataForward: true, leverage: 0, extendedMarketHours: false);
                _momentum.Add(s, MOM(Symbol(s), _momentumPeriod, _resolution));

                _rws.Add(s, new RollingWindow<TradeBar>(_momentumPeriod+1));
            }

            Schedule.On(DateRules.Every(DayOfWeek.Wednesday), TimeRules.AfterMarketOpen("BA", 1), Rebalance);
        }

        public void Rebalance()
        {
            if (IsWarmingUp) return;

            foreach (String s in _symbolStrs)
            {
                if (CurrentSlice.Bars.ContainsKey(s))
                {
                    _momentum2[s] = _rws[s][0].Value - _rws[s][_momentumPeriod].Value;
                }
            }

            List<Orders.OrderTicket> ongoingOrders = 
                Transactions
                .GetOrderTickets(x => 
                x.Status == Orders.OrderStatus.CancelPending ||
                x.Status == Orders.OrderStatus.Invalid ||
                x.Status == Orders.OrderStatus.New ||
                x.Status == Orders.OrderStatus.None ||
                x.Status == Orders.OrderStatus.PartiallyFilled ||
                x.Status == Orders.OrderStatus.Submitted).ToList();

            foreach (Orders.OrderTicket ot in ongoingOrders)
            {
                ot.Cancel();
                int cancelTimeout = 60000; //if order does not get cancelled after 1 minute, than notify the user.
                while (ot.Status != Orders.OrderStatus.Canceled)
                {
                    cancelTimeout -= 5000;
                    Thread.Sleep(5000);
                    Debug(String.Format("{0} Order ticket: {1} has been issued cancel request. Waiting for the order to get cancelled.", Time.ToString(), ot.OrderId.ToString()));
                    if (cancelTimeout <= 0) Debug(String.Format("{0} * Warning * Order ticket: {1}, tag: {2} failed to cancel after 1 minute or more.", Time.ToString(), ot.OrderId.ToString(), ot.Tag.ToString()));
                }
            }

            Dictionary<String, Decimal> investedHoldings =
                (from ih in Portfolio
                 where ih.Value.Invested == true
                 select new { symbol = ih.Value.Symbol.Value.ToString(), qty = ih.Value.Quantity })
                .ToDictionary(t => t.symbol, t => t.qty);

            Dictionary<String, Decimal> topPicks =
                (from top_pair in _momentum
                 where top_pair.Value > 0
                 orderby top_pair.Value descending
                 select new { symbol = top_pair.Key.ToString(), qty = CalculateOrderQuantity(top_pair.Key.ToString(), 1m / _numberOfTopStocks) })
                .Take(_numberOfTopStocks)
                .ToDictionary(t => t.symbol, t => t.qty);

            Dictionary<String, Decimal> liquidateHoldings =
                (from ih in investedHoldings
                 where !topPicks.ContainsKey(ih.Key)
                 select new { symbol = ih.Key, qty = ih.Value })
                .ToDictionary(t => t.symbol, t => t.qty);

            Decimal estimatedProceeds = 
                (from p in Portfolio
                 join lh in liquidateHoldings
                 on p.Value.Symbol.Value.ToString() equals lh.Key
                 select p.Value.HoldingsValue).Sum();

            int numberOfSymbolsToAcquire =
                (from tp in topPicks
                where !investedHoldings.ContainsKey(tp.Key)
                select tp.Key.ToString())
                .Count();

            Decimal estimatedRemainingPower = estimatedProceeds + Portfolio.Cash - _numberOfTopStocks * 4; // keep 4 * number of top stocks to cover fees

            Dictionary<String, Decimal> acquireHoldings =
                (from tp in topPicks
                 where !investedHoldings.ContainsKey(tp.Key)
                 select new { symbol = tp.Key.ToString(), qty = CalculateOrderQuantity(tp.Key.ToString(), (1.0m / numberOfSymbolsToAcquire) * (estimatedRemainingPower / Portfolio.TotalPortfolioValue)) })
                .ToDictionary(t => t.symbol, t => t.qty);

            PlaceOrders(liquidateHoldings, acquireHoldings);

            //foreach (var s in liquidateHoldings) Liquidate(s.ToString());

            //Double remainingCash = Convert.ToDouble(Portfolio.Cash);
            //Double totalPortfolioValue = Convert.ToDouble(Portfolio.TotalPortfolioValue);

            //foreach (var s in acquireHoldings) SetHoldings(s.ToString(), (1.0 * remainingCash / totalPortfolioValue) / Convert.ToDouble(acquireHoldings.Count()));
        }

        public void PlaceOrders(Dictionary<String, Decimal> sell, Dictionary<String, Decimal> buy)
        {
            
            foreach (var s in sell)
            {
                if (s.Value <= 0)
                    Debug(String.Format("{0} * Warning * Invalid quantity. Symbol: {1}, Qty: {2}", Time.ToString(), s.Key, s.Value.ToString()));
                else
                    Debug(String.Format("{0} Placing sell order. Symbol: {1}, Qty: {2} Ticket #{3}.", Time.ToString() ,s.Key, s.Value.ToString(), MarketOrder(s.Key, -1m * s.Value, asynchronous: false).OrderId.ToString()));
                
            }

            foreach (var s in buy)
            {
                if (s.Value <= 0)
                    Debug(String.Format("{0} * Warning * Invalid quantity. Symbol: {1}, Qty: {2}", Time.ToString(), s.Key, s.Value.ToString()));
                else
                    Debug(String.Format("{0} Placing buy order. Symbol: {1}, Qty: {2} Ticket #{3}.", Time.ToString(), s.Key, s.Value.ToString(), MarketOrder(s.Key, s.Value, asynchronous: false).OrderId.ToString()));
            }
        }

        public override void OnData(Slice data)
        {
            foreach (String s in _symbolStrs)
            {
                if (data.ContainsKey(Symbol(s)))
                {
                    _rws[s].Add(data.Bars[s]);
                }
            }
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
