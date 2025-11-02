using System;
using System.Collections.Generic;

namespace TradingLibrary.Core
{
    /// <summary>
    /// Price action primitives: candle geometry, True Range, Directional Movement,
    /// volatility estimators, Heikin-Ashi, and swing detection.
    /// Reference: SharedLibrary/Core specification - PriceAction section
    /// 
    /// Key principles:
    /// - All functions are stateless (batch) unless in a State class
    /// - NaN propagation: invalid inputs → NaN outputs
    /// - Index 0 → oldest bar
    /// - TrueRange/DM require previous close → first bar = NaN
    /// </summary>
    public static class PriceAction
    {
        #region Candle Geometry

        /// <summary>
        /// Bar range: High - Low
        /// </summary>
        public static double Range(double high, double low)
        {
            if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                return MathBase.NaN;
            return high - low;
        }

        /// <summary>
        /// Real body size: |Close - Open|
        /// </summary>
        public static double RealBody(double open, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(close))
                return MathBase.NaN;
            return Math.Abs(close - open);
        }

        /// <summary>
        /// Upper wick (upper shadow): High - max(Open, Close)
        /// </summary>
        public static double UpperWick(double open, double high, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(high) || !MathBase.IsFinite(close))
                return MathBase.NaN;
            return high - Math.Max(open, close);
        }

        /// <summary>
        /// Lower wick (lower shadow): min(Open, Close) - Low
        /// </summary>
        public static double LowerWick(double open, double low, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                return MathBase.NaN;
            return Math.Min(open, close) - low;
        }

        /// <summary>
        /// Body-to-range ratio: |Close - Open| / (High - Low)
        /// Useful for detecting dojis (small body relative to range).
        /// Returns NaN if range is zero.
        /// </summary>
        public static double BodyToRange(double open, double high, double low, double close)
        {
            double body = RealBody(open, close);
            double range = Range(high, low);
            return MathBase.SafeDivide(body, range);
        }

        /// <summary>
        /// Checks if bar is bullish (close > open with tolerance).
        /// </summary>
        public static bool IsBull(double open, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(close))
                return false;
            return close > open + MathBase.EPS;
        }

        /// <summary>
        /// Checks if bar is bearish (close < open with tolerance).
        /// </summary>
        public static bool IsBear(double open, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(close))
                return false;
            return close < open - MathBase.EPS;
        }

        /// <summary>
        /// Checks if bar is doji (body ≤ maxBodyRatio * range).
        /// Default maxBodyRatio = 0.1 (10% of range).
        /// </summary>
        public static bool IsDoji(double open, double high, double low, double close, double maxBodyRatio = 0.1)
        {
            double ratio = BodyToRange(open, high, low, close);
            if (!MathBase.IsFinite(ratio))
                return false;
            return ratio <= maxBodyRatio;
        }

        /// <summary>
        /// Checks if current bar is inside previous bar.
        /// Inside bar: H[i] ≤ H[i-1] AND L[i] ≥ L[i-1]
        /// </summary>
        public static bool IsInsideBar(double prevHigh, double prevLow, double currHigh, double currLow)
        {
            if (!MathBase.IsFinite(prevHigh) || !MathBase.IsFinite(prevLow) ||
                !MathBase.IsFinite(currHigh) || !MathBase.IsFinite(currLow))
                return false;
            return currHigh <= prevHigh && currLow >= prevLow;
        }

        /// <summary>
        /// Checks if current bar is outside previous bar.
        /// Outside bar: H[i] > H[i-1] AND L[i] < L[i-1]
        /// </summary>
        public static bool IsOutsideBar(double prevHigh, double prevLow, double currHigh, double currLow)
        {
            if (!MathBase.IsFinite(prevHigh) || !MathBase.IsFinite(prevLow) ||
                !MathBase.IsFinite(currHigh) || !MathBase.IsFinite(currLow))
                return false;
            return currHigh > prevHigh && currLow < prevLow;
        }

        /// <summary>
        /// Checks for gap up: Low[i] > High[i-1]
        /// </summary>
        public static bool GapUp(double prevHigh, double currLow)
        {
            if (!MathBase.IsFinite(prevHigh) || !MathBase.IsFinite(currLow))
                return false;
            return currLow > prevHigh + MathBase.EPS;
        }

        /// <summary>
        /// Checks for gap down: High[i] < Low[i-1]
        /// </summary>
        public static bool GapDown(double prevLow, double currHigh)
        {
            if (!MathBase.IsFinite(prevLow) || !MathBase.IsFinite(currHigh))
                return false;
            return currHigh < prevLow - MathBase.EPS;
        }

        /// <summary>
        /// Checks if bar is Narrow Range (NR) bar.
        /// NRn: current range is smallest of last n bars.
        /// </summary>
        /// <param name="ranges">Array of ranges (must have at least n values)</param>
        /// <param name="n">Lookback period (default 4 for NR4, 7 for NR7)</param>
        /// <returns>True if current (last) range is smallest in window</returns>
        public static bool IsNRn(double[] ranges, int n = 4)
        {
            if (ranges == null || ranges.Length < n)
                return false;

            int start = ranges.Length - n;
            double currentRange = ranges[ranges.Length - 1];

            if (!MathBase.IsFinite(currentRange))
                return false;

            for (int i = start; i < ranges.Length - 1; i++)
            {
                if (!MathBase.IsFinite(ranges[i]))
                    return false;
                if (ranges[i] <= currentRange)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if bar is Wide Range (WR) bar.
        /// WRn: current range is largest of last n bars.
        /// </summary>
        public static bool IsWRn(double[] ranges, int n = 4)
        {
            if (ranges == null || ranges.Length < n)
                return false;

            int start = ranges.Length - n;
            double currentRange = ranges[ranges.Length - 1];

            if (!MathBase.IsFinite(currentRange))
                return false;

            for (int i = start; i < ranges.Length - 1; i++)
            {
                if (!MathBase.IsFinite(ranges[i]))
                    return false;
                if (ranges[i] >= currentRange)
                    return false;
            }

            return true;
        }

        #endregion

        #region Returns & Changes

        /// <summary>
        /// Simple price change: price[i] - price[i-1]
        /// First bar returns NaN (no previous value).
        /// </summary>
        public static double[] Change(double[] prices)
        {
            if (prices == null || prices.Length == 0)
                return null;

            int n = prices.Length;
            double[] result = new double[n];
            result[0] = MathBase.NaN;

            for (int i = 1; i < n; i++)
            {
                if (!MathBase.IsFinite(prices[i]) || !MathBase.IsFinite(prices[i - 1]))
                    result[i] = MathBase.NaN;
                else
                    result[i] = prices[i] - prices[i - 1];
            }

            return result;
        }

        /// <summary>
        /// Percent change: (price[i] - price[i-1]) / price[i-1] * 100
        /// First bar returns NaN.
        /// </summary>
        public static double[] PercentChange(double[] prices)
        {
            if (prices == null || prices.Length == 0)
                return null;

            int n = prices.Length;
            double[] result = new double[n];
            result[0] = MathBase.NaN;

            for (int i = 1; i < n; i++)
            {
                result[i] = MathBase.PercentChange(prices[i], prices[i - 1]);
            }

            return result;
        }

        /// <summary>
        /// Log return: ln(price[i] / price[i-1])
        /// First bar returns NaN.
        /// More appropriate for multiplicative processes.
        /// </summary>
        public static double[] LogReturn(double[] prices)
        {
            if (prices == null || prices.Length == 0)
                return null;

            int n = prices.Length;
            double[] result = new double[n];
            result[0] = MathBase.NaN;

            for (int i = 1; i < n; i++)
            {
                if (!MathBase.IsFinite(prices[i]) || !MathBase.IsFinite(prices[i - 1]))
                    result[i] = MathBase.NaN;
                else if (prices[i - 1] <= 0 || prices[i] <= 0)
                    result[i] = MathBase.NaN;
                else
                    result[i] = Math.Log(prices[i] / prices[i - 1]);
            }

            return result;
        }

        /// <summary>
        /// Cumulative return from series of returns.
        /// Can use simple returns: Π(1 + r) - 1
        /// Or log returns: exp(Σ log_r) - 1
        /// </summary>
        /// <param name="returns">Array of returns (simple or log)</param>
        /// <param name="isLogReturns">True if returns are log returns</param>
        /// <returns>Cumulative return</returns>
        public static double CumulativeReturn(double[] returns, bool isLogReturns = false)
        {
            if (returns == null || returns.Length == 0)
                return MathBase.NaN;

            if (isLogReturns)
            {
                // Sum log returns
                double sum = 0;
                foreach (var r in returns)
                {
                    if (!MathBase.IsFinite(r))
                        return MathBase.NaN;
                    sum += r;
                }
                return Math.Exp(sum) - 1.0;
            }
            else
            {
                // Product of (1 + return)
                double product = 1.0;
                foreach (var r in returns)
                {
                    if (!MathBase.IsFinite(r))
                        return MathBase.NaN;
                    product *= (1.0 + r);
                }
                return product - 1.0;
            }
        }

        #endregion

        #region True Range & Directional Movement (Wilder)

        /// <summary>
        /// True Range: max(H - L, |H - C_prev|, |L - C_prev|)
        /// Batch version: first bar returns NaN (no previous close).
        /// </summary>
        public static double[] TrueRange(double[] high, double[] low, double[] close)
        {
            if (high == null || low == null || close == null)
                return null;
            if (high.Length != low.Length || high.Length != close.Length)
                throw new ArgumentException("Arrays must have same length");

            int n = high.Length;
            double[] result = new double[n];
            result[0] = MathBase.NaN; // No previous close

            for (int i = 1; i < n; i++)
            {
                if (!MathBase.IsFinite(high[i]) || !MathBase.IsFinite(low[i]) || 
                    !MathBase.IsFinite(close[i]) || !MathBase.IsFinite(close[i - 1]))
                {
                    result[i] = MathBase.NaN;
                    continue;
                }

                double tr1 = high[i] - low[i];
                double tr2 = Math.Abs(high[i] - close[i - 1]);
                double tr3 = Math.Abs(low[i] - close[i - 1]);
                result[i] = Math.Max(tr1, Math.Max(tr2, tr3));
            }

            return result;
        }

        /// <summary>
        /// Single bar True Range calculation.
        /// </summary>
        public static double TrueRange(double high, double low, double close, double prevClose)
        {
            if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low) || 
                !MathBase.IsFinite(close) || !MathBase.IsFinite(prevClose))
                return MathBase.NaN;

            double tr1 = high - low;
            double tr2 = Math.Abs(high - prevClose);
            double tr3 = Math.Abs(low - prevClose);
            return Math.Max(tr1, Math.Max(tr2, tr3));
        }

        /// <summary>
        /// Directional Movement Plus (+DM).
        /// +DM = (H[i] - H[i-1]) if up move > down move AND > 0, else 0
        /// First bar returns NaN.
        /// </summary>
        public static double[] DirectionalMovementPlus(double[] high, double[] low)
        {
            if (high == null || low == null || high.Length != low.Length)
                return null;

            int n = high.Length;
            double[] result = new double[n];
            result[0] = MathBase.NaN;

            for (int i = 1; i < n; i++)
            {
                if (!MathBase.IsFinite(high[i]) || !MathBase.IsFinite(high[i - 1]) ||
                    !MathBase.IsFinite(low[i]) || !MathBase.IsFinite(low[i - 1]))
                {
                    result[i] = MathBase.NaN;
                    continue;
                }

                double upMove = high[i] - high[i - 1];
                double downMove = low[i - 1] - low[i];

                if (upMove > downMove && upMove > 0)
                    result[i] = upMove;
                else
                    result[i] = 0;
            }

            return result;
        }

        /// <summary>
        /// Directional Movement Minus (-DM).
        /// -DM = (L[i-1] - L[i]) if down move > up move AND > 0, else 0
        /// First bar returns NaN.
        /// </summary>
        public static double[] DirectionalMovementMinus(double[] high, double[] low)
        {
            if (high == null || low == null || high.Length != low.Length)
                return null;

            int n = high.Length;
            double[] result = new double[n];
            result[0] = MathBase.NaN;

            for (int i = 1; i < n; i++)
            {
                if (!MathBase.IsFinite(high[i]) || !MathBase.IsFinite(high[i - 1]) ||
                    !MathBase.IsFinite(low[i]) || !MathBase.IsFinite(low[i - 1]))
                {
                    result[i] = MathBase.NaN;
                    continue;
                }

                double upMove = high[i] - high[i - 1];
                double downMove = low[i - 1] - low[i];

                if (downMove > upMove && downMove > 0)
                    result[i] = downMove;
                else
                    result[i] = 0;
            }

            return result;
        }

        /// <summary>
        /// Stateful True Range calculator.
        /// Maintains previous close for continuous TR calculation.
        /// </summary>
        public class TrueRangeState
        {
            private double _prevClose = MathBase.NaN;
            public double Value { get; private set; } = MathBase.NaN;

            public double Update(double high, double low, double close)
            {
                if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                {
                    Value = MathBase.NaN;
                    _prevClose = close;
                    return Value;
                }

                if (!MathBase.IsFinite(_prevClose))
                {
                    Value = MathBase.NaN; // First bar
                }
                else
                {
                    Value = TrueRange(high, low, close, _prevClose);
                }

                _prevClose = close;
                return Value;
            }

            public void Reset()
            {
                _prevClose = MathBase.NaN;
                Value = MathBase.NaN;
            }
        }

        /// <summary>
        /// Stateful Directional Movement calculator.
        /// Maintains previous high/low for +DM and -DM.
        /// </summary>
        public class DirectionalMovementState
        {
            private double _prevHigh = MathBase.NaN;
            private double _prevLow = MathBase.NaN;

            public double PlusDM { get; private set; } = MathBase.NaN;
            public double MinusDM { get; private set; } = MathBase.NaN;

            public void Update(double high, double low)
            {
                if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                {
                    PlusDM = MathBase.NaN;
                    MinusDM = MathBase.NaN;
                    _prevHigh = high;
                    _prevLow = low;
                    return;
                }

                if (!MathBase.IsFinite(_prevHigh) || !MathBase.IsFinite(_prevLow))
                {
                    PlusDM = MathBase.NaN;
                    MinusDM = MathBase.NaN;
                }
                else
                {
                    double upMove = high - _prevHigh;
                    double downMove = _prevLow - low;

                    if (upMove > downMove && upMove > 0)
                        PlusDM = upMove;
                    else
                        PlusDM = 0;

                    if (downMove > upMove && downMove > 0)
                        MinusDM = downMove;
                    else
                        MinusDM = 0;
                }

                _prevHigh = high;
                _prevLow = low;
            }

            public void Reset()
            {
                _prevHigh = MathBase.NaN;
                _prevLow = MathBase.NaN;
                PlusDM = MathBase.NaN;
                MinusDM = MathBase.NaN;
            }
        }

        #endregion

        #region OHLC Volatility Estimators

        /// <summary>
        /// Parkinson volatility estimator (per-bar variance).
        /// σ² = [ln(H/L)]² / (4·ln(2))
        /// Efficient estimator using only High and Low.
        /// </summary>
        public static double ParkinsonVariance(double high, double low)
        {
            if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                return MathBase.NaN;
            if (high <= 0 || low <= 0)
                return MathBase.NaN;
            if (high < low)
                return MathBase.NaN;

            double logHL = Math.Log(high / low);
            return (logHL * logHL) / (4.0 * Math.Log(2.0));
        }

        /// <summary>
        /// Garman-Klass volatility estimator (per-bar variance).
        /// σ² = 0.5·[ln(H/L)]² - (2·ln(2)-1)·[ln(C/O)]²
        /// More efficient than Parkinson, uses OHLC.
        /// </summary>
        public static double GarmanKlassVariance(double open, double high, double low, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(high) || 
                !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                return MathBase.NaN;
            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                return MathBase.NaN;

            double logHL = Math.Log(high / low);
            double logCO = Math.Log(close / open);
            return 0.5 * logHL * logHL - (2.0 * Math.Log(2.0) - 1.0) * logCO * logCO;
        }

        /// <summary>
        /// Rogers-Satchell volatility estimator (per-bar variance).
        /// σ² = ln(H/C)·ln(H/O) + ln(L/C)·ln(L/O)
        /// Handles trending markets better than Garman-Klass.
        /// </summary>
        public static double RogersSatchellVariance(double open, double high, double low, double close)
        {
            if (!MathBase.IsFinite(open) || !MathBase.IsFinite(high) || 
                !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                return MathBase.NaN;
            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                return MathBase.NaN;

            double logHC = Math.Log(high / close);
            double logHO = Math.Log(high / open);
            double logLC = Math.Log(low / close);
            double logLO = Math.Log(low / open);

            return logHC * logHO + logLC * logLO;
        }

        /// <summary>
        /// Yang-Zhang Kinetic component (per-bar).
        /// Part of Yang-Zhang volatility calculation.
        /// K = ln(H/C)·ln(H/O) + ln(L/C)·ln(L/O)
        /// (This is actually identical to Rogers-Satchell)
        /// </summary>
        public static double YangZhangKinetic(double open, double high, double low, double close)
        {
            return RogersSatchellVariance(open, high, low, close);
        }

        #endregion

        #region Windowed High/Low & Donchian

        /// <summary>
        /// Highest value in window [startIndex, startIndex + period).
        /// Returns NaN if any value in window is NaN.
        /// </summary>
        public static double Highest(double[] src, int startIndex, int period)
        {
            if (src == null || period < 1)
                return MathBase.NaN;
            if (startIndex < 0 || startIndex + period > src.Length)
                return MathBase.NaN;

            double max = double.NegativeInfinity;
            for (int i = 0; i < period; i++)
            {
                double val = src[startIndex + i];
                if (!MathBase.IsFinite(val))
                    return MathBase.NaN;
                if (val > max)
                    max = val;
            }

            return max;
        }

        /// <summary>
        /// Highest value over most recent period.
        /// </summary>
        public static double Highest(double[] src, int period)
        {
            if (src == null || src.Length < period)
                return MathBase.NaN;
            return Highest(src, src.Length - period, period);
        }

        /// <summary>
        /// Lowest value in window [startIndex, startIndex + period).
        /// Returns NaN if any value in window is NaN.
        /// </summary>
        public static double Lowest(double[] src, int startIndex, int period)
        {
            if (src == null || period < 1)
                return MathBase.NaN;
            if (startIndex < 0 || startIndex + period > src.Length)
                return MathBase.NaN;

            double min = double.PositiveInfinity;
            for (int i = 0; i < period; i++)
            {
                double val = src[startIndex + i];
                if (!MathBase.IsFinite(val))
                    return MathBase.NaN;
                if (val < min)
                    min = val;
            }

            return min;
        }

        /// <summary>
        /// Lowest value over most recent period.
        /// </summary>
        public static double Lowest(double[] src, int period)
        {
            if (src == null || src.Length < period)
                return MathBase.NaN;
            return Lowest(src, src.Length - period, period);
        }

        /// <summary>
        /// Donchian Channel: highest high and lowest low over period.
        /// Calculates upper, lower, and mid arrays where mid = (upper + lower) / 2.
        /// Warm-up: period bars.
        /// </summary>
        public static void Donchian(double[] high, double[] low, int period, 
            out double[] upper, out double[] lower, out double[] mid)
        {
            if (high == null || low == null || period < 1)
            {
                upper = null;
                lower = null;
                mid = null;
                return;
            }
            if (high.Length != low.Length)
                throw new ArgumentException("High and Low arrays must have same length");

            int n = high.Length;
            upper = new double[n];
            lower = new double[n];
            mid = new double[n];

            // Fill NaN before warm-up
            for (int i = 0; i < Math.Min(period - 1, n); i++)
            {
                upper[i] = MathBase.NaN;
                lower[i] = MathBase.NaN;
                mid[i] = MathBase.NaN;
            }

            // Calculate Donchian for each window
            for (int i = period - 1; i < n; i++)
            {
                upper[i] = Highest(high, i - period + 1, period);
                lower[i] = Lowest(low, i - period + 1, period);

                if (MathBase.IsFinite(upper[i]) && MathBase.IsFinite(lower[i]))
                    mid[i] = (upper[i] + lower[i]) / 2.0;
                else
                    mid[i] = MathBase.NaN;
            }
        }

        #endregion

        #region Swings & Fractals

        /// <summary>
        /// Checks if bar at index is a swing high.
        /// Swing high: H[i] is highest among [i-kLeft, i+kRight].
        /// Returns true AFTER kRight bars have formed (causal check).
        /// </summary>
        /// <param name="high">High prices</param>
        /// <param name="index">Bar index to check</param>
        /// <param name="kLeft">Bars to left</param>
        /// <param name="kRight">Bars to right</param>
        /// <returns>True if swing high detected</returns>
        public static bool IsSwingHigh(double[] high, int index, int kLeft, int kRight)
        {
            if (high == null || index < kLeft || index + kRight >= high.Length)
                return false;

            double pivotHigh = high[index];
            if (!MathBase.IsFinite(pivotHigh))
                return false;

            // Check left side
            for (int i = index - kLeft; i < index; i++)
            {
                if (!MathBase.IsFinite(high[i]))
                    return false;
                if (high[i] >= pivotHigh)
                    return false;
            }

            // Check right side
            for (int i = index + 1; i <= index + kRight; i++)
            {
                if (!MathBase.IsFinite(high[i]))
                    return false;
                if (high[i] > pivotHigh)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if bar at index is a swing low.
        /// Swing low: L[i] is lowest among [i-kLeft, i+kRight].
        /// Returns true AFTER kRight bars have formed (causal check).
        /// </summary>
        public static bool IsSwingLow(double[] low, int index, int kLeft, int kRight)
        {
            if (low == null || index < kLeft || index + kRight >= low.Length)
                return false;

            double pivotLow = low[index];
            if (!MathBase.IsFinite(pivotLow))
                return false;

            // Check left side
            for (int i = index - kLeft; i < index; i++)
            {
                if (!MathBase.IsFinite(low[i]))
                    return false;
                if (low[i] <= pivotLow)
                    return false;
            }

            // Check right side
            for (int i = index + 1; i <= index + kRight; i++)
            {
                if (!MathBase.IsFinite(low[i]))
                    return false;
                if (low[i] < pivotLow)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Finds all swing high indices in array.
        /// Returned indices are where swing was confirmed (i + kRight).
        /// </summary>
        public static List<int> FindSwingHighs(double[] high, int kLeft, int kRight)
        {
            var swings = new List<int>();
            if (high == null || high.Length < kLeft + 1 + kRight)
                return swings;

            for (int i = kLeft; i < high.Length - kRight; i++)
            {
                if (IsSwingHigh(high, i, kLeft, kRight))
                    swings.Add(i);
            }

            return swings;
        }

        /// <summary>
        /// Finds all swing low indices in array.
        /// </summary>
        public static List<int> FindSwingLows(double[] low, int kLeft, int kRight)
        {
            var swings = new List<int>();
            if (low == null || low.Length < kLeft + 1 + kRight)
                return swings;

            for (int i = kLeft; i < low.Length - kRight; i++)
            {
                if (IsSwingLow(low, i, kLeft, kRight))
                    swings.Add(i);
            }

            return swings;
        }

        #endregion

        #region Heikin-Ashi Transform

        /// <summary>
        /// Heikin-Ashi candle transformation.
        /// Smooths price action by averaging OHLC components.
        /// 
        /// Formulas:
        /// HA_Close = (O + H + L + C) / 4
        /// HA_Open = (HA_Open[i-1] + HA_Close[i-1]) / 2
        /// HA_High = max(H, HA_Open, HA_Close)
        /// HA_Low = min(L, HA_Open, HA_Close)
        /// 
        /// Seed (bar 0):
        /// HA_Open[0] = (O[0] + C[0]) / 2
        /// HA_Close[0] = (O[0] + H[0] + L[0] + C[0]) / 4
        /// </summary>
        public static void HeikinAshi(double[] open, double[] high, double[] low, double[] close,
            out double[] haOpen, out double[] haHigh, out double[] haLow, out double[] haClose)
        {
            if (open == null || high == null || low == null || close == null)
            {
                haOpen = null;
                haHigh = null;
                haLow = null;
                haClose = null;
                return;
            }
            if (open.Length != high.Length || open.Length != low.Length || open.Length != close.Length)
                throw new ArgumentException("All OHLC arrays must have same length");

            int n = open.Length;
            haOpen = new double[n];
            haHigh = new double[n];
            haLow = new double[n];
            haClose = new double[n];

            if (n == 0)
                return;

            // First bar - seed values
            if (!MathBase.IsFinite(open[0]) || !MathBase.IsFinite(high[0]) || 
                !MathBase.IsFinite(low[0]) || !MathBase.IsFinite(close[0]))
            {
                haOpen[0] = MathBase.NaN;
                haHigh[0] = MathBase.NaN;
                haLow[0] = MathBase.NaN;
                haClose[0] = MathBase.NaN;
            }
            else
            {
                haClose[0] = (open[0] + high[0] + low[0] + close[0]) / 4.0;
                haOpen[0] = (open[0] + close[0]) / 2.0;
                haHigh[0] = Math.Max(high[0], Math.Max(haOpen[0], haClose[0]));
                haLow[0] = Math.Min(low[0], Math.Min(haOpen[0], haClose[0]));
            }

            // Remaining bars - recursive calculation
            for (int i = 1; i < n; i++)
            {
                if (!MathBase.IsFinite(open[i]) || !MathBase.IsFinite(high[i]) || 
                    !MathBase.IsFinite(low[i]) || !MathBase.IsFinite(close[i]) ||
                    !MathBase.IsFinite(haOpen[i - 1]) || !MathBase.IsFinite(haClose[i - 1]))
                {
                    haOpen[i] = MathBase.NaN;
                    haHigh[i] = MathBase.NaN;
                    haLow[i] = MathBase.NaN;
                    haClose[i] = MathBase.NaN;
                    continue;
                }

                haClose[i] = (open[i] + high[i] + low[i] + close[i]) / 4.0;
                haOpen[i] = (haOpen[i - 1] + haClose[i - 1]) / 2.0;
                haHigh[i] = Math.Max(high[i], Math.Max(haOpen[i], haClose[i]));
                haLow[i] = Math.Min(low[i], Math.Min(haOpen[i], haClose[i]));
            }
        }

        /// <summary>
        /// Stateful Heikin-Ashi calculator for streaming updates.
        /// Maintains previous HA Open and Close for recursive calculation.
        /// </summary>
        public class HeikinAshiState
        {
            private double _prevHaOpen = MathBase.NaN;
            private double _prevHaClose = MathBase.NaN;
            private bool _isFirst = true;

            public double HAOpen { get; private set; } = MathBase.NaN;
            public double HAHigh { get; private set; } = MathBase.NaN;
            public double HALow { get; private set; } = MathBase.NaN;
            public double HAClose { get; private set; } = MathBase.NaN;

            /// <summary>
            /// Updates Heikin-Ashi values with new OHLC bar.
            /// Access results via HAOpen, HAHigh, HALow, HAClose properties.
            /// </summary>
            public void Update(double open, double high, double low, double close)
            {
                if (!MathBase.IsFinite(open) || !MathBase.IsFinite(high) || 
                    !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                {
                    HAOpen = MathBase.NaN;
                    HAHigh = MathBase.NaN;
                    HALow = MathBase.NaN;
                    HAClose = MathBase.NaN;
                    return;
                }

                if (_isFirst)
                {
                    // First bar - seed
                    HAClose = (open + high + low + close) / 4.0;
                    HAOpen = (open + close) / 2.0;
                    HAHigh = Math.Max(high, Math.Max(HAOpen, HAClose));
                    HALow = Math.Min(low, Math.Min(HAOpen, HAClose));
                    _isFirst = false;
                }
                else
                {
                    // Recursive calculation
                    HAClose = (open + high + low + close) / 4.0;
                    HAOpen = (_prevHaOpen + _prevHaClose) / 2.0;
                    HAHigh = Math.Max(high, Math.Max(HAOpen, HAClose));
                    HALow = Math.Min(low, Math.Min(HAOpen, HAClose));
                }

                _prevHaOpen = HAOpen;
                _prevHaClose = HAClose;
            }

            public void Reset()
            {
                _prevHaOpen = MathBase.NaN;
                _prevHaClose = MathBase.NaN;
                _isFirst = true;
                HAOpen = MathBase.NaN;
                HAHigh = MathBase.NaN;
                HALow = MathBase.NaN;
                HAClose = MathBase.NaN;
            }
        }

        #endregion
    }
}
