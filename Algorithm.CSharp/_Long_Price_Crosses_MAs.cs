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

using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash. This is a skeleton
    /// framework you can use for designing an algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class _Long_Price_Crosses_MAs : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private int _strategy_cash = 10000;

        private Orders.OrderTicket _ge_buy_order = null;
        private Orders.OrderTicket _ge_stoploss_order = null;
        private Orders.OrderTicket _ge_sell_order = null;

        private decimal _ge_starting_price = 0;

        private Symbol _spy;
        private Symbol _ge;

        private SimpleMovingAverage _ge_ma3;
        private SimpleMovingAverage _ge_ma12;

        private AverageTrueRange _ge_atr;

        private RelativeStrengthIndex _ge_rsi;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetWarmUp(51, Resolution.Minute);
            _spy = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);
            _ge = QuantConnect.Symbol.Create("GE", SecurityType.Equity, Market.USA);

            SetStartDate(2017, 01, 01);  //Set Start Date
            SetEndDate(2017, 07, 01);    //Set End Date
            SetCash(_strategy_cash);             //Set Strategy Cash

            // Find more symbols here: http://quantconnect.com/data
            // Forex, CFD, Equities Resolutions: Tick, Second, Minute, Hour, Daily.
            // Futures Resolution: Tick, Second, Minute
            // Options Resolution: Minute Only.
            AddEquity(_spy.Value, Resolution.Minute);
            AddEquity(_ge.Value, Resolution.Minute);

            _ge_ma3 = SMA(_ge.Value, 3, Resolution.Minute);
            _ge_ma12 = SMA(_ge.Value, 12, Resolution.Minute);

            _ge_atr = ATR(_ge.Value, 12, type: MovingAverageType.Simple, resolution: Resolution.Minute);

            _ge_rsi = RSI(_ge.Value, 26, MovingAverageType.Wilders, Resolution.Minute);
            
            // There are other assets with similar methods. See "Selecting Options" etc for more details.
            // AddFuture, AddForex, AddCfd, AddOption
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (IsWarmingUp || data[_ge.Value].Time.Date <= StartDate.Date || !data.ContainsKey(_ge) || !_ge_atr.IsReady || !_ge_ma12.IsReady || !_ge_ma3.IsReady) return;

            if (_ge_starting_price == 0)
            {
                _ge_starting_price = data[_ge.Value].Open;
            }
            //_avg_macd_HISTOGRAM_3p = (_minus1_macd_HISTOGRAM + _minus2_macd_HISTOGRAM + _minus3_macd_HISTOGRAM) / 3;

            var holding = Portfolio[_ge.Value];

            if (data[_ge.Value].Time.Hour == 15 && data[_ge.Value].Time.Minute > 50)
            {
                if (holding.Quantity > 0)
                {
                    if (_ge_stoploss_order == null || _ge_stoploss_order.Status == Orders.OrderStatus.Canceled || _ge_stoploss_order.Status == Orders.OrderStatus.Filled || _ge_stoploss_order.Status == Orders.OrderStatus.Invalid || _ge_stoploss_order.Status == Orders.OrderStatus.None)
                    {
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                    else if (_ge_stoploss_order != null && (_ge_stoploss_order.Status == Orders.OrderStatus.New || _ge_stoploss_order.Status == Orders.OrderStatus.Submitted))
                    {
                        _ge_stoploss_order.Cancel();
                        Debug("Cancelling stop loss order.");
                        while (_ge_stoploss_order.Status != Orders.OrderStatus.Canceled)
                        {
                            Debug("Cancelling in progress...");
                        }
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                    else if (_ge_stoploss_order != null || _ge_stoploss_order.Status == Orders.OrderStatus.CancelPending)
                    {
                        Debug("Cancelling stop loss order.");
                        while (_ge_stoploss_order.Status != Orders.OrderStatus.Canceled)
                        {
                            Debug("Cancelling in progress...");
                        }
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                    else if (_ge_stoploss_order != null || _ge_stoploss_order.Status == Orders.OrderStatus.PartiallyFilled)
                    {
                        _ge_stoploss_order.Cancel();
                        Debug("Cancelling stop loss order.");
                        while (_ge_stoploss_order.Status != Orders.OrderStatus.Canceled)
                        {
                            Debug("Cancelling in progress...");
                        }
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                }
                
                // plot both lines
                //Plot("Portfolio", Portfolio.TotalPortfolioValue);
                //Plot(_ge.Value, data[_ge.Value].Open * System.Math.Floor(_strategy_cash / _ge_starting_price));

                return;
            }

            if (_ge_ma3 > _ge_ma12 && _ge_rsi < 30)//&& _dema6 >= _dema9 && _dema9 >= _dema12 && _dema12 >= _dema26 && _dema26 >= _dema50 && ((decimal)_atr)/((decimal)data[_ge.Value].Open) >= ((decimal)0.01))
            {
                if (holding.Quantity <= 0)
                {
                    int amount = System.Convert.ToInt32(System.Math.Floor(((0.98m*Portfolio.Cash) / System.Convert.ToDecimal(data[_ge.Value].Close))));
                    _ge_buy_order = MarketOrder(_ge.Value, amount, asynchronous : false);
                    Debug("Market order sent. Buy.");
                    _ge_stoploss_order = StopMarketOrder(_ge.Value, -1 * amount, (data[_ge.Value].Close - (_ge_atr / 2)));
                    Debug("Stop loss order sent.");
                }
            }

            if (_ge_ma3 <= _ge_ma12)//holding.Quantity >= 0 && _avg_macd_HISTOGRAM_3p != 0 && (data[_ge.Value].Close < _dema6 || ((_macd.Histogram - _avg_macd_HISTOGRAM_3p) / System.Math.Abs(_avg_macd_HISTOGRAM_3p)) <= _required_minimum_macd_HISTOGRAM_increase_HOLD) )
            {
                if (holding.Quantity > 0)
                {
                    if (_ge_stoploss_order == null || _ge_stoploss_order.Status == Orders.OrderStatus.Canceled || _ge_stoploss_order.Status == Orders.OrderStatus.Filled || _ge_stoploss_order.Status == Orders.OrderStatus.Invalid || _ge_stoploss_order.Status == Orders.OrderStatus.None)
                    {
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                    else if (_ge_stoploss_order != null && (_ge_stoploss_order.Status == Orders.OrderStatus.New || _ge_stoploss_order.Status == Orders.OrderStatus.Submitted))
                    {
                        _ge_stoploss_order.Cancel();
                        Debug("Cancelling stop loss order.");
                        while (_ge_stoploss_order.Status != Orders.OrderStatus.Canceled)
                        {
                            Debug("Cancelling in progress...");
                        }
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                    else if (_ge_stoploss_order != null || _ge_stoploss_order.Status == Orders.OrderStatus.CancelPending)
                    {
                        Debug("Cancelling stop loss order.");
                        while (_ge_stoploss_order.Status != Orders.OrderStatus.Canceled)
                        {
                            Debug("Cancelling in progress...");
                        }
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                    else if (_ge_stoploss_order != null || _ge_stoploss_order.Status == Orders.OrderStatus.PartiallyFilled)
                    {
                        _ge_stoploss_order.Cancel();
                        Debug("Cancelling stop loss order.");
                        while (_ge_stoploss_order.Status != Orders.OrderStatus.Canceled)
                        {
                            Debug("Cancelling in progress...");
                        }
                        holding = Portfolio[_ge.Value];
                        _ge_sell_order = MarketOrder(_ge.Value, -1 * holding.Quantity, asynchronous: false);
                        Debug("Market order sent. Sell.");
                    }
                }
            }
            
            // plot both lines
            //Plot("Portfolio", Portfolio.TotalPortfolioValue);
            //Plot(_ge.Value, data[_ge.Value].Open * System.Math.Floor(_strategy_cash / _ge_starting_price));
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
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