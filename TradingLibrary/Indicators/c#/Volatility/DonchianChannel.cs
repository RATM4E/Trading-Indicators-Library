using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Donchian Channel - volatility indicator based on highest high and lowest low.
    /// 
    /// Formula:
    /// - Upper = Highest(High, period)
    /// - Lower = Lowest(Low, period)
    /// - Mid = (Upper + Lower) / 2
    /// - Width = Upper - Lower
    /// - PercentWidth = Width / Mid
    /// 
    /// Reference: Donchian, Richard (1960s), popularized by Turtle Traders
    /// Specification: Volatility_Spec_v1_0.md Section 5
    /// 
    /// Key properties:
    /// - Warm-up period: period bars
    /// - Pure price-based, no smoothing
    /// - Useful for breakout strategies and volatility assessment
    /// - Thread-safe for batch operations
    /// </summary>
    public static class DonchianChannel
    {
        #region Result Structure

        /// <summary>
        /// Donchian Channel calculation result
        /// </summary>
        public struct DonchianResult
        {
            /// <summary>Upper channel (highest high)</summary>
            public double Upper;

            /// <summary>Middle line (average of upper and lower)</summary>
            public double Mid;

            /// <summary>Lower channel (lowest low)</summary>
            public double Lower;

            /// <summary>
            /// Constructor for result
            /// </summary>
            public DonchianResult(double upper, double mid, double lower)
            {
                Upper = upper;
                Mid = mid;
                Lower = lower;
            }

            /// <summary>
            /// Creates invalid result (all NaN)
            /// </summary>
            public static DonchianResult Invalid => new DonchianResult(double.NaN, double.NaN, double.NaN);
        }

        #endregion

        #region Batch Calculation - Donchian Channel

        /// <summary>
        /// Calculates Donchian Channel for entire price series.
        /// Returns three arrays: upper, mid, lower channels.
        /// </summary>
        /// <param name="high">High prices (chronological: 0 → oldest)</param>
        /// <param name="low">Low prices</param>
        /// <param name="period">Lookback period (default: 20)</param>
        /// <returns>Tuple of (upper[], mid[], lower[]) arrays</returns>
        /// <exception cref="ArgumentNullException">If any input array is null</exception>
        /// <exception cref="ArgumentException">If arrays have different lengths or period &lt; 1</exception>
        public static (double[] upper, double[] mid, double[] lower) Calculate(
            double[] high, double[] low,
            int period = 20)
        {
            // Validate inputs
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));

            int len = high.Length;
            if (low.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (period < 1)
                throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

            // Calculate highest and lowest
            double[] upper = PriceAction.Highest(high, period);
            double[] lower = PriceAction.Lowest(low, period);

            // Calculate mid line
            double[] mid = new double[len];
            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(upper[i]) || !MathBase.IsFinite(lower[i]))
                {
                    mid[i] = double.NaN;
                }
                else
                {
                    mid[i] = (upper[i] + lower[i]) * 0.5;
                }
            }

            return (upper, mid, lower);
        }

        #endregion

        #region Batch Calculation - Donchian Width

        /// <summary>
        /// Calculates Donchian Channel Width (absolute).
        /// 
        /// Formula: Width = Upper - Lower
        /// 
        /// Measures absolute volatility in price units.
        /// </summary>
        /// <param name="high">High prices</param>
        /// <param name="low">Low prices</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Width values array, NaN during warm-up</returns>
        public static double[] Width(double[] high, double[] low, int period = 20)
        {
            var (upper, mid, lower) = Calculate(high, low, period);

            int len = high.Length;
            double[] width = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(upper[i]) || !MathBase.IsFinite(lower[i]))
                {
                    width[i] = double.NaN;
                }
                else
                {
                    width[i] = upper[i] - lower[i];
                }
            }

            return width;
        }

        #endregion

        #region Batch Calculation - Donchian PercentWidth

        /// <summary>
        /// Calculates Donchian Channel Percent Width (normalized).
        /// 
        /// Formula: PercentWidth = (Upper - Lower) / Mid
        /// 
        /// Normalizes width to middle price for cross-instrument comparison.
        /// </summary>
        /// <param name="high">High prices</param>
        /// <param name="low">Low prices</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Percent width values array (decimal, not %), NaN during warm-up or when mid ≈ 0</returns>
        public static double[] PercentWidth(double[] high, double[] low, int period = 20)
        {
            var (upper, mid, lower) = Calculate(high, low, period);

            int len = high.Length;
            double[] percentWidth = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(upper[i]) || !MathBase.IsFinite(lower[i]) || !MathBase.IsFinite(mid[i]))
                {
                    percentWidth[i] = double.NaN;
                    continue;
                }

                double width = upper[i] - lower[i];
                percentWidth[i] = MathBase.SafeDivide(width, mid[i]);
            }

            return percentWidth;
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful Donchian Channel calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class DonchianCalculator
        {
            private readonly int _period;
            private readonly CircularBuffer<double> _highBuffer;
            private readonly CircularBuffer<double> _lowBuffer;

            /// <summary>
            /// Current channel result (all NaN until warmed up)
            /// </summary>
            public DonchianResult Value { get; private set; }

            /// <summary>
            /// Current width value (NaN until warmed up)
            /// </summary>
            public double Width { get; private set; }

            /// <summary>
            /// Current percent width value (NaN until warmed up)
            /// </summary>
            public double PercentWidth { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _highBuffer.Count >= _period;

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => Math.Max(0, _period - _highBuffer.Count);

            /// <summary>
            /// Creates new Donchian Channel calculator
            /// </summary>
            /// <param name="period">Lookback period (must be >= 1)</param>
            public DonchianCalculator(int period = 20)
            {
                if (period < 1)
                    throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

                _period = period;
                _highBuffer = new CircularBuffer<double>(period);
                _lowBuffer = new CircularBuffer<double>(period);

                Value = DonchianResult.Invalid;
                Width = double.NaN;
                PercentWidth = double.NaN;
            }

            /// <summary>
            /// Updates Donchian Channel with new bar data
            /// </summary>
            /// <param name="high">Bar high price</param>
            /// <param name="low">Bar low price</param>
            /// <returns>Updated channel result (all NaN during warm-up)</returns>
            public DonchianResult Update(double high, double low)
            {
                // Validate inputs
                if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                {
                    Value = DonchianResult.Invalid;
                    Width = double.NaN;
                    PercentWidth = double.NaN;
                    return Value;
                }

                // Add to buffers
                _highBuffer.Add(high);
                _lowBuffer.Add(low);

                if (!IsReady)
                {
                    Value = DonchianResult.Invalid;
                    Width = double.NaN;
                    PercentWidth = double.NaN;
                    return Value;
                }

                // Find highest and lowest
                double[] highs = _highBuffer.ToArray();
                double[] lows = _lowBuffer.ToArray();

                double upper = highs[0];
                double lower = lows[0];

                for (int i = 1; i < _period; i++)
                {
                    if (highs[i] > upper) upper = highs[i];
                    if (lows[i] < lower) lower = lows[i];
                }

                double mid = (upper + lower) * 0.5;
                Value = new DonchianResult(upper, mid, lower);

                // Calculate width metrics
                Width = upper - lower;
                PercentWidth = MathBase.SafeDivide(Width, mid);

                return Value;
            }

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _highBuffer.Clear();
                _lowBuffer.Clear();
                Value = DonchianResult.Invalid;
                Width = double.NaN;
                PercentWidth = double.NaN;
            }
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Circular buffer for efficient rolling window operations
        /// </summary>
        private class CircularBuffer<T>
        {
            private readonly T[] _buffer;
            private int _head;
            private int _count;

            public int Count => _count;
            public int Capacity => _buffer.Length;

            public CircularBuffer(int capacity)
            {
                _buffer = new T[capacity];
                _head = 0;
                _count = 0;
            }

            public void Add(T item)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length)
                    _count++;
            }

            public T[] ToArray()
            {
                T[] result = new T[_count];
                for (int i = 0; i < _count; i++)
                {
                    int idx = (_head - _count + i + _buffer.Length) % _buffer.Length;
                    result[i] = _buffer[idx];
                }
                return result;
            }

            public void Clear()
            {
                _head = 0;
                _count = 0;
            }
        }

        #endregion
    }
}
