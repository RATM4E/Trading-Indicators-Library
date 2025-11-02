using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Parkinson Volatility - range-based volatility estimator using high-low data.
    /// More efficient than close-to-close volatility for estimating true volatility.
    /// 
    /// Formula:
    /// σ² = (1 / (4 * ln(2))) * mean(ln(High/Low)²)
    /// σ = sqrt(σ²)
    /// 
    /// If annualized: σ_annual = σ * sqrt(tradingDaysPerYear / period)
    /// 
    /// Reference: Parkinson, M. (1980) "The Extreme Value Method for Estimating the Variance of the Rate of Return"
    /// Specification: Volatility_Spec_v1_0.md Section 6.1
    /// 
    /// Key properties:
    /// - Warm-up period: period bars
    /// - ~5x more efficient than close-to-close volatility
    /// - Assumes no overnight gaps (continuous trading)
    /// - Thread-safe for batch operations
    /// </summary>
    public static class ParkinsonVolatility
    {
        // Constant: 1 / (4 * ln(2))
        private const double PARKINSON_FACTOR = 0.3606737602222409; // 1.0 / (4.0 * Math.Log(2))

        #region Batch Calculation

        /// <summary>
        /// Calculates Parkinson Volatility for entire price series.
        /// </summary>
        /// <param name="high">High prices (chronological: 0 → oldest)</param>
        /// <param name="low">Low prices</param>
        /// <param name="period">Rolling window period (default: 20)</param>
        /// <param name="annualize">If true, annualize the result (default: true)</param>
        /// <param name="tradingDaysPerYear">Trading days for annualization (default: 252)</param>
        /// <returns>Volatility array (decimal, not %), NaN during warm-up</returns>
        /// <exception cref="ArgumentNullException">If any input array is null</exception>
        /// <exception cref="ArgumentException">If arrays have different lengths or period &lt; 1</exception>
        public static double[] Calculate(
            double[] high, double[] low,
            int period = 20,
            bool annualize = true,
            double tradingDaysPerYear = 252.0)
        {
            // Validate inputs
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));

            int len = high.Length;
            if (low.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (period < 1)
                throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

            if (tradingDaysPerYear <= 0)
                throw new ArgumentException($"tradingDaysPerYear must be > 0, got {tradingDaysPerYear}",
                    nameof(tradingDaysPerYear));

            double[] result = new double[len];

            // Calculate ln(H/L)² for each bar
            double[] hlSquared = new double[len];
            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(high[i]) || !MathBase.IsFinite(low[i]))
                {
                    hlSquared[i] = double.NaN;
                    continue;
                }

                double ratio = MathBase.SafeDivide(high[i], low[i]);
                double logRatio = MathBase.SafeLog(ratio);

                if (!MathBase.IsFinite(logRatio))
                {
                    hlSquared[i] = double.NaN;
                }
                else
                {
                    hlSquared[i] = logRatio * logRatio;
                }
            }

            // Calculate rolling mean and volatility
            for (int i = 0; i < len; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Extract window [i - period + 1, i]
                double sum = 0;
                bool hasNaN = false;
                int count = 0;

                for (int j = 0; j < period; j++)
                {
                    int idx = i - period + 1 + j;
                    if (!MathBase.IsFinite(hlSquared[idx]))
                    {
                        hasNaN = true;
                        break;
                    }
                    sum += hlSquared[idx];
                    count++;
                }

                if (hasNaN || count == 0)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate variance: PARKINSON_FACTOR * mean(ln(H/L)²)
                double mean = sum / count;
                double variance = PARKINSON_FACTOR * mean;

                // Standard deviation
                double volatility = Math.Sqrt(variance);

                // Annualize if requested
                if (annualize)
                {
                    double annualizationFactor = Math.Sqrt(tradingDaysPerYear / period);
                    volatility *= annualizationFactor;
                }

                result[i] = volatility;
            }

            return result;
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful Parkinson Volatility calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class ParkinsonCalculator
        {
            private readonly int _period;
            private readonly bool _annualize;
            private readonly double _tradingDaysPerYear;
            private readonly CircularBuffer<double> _hlSquaredBuffer;

            /// <summary>
            /// Current volatility value (NaN until warmed up)
            /// </summary>
            public double Value { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _hlSquaredBuffer.Count >= _period;

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => Math.Max(0, _period - _hlSquaredBuffer.Count);

            /// <summary>
            /// Creates new Parkinson Volatility calculator
            /// </summary>
            /// <param name="period">Rolling window period (must be >= 1)</param>
            /// <param name="annualize">If true, annualize the result</param>
            /// <param name="tradingDaysPerYear">Trading days for annualization</param>
            public ParkinsonCalculator(int period = 20,
                                      bool annualize = true,
                                      double tradingDaysPerYear = 252.0)
            {
                if (period < 1)
                    throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

                if (tradingDaysPerYear <= 0)
                    throw new ArgumentException($"tradingDaysPerYear must be > 0, got {tradingDaysPerYear}",
                        nameof(tradingDaysPerYear));

                _period = period;
                _annualize = annualize;
                _tradingDaysPerYear = tradingDaysPerYear;
                _hlSquaredBuffer = new CircularBuffer<double>(period);
                Value = double.NaN;
            }

            /// <summary>
            /// Updates Parkinson Volatility with new bar data
            /// </summary>
            /// <param name="high">Bar high price</param>
            /// <param name="low">Bar low price</param>
            /// <returns>Updated volatility value (NaN during warm-up)</returns>
            public double Update(double high, double low)
            {
                // Validate inputs
                if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate ln(H/L)²
                double ratio = MathBase.SafeDivide(high, low);
                double logRatio = MathBase.SafeLog(ratio);

                if (!MathBase.IsFinite(logRatio))
                {
                    Value = double.NaN;
                    return Value;
                }

                double hlSquared = logRatio * logRatio;
                _hlSquaredBuffer.Add(hlSquared);

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate mean of ln(H/L)²
                double[] data = _hlSquaredBuffer.ToArray();
                double sum = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    sum += data[i];
                }
                double mean = sum / data.Length;

                // Calculate variance: PARKINSON_FACTOR * mean
                double variance = PARKINSON_FACTOR * mean;

                // Standard deviation
                double volatility = Math.Sqrt(variance);

                // Annualize if requested
                if (_annualize)
                {
                    double annualizationFactor = Math.Sqrt(_tradingDaysPerYear / _period);
                    volatility *= annualizationFactor;
                }

                Value = volatility;
                return Value;
            }

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _hlSquaredBuffer.Clear();
                Value = double.NaN;
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
