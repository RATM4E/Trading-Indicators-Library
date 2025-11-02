using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Rogers-Satchell Volatility - OHLC-based estimator that allows for drift.
    /// Unlike Garman-Klass, does not assume zero drift.
    /// 
    /// Formula:
    /// σ² = ln(H/C) * ln(H/O) + ln(L/C) * ln(L/O)
    /// σ = sqrt(mean(σ²))
    /// 
    /// If annualized: σ_annual = σ * sqrt(tradingDaysPerYear / period)
    /// 
    /// Reference: Rogers, L.C.G. & Satchell, S.E. (1991) "Estimating Variance From High, Low and Closing Prices"
    /// Specification: Volatility_Spec_v1_0.md Section 6.3
    /// 
    /// Key properties:
    /// - Warm-up period: period bars
    /// - Handles trending markets (drift)
    /// - Does not assume lognormal distribution
    /// - Thread-safe for batch operations
    /// </summary>
    public static class RogersSatchellVolatility
    {
        #region Batch Calculation

        /// <summary>
        /// Calculates Rogers-Satchell Volatility for entire price series.
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

            // Calculate RS variance component for each bar
            double[] rsComponents = new double[len];
            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(open[i]) || !MathBase.IsFinite(high[i]) ||
                    !MathBase.IsFinite(low[i]) || !MathBase.IsFinite(close[i]))
                {
                    rsComponents[i] = double.NaN;
                    continue;
                }

                // ln(H/C) * ln(H/O)
                double hcRatio = MathBase.SafeDivide(high[i], close[i]);
                double hoRatio = MathBase.SafeDivide(high[i], open[i]);
                double hcLog = MathBase.SafeLog(hcRatio);
                double hoLog = MathBase.SafeLog(hoRatio);

                // ln(L/C) * ln(L/O)
                double lcRatio = MathBase.SafeDivide(low[i], close[i]);
                double loRatio = MathBase.SafeDivide(low[i], open[i]);
                double lcLog = MathBase.SafeLog(lcRatio);
                double loLog = MathBase.SafeLog(loRatio);

                if (!MathBase.IsFinite(hcLog) || !MathBase.IsFinite(hoLog) ||
                    !MathBase.IsFinite(lcLog) || !MathBase.IsFinite(loLog))
                {
                    rsComponents[i] = double.NaN;
                    continue;
                }

                // RS formula: ln(H/C)*ln(H/O) + ln(L/C)*ln(L/O)
                rsComponents[i] = hcLog * hoLog + lcLog * loLog;
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
                    if (!MathBase.IsFinite(rsComponents[idx]))
                    {
                        hasNaN = true;
                        break;
                    }
                    sum += rsComponents[idx];
                    count++;
                }

                if (hasNaN || count == 0)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Mean variance
                double meanVariance = sum / count;

                // Ensure non-negative
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
        /// Stateful Rogers-Satchell Volatility calculator for streaming updates.
        /// </summary>
        public class RogersSatchellCalculator
        {
            private readonly int _period;
            private readonly bool _annualize;
            private readonly double _tradingDaysPerYear;
            private readonly CircularBuffer<double> _rsBuffer;

            public double Value { get; private set; }
            public bool IsReady => _rsBuffer.Count >= _period;
            public int WarmupBarsLeft => Math.Max(0, _period - _rsBuffer.Count);

            public RogersSatchellCalculator(int period = 20,
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
                _rsBuffer = new CircularBuffer<double>(period);
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

                // Calculate RS component
                double hcRatio = MathBase.SafeDivide(high, close);
                double hoRatio = MathBase.SafeDivide(high, open);
                double hcLog = MathBase.SafeLog(hcRatio);
                double hoLog = MathBase.SafeLog(hoRatio);

                double lcRatio = MathBase.SafeDivide(low, close);
                double loRatio = MathBase.SafeDivide(low, open);
                double lcLog = MathBase.SafeLog(lcRatio);
                double loLog = MathBase.SafeLog(loRatio);

                if (!MathBase.IsFinite(hcLog) || !MathBase.IsFinite(hoLog) ||
                    !MathBase.IsFinite(lcLog) || !MathBase.IsFinite(loLog))
                {
                    Value = double.NaN;
                    return Value;
                }

                double rsComponent = hcLog * hoLog + lcLog * loLog;
                _rsBuffer.Add(rsComponent);

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate mean
                double[] data = _rsBuffer.ToArray();
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
                _rsBuffer.Clear();
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
