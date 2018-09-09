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
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Simple indicator demonstration algorithm of MACD
    /// </summary>
    /// <meta name="tag" content="indicators" />
    /// <meta name="tag" content="indicator classes" />
    /// <meta name="tag" content="plotting indicators" />
    public class _Long_MACD : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        //
        private DateTime _previous;
        private MovingAverageConvergenceDivergence _macd;
        private AverageTrueRange _atr;
        //

        private readonly string _symbolstr = "GE";
        private Symbol _symbol;

        private SymbolData _sd;

        private int _starting_cash = 10000;

        //
        private int _trend_state = 0;

        private decimal _minus1_macd_histo = 0;
        private decimal _minus2_macd_histo = 0;
        //


        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetBrokerageModel(Brokerages.BrokerageName.InteractiveBrokersBrokerage, AccountType.Cash);

            SetWarmUp(51, Resolution.Minute);

            AddSecurity(SecurityType.Equity, _symbolstr, Resolution.Minute);

            _symbol = Symbol(_symbolstr);

            SetStartDate(2018, 09, 06);
            SetEndDate(2018, 09, 06);
            
            SetCash(_starting_cash);

            SetBenchmark(_symbol.Value);
            
            

            _sd = new SymbolData(_symbol, this);

            // define our daily macd(12,26) with a 9 day signal
            _macd = MACD(_symbol, 12, 26, 9, MovingAverageType.Exponential, Resolution.Minute);
            _atr = ATR(_symbol, 9, MovingAverageType.Simple, Resolution.Minute);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            _sd.UpdateSymbol();

            // after warmup only
            if (IsWarmingUp)
                return;

            // only once per day
            if (_previous.Date == Time.Date) return;

            // only after macd is ready
            if (!_macd.IsReady) return;

            var holding = Portfolio[_symbol];

            decimal histoPrev2pAvg = (_minus1_macd_histo + _minus2_macd_histo) / 2;

            if ((_trend_state == 8 || _trend_state == 7 || _trend_state == 6 || _trend_state == 5) && _macd.Histogram > 0)
                _trend_state = 1;
            else if ((_trend_state == 1 || _trend_state == 2 || _trend_state == 3 || _trend_state == 4) && _macd.Histogram <= 0)
                _trend_state = 5;
            else if (_trend_state == 1 && _macd.Histogram > 0)
            {
                if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 2;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 3;
                else if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 4;
            }
            else if (_trend_state == 2 && _macd.Histogram > 0)
            {
                if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 2;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 3;
                else if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 4;
            }
            else if (_trend_state == 3 && _macd.Histogram > 0)
            {
                if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 3;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 3;
                else if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 4;
            }
            else if (_trend_state == 4 && _macd.Histogram > 0)
            {
                if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 4;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 4;
                else if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 4;
            }
            else if (_trend_state == 5 && _macd.Histogram <= 0)
            {
                if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 6;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 7;
                else if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 8;
            }
            else if (_trend_state == 6 && _macd.Histogram <= 0)
            {
                if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 6;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 7;
                else if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 8;
            }
            else if (_trend_state == 7 && _macd.Histogram <= 0)
            {
                if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 7;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 7;
                else if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 8;
            }
            else if (_trend_state == 8 && _macd.Histogram <= 0)
            {
                if (_macd.Histogram < histoPrev2pAvg)
                    _trend_state = 8;
                else if (_macd.Histogram == histoPrev2pAvg)
                    _trend_state = 8;
                else if (_macd.Histogram > histoPrev2pAvg)
                    _trend_state = 8;
            }
            else if (_trend_state == 0 && _macd.Histogram > 0)
            {
                if (_minus1_macd_histo < 0 && _macd.Histogram > 0)
                    _trend_state = 1;
                else if (_minus1_macd_histo > 0 && _macd.Histogram < 0)
                    _trend_state = 5;
            }

            // if our macd histogram just crossed, then let's go long
            if (holding.Quantity <= 0 && (_trend_state == 8))
            {
                // longterm says buy as well
                SetHoldings(_symbol, 1.0);
            }
            // of our macd histogram flattened out on uptrend, then let's go short
            else if (holding.Quantity >= 0 && (_trend_state == 3 || _trend_state == 4 || _trend_state == 5 || _trend_state == 6))
            {
                SetHoldings(_symbol, 0);
            }

            // plot both lines
            //Plot("MACD", _macd, _macd.Signal);
            //Plot(_symbol, "Open", data[_symbol].Open);
            //Plot(_symbol, _macd.Fast, _macd.Slow);
            Plot("Portfolio", Portfolio.TotalPortfolioValue);

            _previous = Time;

            _minus2_macd_histo = _minus1_macd_histo;
            _minus1_macd_histo = _macd.Histogram;
        }

        public class SymbolData
        {
            private Symbol _symbol;

            private QCAlgorithm _algorithm;

            private RollingWindow<TradeBar> _rw;

            private RollingWindow<IndicatorDataPoint> _rw_ATR;

            private RollingWindow<IndicatorDataPoint> _rw_MACD;
            private RollingWindow<IndicatorDataPoint> _rw_MACD_histogram;
            private RollingWindow<IndicatorDataPoint> _rw_MACD_signal;

            private RollingWindow<Decimal> _rw_MACD_histogram_rate_of_change;

            //private RollingWindow<IndicatorDataPoint> _rw_EMA9;
            //private RollingWindow<IndicatorDataPoint> _rw_EMA12;
            //private RollingWindow<IndicatorDataPoint> _rw_EMA26;

            public SymbolData(Symbol symbol, QCAlgorithm algorithm)
            {
                _symbol = symbol;
                _algorithm = algorithm;
                _rw = new RollingWindow<TradeBar>(4);

                // Creates indicators and adds to a rolling window when updated
                //MACD + MACD Histogram
                var macd = _algorithm.MACD(_symbol, 12, 26, 9, MovingAverageType.Exponential, Resolution.Minute);

                macd.Updated += (sender, updated) => _rw_MACD.Add(updated);
                _rw_MACD = new RollingWindow<IndicatorDataPoint>(4);

                macd.Histogram.Updated += (sender, updated) => _rw_MACD_histogram.Add(updated);
                _rw_MACD_histogram = new RollingWindow<IndicatorDataPoint>(4);

                macd.Signal.Updated += (sender, updated) => _rw_MACD_signal.Add(updated);
                _rw_MACD_signal = new RollingWindow<IndicatorDataPoint>(4);

                macd.Signal.Updated += (sender, updated) => _rw_MACD_histogram_rate_of_change.Add(updated);
                _rw_MACD_signal = new RollingWindow<IndicatorDataPoint>(4);

                //ATR
                var atr = _algorithm.ATR(_symbol, 26, MovingAverageType.Simple, Resolution.Minute);

                atr.Updated += (sender, updated) => _rw_ATR.Add(updated);
                _rw_ATR = new RollingWindow<IndicatorDataPoint>(4);

                //EMA9, 12, 26
                //var ema9 = _algorithm.EMA(_symbol, 9, Resolution.Minute);
                //var ema12 = _algorithm.EMA(_symbol, 12, Resolution.Minute);
                //var ema26 = _algorithm.EMA(_symbol, 26, Resolution.Minute);

                //ema9.Updated += (sender, updated) => _rw_EMA9.Add(updated);
                //_rw_EMA9 = new RollingWindow<IndicatorDataPoint>(4);

                //ema12.Updated += (sender, updated) => _rw_EMA12.Add(updated);
                //_rw_EMA12 = new RollingWindow<IndicatorDataPoint>(4);

                //ema26.Updated += (sender, updated) => _rw_EMA26.Add(updated);
                //_rw_EMA26 = new RollingWindow<IndicatorDataPoint>(4);
            }

            public bool UpdateSymbol()
            {
                try
                {
                    _rw.Add(_algorithm.CurrentSlice[_symbol.Value]);
                    return true;
                }
                catch (Exception e)
                {
                    return false;
                }
            }

            public bool UpdateMACDHistogramRateOfChange()
            {
                _rw_MACD_histogram_rate_of_change
                return true;
            }

            /// <summary>
            /// This returns the state of the trend for the symbol at the time.
            /// 
            /// 0 - nothing
            /// 1 - histogram crossed 0 on the way up
            /// 2 - histogram rising in uptrend
            /// 3 - histogram 'flatted' in uptrend
            /// 4 - histogram falling in uptrend
            /// 5 - histogram crossed below 0
            /// 6 - histogram falling in downtrend
            /// 7 - histogram 'flatted' in the downtrend
            /// 8 - histogram rising within downtrend
            /// </summary>
            /// <returns></returns>
            /*public int GetTrendState()
            {
                int trend_state;

                if ((_trend_state == 8 || _trend_state == 7 || _trend_state == 6 || _trend_state == 5) && _macd.Histogram > 0)
                    _trend_state = 1;
                else if ((_trend_state == 1 || _trend_state == 2 || _trend_state == 3 || _trend_state == 4) && _macd.Histogram <= 0)
                    _trend_state = 5;
                else if (_trend_state == 1 && _macd.Histogram > 0)
                {
                    if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 2;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 3;
                    else if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 4;
                }
                else if (_trend_state == 2 && _macd.Histogram > 0)
                {
                    if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 2;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 3;
                    else if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 4;
                }
                else if (_trend_state == 3 && _macd.Histogram > 0)
                {
                    if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 3;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 3;
                    else if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 4;
                }
                else if (_trend_state == 4 && _macd.Histogram > 0)
                {
                    if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 4;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 4;
                    else if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 4;
                }
                else if (_trend_state == 5 && _macd.Histogram <= 0)
                {
                    if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 6;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 7;
                    else if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 8;
                }
                else if (_trend_state == 6 && _macd.Histogram <= 0)
                {
                    if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 6;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 7;
                    else if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 8;
                }
                else if (_trend_state == 7 && _macd.Histogram <= 0)
                {
                    if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 7;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 7;
                    else if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 8;
                }
                else if (_trend_state == 8 && _macd.Histogram <= 0)
                {
                    if (_macd.Histogram < histoPrev2pAvg)
                        _trend_state = 8;
                    else if (_macd.Histogram == histoPrev2pAvg)
                        _trend_state = 8;
                    else if (_macd.Histogram > histoPrev2pAvg)
                        _trend_state = 8;
                }
                else if (_trend_state == 0 && _macd.Histogram > 0)
                {
                    if (_minus1_macd_histo < 0 && _macd.Histogram > 0)
                        _trend_state = 1;
                    else if (_minus1_macd_histo > 0 && _macd.Histogram < 0)
                        _trend_state = 5;
                }
                return 0;
            }*/
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
            {"Total Trades", "84"},
            {"Average Win", "4.78%"},
            {"Average Loss", "-4.16%"},
            {"Compounding Annual Return", "2.958%"},
            {"Drawdown", "34.800%"},
            {"Expectancy", "0.228"},
            {"Net Profit", "37.837%"},
            {"Sharpe Ratio", "0.297"},
            {"Loss Rate", "43%"},
            {"Win Rate", "57%"},
            {"Profit-Loss Ratio", "1.15"},
            {"Alpha", "0.107"},
            {"Beta", "-3.51"},
            {"Annual Standard Deviation", "0.124"},
            {"Annual Variance", "0.015"},
            {"Information Ratio", "0.136"},
            {"Tracking Error", "0.125"},
            {"Treynor Ratio", "-0.011"},
            {"Total Fees", "$443.50"}
        };
    }
}

        