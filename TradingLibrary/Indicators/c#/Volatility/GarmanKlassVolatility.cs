using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Garman-Klass Volatility - OHLC-based volatility estimator.
    /// More efficient than Parkinson by incorporating open and close prices.
    /// 
    /// Formula:
    /// σ² = 0.5 * ln(H/L)² - (2*ln(2) - 1) * ln(C/O)²
    /// σ = sqrt(mean(σ²))
    /// 
    /// If annualized: σ_annual = σ * sqrt(tradingDaysPerYear / period)
    /// 
    /// Reference: Garman, M. & Klass, M. (1980) "On the Estimation of Security Price Volatilities from Historical Data"
    /// Specification: Volatility_Spec_v1_0.md Section 6.2
    /// 
    /// Key properties:
    /// - Warm-up period: period bars
    /// - ~7.4x more efficient than close-to-close volatility
    /// - Assumes no overnight gaps and zero drift
    /// - Thread-safe for batch operations
    /// </summary>
    public static class GarmanKlassVolatility
    {
        // Constants
        private const double HL_FACTOR = 0.5;  // 0.5
        private const double CO_FACTOR = 0.38629436111989; // 2*ln(2) - 1 ≈ 0.3863

        #region Batch Calculation

        /// <summary>
        /// Calculates Garman-Klass Volatility for entire price series.
        /// </summary>
        /// <param name="open">Open prices (chronological: 0 → oldest)</param>
        /// <param name="high">High prices</param>
        /// <param name="low">Low prices</param>
        /// <param name="close">Close prices</param>
        /// <param name="period">Rolling window period (default: 20)</param>
        /// <param name="annualize">If true, annualize the result (default: true)</param>
        /// <param name="tradingDaysPerYear">Trading days for annualization (default: 252)</param>
        /// <returns>Volatility array (decimal, not %), NaN during warm-up</returns>
        public static double[] Calculate(
            double[] open, double[] high, double[] low, double[] close,
            int period = 20,
            bool annualize = true,
            double tradingDaysPerYear = 252.0)
        {
            // Validate inputs
            if (open == null) throw new ArgumentNullException(nameof(open));
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (close == null) throw new ArgumentNullException(nameof(close));

            int len = open.Length;
            if (high.Length != len || low.Length != len || close.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (period < 1)
                throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

            if (tradingDaysPerYear <= 0)
                throw new ArgumentException($"tradingDaysPerYear must be > 0, got {tradingDaysPerYear}",
                    nameof(tradingDaysPerYear));

            double[] result = new double[len];

            // Calculate GK variance component for each bar
            double[] gkComponents = new double[len];
            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(open[i]) || !MathBase.IsFinite(high[i]) ||
                    !MathBase.IsFinite(low[i]) || !MathBase.IsFinite(close[i]))
                {
                    gkComponents[i] = double.NaN;
                    continue;
                }

                // HL component: 0.5 * ln(H/L)²
                double hlRatio = MathBase.SafeDivide(high[i], low[i]);
                double hlLog = MathBase.SafeLog(hlRatio);

                // CO component: (2*ln(2) - 1) * ln(C/O)²
                double coRatio = MathBase.SafeDivide(close[i], open[i]);
                double coLog = MathBase.SafeLog(coRatio);

                if (!MathBase.IsFinite(hlLog) || !MathBase.IsFinite(coLog))
                {
                    gkComponents[i] = double.NaN;
                    continue;
                }

                // GK formula: 0.5 * ln(H/L)² - (2*ln(2)-1) * ln(C/O)²
                gkComponents[i] = HL_FACTOR * hlLog * hlLog - CO_FACTOR * coLog * coLog;
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
                    if (!MathBase.IsFinite(gkComponents[idx]))
                    {
                        hasNaN = true;
                        break;
                    }
                    sum += gkComponents[idx];
                    count++;
                }

                if (hasNaN || count == 0)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Mean variance
                double meanVariance = sum / count;

                // Ensure non-negative (can be slightly negative due to numerical errors)
                if (meanVariance < 0)
                    meanVariance = 0;

                // Standard deviation
                double volatility = Math.Sqrt(meanVariance);

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
        /// Stateful Garman-Klass Volatility calculator for streaming updates.
        /// </summary>
        public class GarmanKlassCalculator
        {
            private readonly int _period;
            private readonly bool _annualize;
            private readonly double _tradingDaysPerYear;
            private readonly CircularBuffer<double> _gkBuffer;

            public double Value { get; private set; }
            public bool IsReady => _gkBuffer.Count >= _period;
            public int WarmupBarsLeft => Math.Max(0, _period - _gkBuffer.Count);

            public GarmanKlassCalculator(int period = 20,
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
                _gkBuffer = new CircularBuffer<double>(period);
                Value = double.NaN;
            }

            public double Update(double open, double high, double low, double close)
            {
                if (!MathBase.IsFinite(open) || !MathBase.IsFinite(high) ||
                    !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate GK component
                double hlRatio = MathBase.SafeDivide(high, low);
                double hlLog = MathBase.SafeLog(hlRatio);
                double coRatio = MathBase.SafeDivide(close, open);
                double coLog = MathBase.SafeLog(coRatio);

                if (!MathBase.IsFinite(hlLog) || !MathBase.IsFinite(coLog))
                {
                    Value = double.NaN;
                    return Value;
                }

                double gkComponent = HL_FACTOR * hlLog * hlLog - CO_FACTOR * coLog * coLog;
                _gkBuffer.Add(gkComponent);

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate mean
                double[] data = _gkBuffer.ToArray();
                double sum = 0;
                for (int i = 0; i < data.Length; i++)
                    sum += data[i];
                double meanVariance = sum / data.Length;

                if (meanVariance < 0)
                    meanVariance = 0;

                double volatility = Math.Sqrt(meanVariance);

                if (_annualize)
                {
                    double annualizationFactor = Math.Sqrt(_tradingDaysPerYear / _period);
                    volatility *= annualizationFactor;
                }

                Value = volatility;
                return Value;
            }

            public void Reset()
            {
                _gkBuffer.Clear();
                Value = double.NaN;
            }
        }

        #endregion

        #region Helper Classes

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
