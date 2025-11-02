using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Statistical volatility indicators: StdDev, ZScore, and Historical Volatility.
    /// 
    /// - StdDev: Rolling standard deviation (sample or population)
    /// - ZScore: Standardized distance from mean in standard deviation units
    /// - HV: Annualized volatility based on log returns
    /// 
    /// Specification: Volatility_Spec_v1_0.md Section 2
    /// 
    /// Key properties:
    /// - All use rolling windows
    /// - Warm-up period: period bars
    /// - Thread-safe for batch operations
    /// </summary>
    public static class StdDevIndicator
    {
        #region StdDev - Standard Deviation

        /// <summary>
        /// Calculates rolling standard deviation for entire series.
        /// </summary>
        /// <param name="src">Source values (chronological: 0 → oldest)</param>
        /// <param name="period">Rolling window period (default: 20)</param>
        /// <param name="sample">If true, use sample std (n-1), else population std (n) (default: true)</param>
        /// <returns>Standard deviation array, NaN during warm-up</returns>
        /// <exception cref="ArgumentNullException">If src is null</exception>
        /// <exception cref="ArgumentException">If period &lt; 1 (or &lt; 2 for sample mode)</exception>
        public static double[] StdDev(double[] src, int period = 20, bool sample = true)
        {
            // Validate inputs
            if (src == null) throw new ArgumentNullException(nameof(src));

            if (sample && period < 2)
                throw new ArgumentException($"Period must be >= 2 for sample std, got {period}", nameof(period));
            if (!sample && period < 1)
                throw new ArgumentException($"Period must be >= 1 for population std, got {period}", nameof(period));

            int len = src.Length;
            double[] result = new double[len];

            // Use MathBase.StdDev which handles the rolling calculation
            for (int i = 0; i < len; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Extract window [i - period + 1, i]
                double[] window = new double[period];
                bool hasNaN = false;

                for (int j = 0; j < period; j++)
                {
                    int idx = i - period + 1 + j;
                    window[j] = src[idx];
                    if (!MathBase.IsFinite(window[j]))
                        hasNaN = true;
                }

                if (hasNaN)
                {
                    result[i] = double.NaN;
                }
                else
                {
                    result[i] = MathBase.StdDev(window, period, sample);
                }
            }

            return result;
        }

        #endregion

        #region ZScore - Standardized Score

        /// <summary>
        /// Calculates Z-Score: (value - mean) / stddev
        /// Measures how many standard deviations value is from mean.
        /// </summary>
        /// <param name="src">Source values (chronological: 0 → oldest)</param>
        /// <param name="period">Rolling window period (default: 20)</param>
        /// <param name="sample">Use sample std (default: true)</param>
        /// <returns>Z-Score array, NaN during warm-up or when std ≈ 0</returns>
        /// <exception cref="ArgumentNullException">If src is null</exception>
        /// <exception cref="ArgumentException">If period invalid</exception>
        public static double[] ZScore(double[] src, int period = 20, bool sample = true)
        {
            // Validate inputs
            if (src == null) throw new ArgumentNullException(nameof(src));

            if (sample && period < 2)
                throw new ArgumentException($"Period must be >= 2 for sample std, got {period}", nameof(period));
            if (!sample && period < 1)
                throw new ArgumentException($"Period must be >= 1 for population std, got {period}", nameof(period));

            int len = src.Length;
            double[] result = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Extract window [i - period + 1, i]
                double[] window = new double[period];
                bool hasNaN = false;

                for (int j = 0; j < period; j++)
                {
                    int idx = i - period + 1 + j;
                    window[j] = src[idx];
                    if (!MathBase.IsFinite(window[j]))
                        hasNaN = true;
                }

                if (hasNaN)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate mean and stddev
                double mean = MathBase.Mean(window, period);
                double std = MathBase.StdDev(window, period, sample);

                // Z-score = (current - mean) / std
                result[i] = MathBase.SafeDivide(src[i] - mean, std);
            }

            return result;
        }

        #endregion

        #region Historical Volatility

        /// <summary>
        /// Calculates Historical Volatility (HV) - annualized volatility of log returns.
        /// 
        /// Formula: HV = StdDev(ln(Close[i] / Close[i-1])) * sqrt(tradingDaysPerYear / period)
        /// 
        /// Commonly used in options pricing and risk management.
        /// </summary>
        /// <param name="close">Close prices (chronological: 0 → oldest)</param>
        /// <param name="period">Rolling window period (default: 20)</param>
        /// <param name="annualize">If true, annualize the result (default: true)</param>
        /// <param name="tradingDaysPerYear">Trading days for annualization (default: 252)</param>
        /// <returns>Historical volatility array (decimal, not %), NaN during warm-up</returns>
        /// <exception cref="ArgumentNullException">If close is null</exception>
        /// <exception cref="ArgumentException">If period &lt; 2 or tradingDaysPerYear &lt;= 0</exception>
        public static double[] HistoricalVolatility(double[] close, 
                                                    int period = 20,
                                                    bool annualize = true,
                                                    double tradingDaysPerYear = 252.0)
        {
            // Validate inputs
            if (close == null) throw new ArgumentNullException(nameof(close));

            if (period < 2)
                throw new ArgumentException($"Period must be >= 2, got {period}", nameof(period));

            if (tradingDaysPerYear <= 0)
                throw new ArgumentException($"tradingDaysPerYear must be > 0, got {tradingDaysPerYear}", 
                    nameof(tradingDaysPerYear));

            int len = close.Length;
            double[] result = new double[len];

            // Calculate log returns: ln(Close[i] / Close[i-1])
            double[] logReturns = new double[len];
            logReturns[0] = double.NaN; // First bar has no previous close

            for (int i = 1; i < len; i++)
            {
                if (!MathBase.IsFinite(close[i]) || !MathBase.IsFinite(close[i - 1]))
                {
                    logReturns[i] = double.NaN;
                }
                else
                {
                    // Safe log of ratio
                    double ratio = MathBase.SafeDivide(close[i], close[i - 1]);
                    logReturns[i] = MathBase.SafeLog(ratio);
                }
            }

            // Calculate rolling standard deviation of log returns
            for (int i = 0; i < len; i++)
            {
                // Need period log returns, but first log return is at index 1
                // So we need i >= period (not period-1) because logReturns[0] is NaN
                if (i < period)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Extract window of log returns [i - period + 1, i]
                double[] window = new double[period];
                bool hasNaN = false;

                for (int j = 0; j < period; j++)
                {
                    int idx = i - period + 1 + j;
                    window[j] = logReturns[idx];
                    if (!MathBase.IsFinite(window[j]))
                        hasNaN = true;
                }

                if (hasNaN)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Standard deviation of log returns (sample mode)
                double volatility = MathBase.StdDev(window, period, sample: true);

                // Annualize if requested
                if (annualize)
                {
                    // σ_annual = σ_period * sqrt(tradingDaysPerYear / period)
                    double annualizationFactor = Math.Sqrt(tradingDaysPerYear / period);
                    volatility *= annualizationFactor;
                }

                result[i] = volatility;
            }

            return result;
        }

        #endregion

        #region Stateful Calculators

        /// <summary>
        /// Stateful StdDev calculator for streaming updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class StdDevCalculator
        {
            private readonly int _period;
            private readonly bool _sample;
            private readonly CircularBuffer<double> _buffer;

            /// <summary>Current standard deviation value</summary>
            public double Value { get; private set; }

            /// <summary>True if enough data accumulated</summary>
            public bool IsReady => _buffer.Count >= _period;

            /// <summary>Bars remaining until ready</summary>
            public int WarmupBarsLeft => Math.Max(0, _period - _buffer.Count);

            /// <summary>
            /// Creates new StdDev calculator
            /// </summary>
            public StdDevCalculator(int period, bool sample = true)
            {
                if (sample && period < 2)
                    throw new ArgumentException($"Period must be >= 2 for sample std, got {period}", nameof(period));
                if (!sample && period < 1)
                    throw new ArgumentException($"Period must be >= 1 for population std, got {period}", nameof(period));

                _period = period;
                _sample = sample;
                _buffer = new CircularBuffer<double>(period);
                Value = double.NaN;
            }

            /// <summary>Updates with new value</summary>
            public double Update(double value)
            {
                if (!MathBase.IsFinite(value))
                {
                    Value = double.NaN;
                    return Value;
                }

                _buffer.Add(value);

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate std of current buffer
                Value = MathBase.StdDev(_buffer.ToArray(), _period, _sample);
                return Value;
            }

            /// <summary>Resets to initial state</summary>
            public void Reset()
            {
                _buffer.Clear();
                Value = double.NaN;
            }
        }

        /// <summary>
        /// Stateful Z-Score calculator for streaming updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class ZScoreCalculator
        {
            private readonly int _period;
            private readonly bool _sample;
            private readonly CircularBuffer<double> _buffer;

            /// <summary>Current Z-Score value</summary>
            public double Value { get; private set; }

            /// <summary>True if enough data accumulated</summary>
            public bool IsReady => _buffer.Count >= _period;

            /// <summary>Bars remaining until ready</summary>
            public int WarmupBarsLeft => Math.Max(0, _period - _buffer.Count);

            /// <summary>
            /// Creates new ZScore calculator
            /// </summary>
            public ZScoreCalculator(int period, bool sample = true)
            {
                if (sample && period < 2)
                    throw new ArgumentException($"Period must be >= 2 for sample std, got {period}", nameof(period));
                if (!sample && period < 1)
                    throw new ArgumentException($"Period must be >= 1 for population std, got {period}", nameof(period));

                _period = period;
                _sample = sample;
                _buffer = new CircularBuffer<double>(period);
                Value = double.NaN;
            }

            /// <summary>Updates with new value</summary>
            public double Update(double value)
            {
                if (!MathBase.IsFinite(value))
                {
                    Value = double.NaN;
                    return Value;
                }

                _buffer.Add(value);

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate mean and std of current buffer
                double[] data = _buffer.ToArray();
                double mean = MathBase.Mean(data, _period);
                double std = MathBase.StdDev(data, _period, _sample);

                // Z-score = (current - mean) / std
                Value = MathBase.SafeDivide(value - mean, std);
                return Value;
            }

            /// <summary>Resets to initial state</summary>
            public void Reset()
            {
                _buffer.Clear();
                Value = double.NaN;
            }
        }

        /// <summary>
        /// Stateful Historical Volatility calculator for streaming updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class HistoricalVolatilityCalculator
        {
            private readonly int _period;
            private readonly bool _annualize;
            private readonly double _tradingDaysPerYear;
            private readonly CircularBuffer<double> _logReturnsBuffer;
            private double _prevClose;

            /// <summary>Current HV value</summary>
            public double Value { get; private set; }

            /// <summary>True if enough data accumulated</summary>
            public bool IsReady => _logReturnsBuffer.Count >= _period;

            /// <summary>Bars remaining until ready</summary>
            public int WarmupBarsLeft => Math.Max(0, _period - _logReturnsBuffer.Count);

            /// <summary>
            /// Creates new Historical Volatility calculator
            /// </summary>
            public HistoricalVolatilityCalculator(int period = 20, 
                                                  bool annualize = true, 
                                                  double tradingDaysPerYear = 252.0)
            {
                if (period < 2)
                    throw new ArgumentException($"Period must be >= 2, got {period}", nameof(period));
                if (tradingDaysPerYear <= 0)
                    throw new ArgumentException($"tradingDaysPerYear must be > 0, got {tradingDaysPerYear}",
                        nameof(tradingDaysPerYear));

                _period = period;
                _annualize = annualize;
                _tradingDaysPerYear = tradingDaysPerYear;
                _logReturnsBuffer = new CircularBuffer<double>(period);
                _prevClose = double.NaN;
                Value = double.NaN;
            }

            /// <summary>Updates with new close price</summary>
            public double Update(double close)
            {
                if (!MathBase.IsFinite(close))
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate log return
                if (!double.IsNaN(_prevClose))
                {
                    double ratio = MathBase.SafeDivide(close, _prevClose);
                    double logReturn = MathBase.SafeLog(ratio);

                    if (MathBase.IsFinite(logReturn))
                    {
                        _logReturnsBuffer.Add(logReturn);
                    }
                }

                _prevClose = close;

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate std of log returns (sample mode)
                double[] logReturns = _logReturnsBuffer.ToArray();
                double volatility = MathBase.StdDev(logReturns, _period, sample: true);

                // Annualize if requested
                if (_annualize)
                {
                    double annualizationFactor = Math.Sqrt(_tradingDaysPerYear / _period);
                    volatility *= annualizationFactor;
                }

                Value = volatility;
                return Value;
            }

            /// <summary>Resets to initial state</summary>
            public void Reset()
            {
                _logReturnsBuffer.Clear();
                _prevClose = double.NaN;
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
