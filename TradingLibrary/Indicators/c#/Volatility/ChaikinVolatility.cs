using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Chaikin Volatility - measures rate of change in trading range.
    /// Based on the spread between high and low prices.
    /// 
    /// Formula:
    /// 1. Range = High - Low
    /// 2. EMA_Range = EMA(Range, emaPeriod)
    /// 3. CV = 100 * (EMA_Range[t] - EMA_Range[t - rocPeriod]) / EMA_Range[t - rocPeriod]
    /// 
    /// Or expressed as decimal:
    /// CV = (EMA_Range[t] - EMA_Range[t - rocPeriod]) / EMA_Range[t - rocPeriod]
    /// 
    /// Reference: Chaikin, Marc (1990s)
    /// Specification: Volatility_Spec_v1_0.md Section 7
    /// 
    /// Key properties:
    /// - Warm-up period: emaPeriod + rocPeriod
    /// - Positive values indicate increasing volatility
    /// - Negative values indicate decreasing volatility
    /// - Thread-safe for batch operations
    /// </summary>
    public static class ChaikinVolatility
    {
        #region Batch Calculation

        /// <summary>
        /// Calculates Chaikin Volatility for entire price series.
        /// </summary>
        /// <param name="high">High prices (chronological: 0 → oldest)</param>
        /// <param name="low">Low prices</param>
        /// <param name="emaPeriod">Period for EMA smoothing of range (default: 10)</param>
        /// <param name="rocPeriod">Rate of change lookback period (default: 10)</param>
        /// <param name="avgMode">Averaging method for range smoothing (default: EMA)</param>
        /// <param name="scaleTo100">If true, multiply result by 100 for percentage (default: true)</param>
        /// <returns>Chaikin Volatility array, NaN during warm-up</returns>
        /// <exception cref="ArgumentNullException">If any input array is null</exception>
        /// <exception cref="ArgumentException">If arrays have different lengths or periods &lt; 1</exception>
        public static double[] Calculate(
            double[] high, double[] low,
            int emaPeriod = 10,
            int rocPeriod = 10,
            MovingAveragesExtensions.AvgMode avgMode = MovingAveragesExtensions.AvgMode.EMA,
            bool scaleTo100 = true)
        {
            // Validate inputs
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));

            int len = high.Length;
            if (low.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (emaPeriod < 1)
                throw new ArgumentException($"emaPeriod must be >= 1, got {emaPeriod}", nameof(emaPeriod));

            if (rocPeriod < 1)
                throw new ArgumentException($"rocPeriod must be >= 1, got {rocPeriod}", nameof(rocPeriod));

            double[] result = new double[len];
            double scale = scaleTo100 ? 100.0 : 1.0;

            // Calculate range (High - Low)
            double[] range = new double[len];
            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(high[i]) || !MathBase.IsFinite(low[i]))
                {
                    range[i] = double.NaN;
                }
                else
                {
                    range[i] = high[i] - low[i];
                }
            }

            // Apply smoothing to range
            double[] smoothedRange = MovingAveragesExtensions.CalculateAverage(range, emaPeriod, avgMode);

            // Calculate rate of change: (current - past) / past
            for (int i = 0; i < len; i++)
            {
                if (i < rocPeriod)
                {
                    result[i] = double.NaN;
                    continue;
                }

                int pastIdx = i - rocPeriod;

                if (!MathBase.IsFinite(smoothedRange[i]) || !MathBase.IsFinite(smoothedRange[pastIdx]))
                {
                    result[i] = double.NaN;
                    continue;
                }

                // ROC = (current - past) / past
                double change = smoothedRange[i] - smoothedRange[pastIdx];
                result[i] = MathBase.SafeDivide(change * scale, smoothedRange[pastIdx]);
            }

            return result;
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful Chaikin Volatility calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class ChaikinCalculator
        {
            private readonly int _emaPeriod;
            private readonly int _rocPeriod;
            private readonly bool _scaleTo100;
            private readonly MovingAveragesExtensions.AvgMode _avgMode;
            private readonly object _avgState;
            private readonly CircularBuffer<double> _smoothedRangeBuffer;
            private int _count;

            /// <summary>
            /// Current Chaikin Volatility value (NaN until warmed up)
            /// </summary>
            public double Value { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _count >= _emaPeriod + _rocPeriod;

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => Math.Max(0, _emaPeriod + _rocPeriod - _count);

            /// <summary>
            /// Creates new Chaikin Volatility calculator
            /// </summary>
            /// <param name="emaPeriod">Period for range smoothing (must be >= 1)</param>
            /// <param name="rocPeriod">ROC lookback period (must be >= 1)</param>
            /// <param name="avgMode">Averaging method for range smoothing</param>
            /// <param name="scaleTo100">If true, results expressed as percentage</param>
            public ChaikinCalculator(int emaPeriod = 10,
                                    int rocPeriod = 10,
                                    MovingAveragesExtensions.AvgMode avgMode = MovingAveragesExtensions.AvgMode.EMA,
                                    bool scaleTo100 = true)
            {
                if (emaPeriod < 1)
                    throw new ArgumentException($"emaPeriod must be >= 1, got {emaPeriod}", nameof(emaPeriod));

                if (rocPeriod < 1)
                    throw new ArgumentException($"rocPeriod must be >= 1, got {rocPeriod}", nameof(rocPeriod));

                _emaPeriod = emaPeriod;
                _rocPeriod = rocPeriod;
                _avgMode = avgMode;
                _scaleTo100 = scaleTo100;
                _count = 0;

                Value = double.NaN;
                _smoothedRangeBuffer = new CircularBuffer<double>(rocPeriod + 1);

                // Initialize averaging state
                _avgState = MovingAveragesExtensions.CreateMaState(emaPeriod, avgMode);
            }

            /// <summary>
            /// Updates Chaikin Volatility with new bar data
            /// </summary>
            /// <param name="high">Bar high price</param>
            /// <param name="low">Bar low price</param>
            /// <returns>Updated Chaikin Volatility value (NaN during warm-up)</returns>
            public double Update(double high, double low)
            {
                // Validate inputs
                if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                {
                    Value = double.NaN;
                    return Value;
                }

                _count++;

                // Calculate range
                double range = high - low;

                // Update smoothed range
                double smoothedRange = MovingAveragesExtensions.UpdateMaState(_avgState, range, _avgMode);

                // Store smoothed range
                if (MathBase.IsFinite(smoothedRange))
                {
                    _smoothedRangeBuffer.Add(smoothedRange);
                }

                // Check if ready to calculate ROC
                if (_smoothedRangeBuffer.Count < _rocPeriod + 1)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Get current and past smoothed range
                double[] rangeHistory = _smoothedRangeBuffer.ToArray();
                double currentRange = rangeHistory[rangeHistory.Length - 1];
                double pastRange = rangeHistory[rangeHistory.Length - 1 - _rocPeriod];

                // Calculate ROC
                double scale = _scaleTo100 ? 100.0 : 1.0;
                double change = currentRange - pastRange;
                Value = MathBase.SafeDivide(change * scale, pastRange);

                return Value;
            }

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _count = 0;
                _smoothedRangeBuffer.Clear();
                Value = double.NaN;
                MovingAveragesExtensions.ResetMaState(_avgState, _avgMode);
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
