using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Yang-Zhang Volatility - most comprehensive OHLC estimator.
    /// Combines overnight (close-to-open), Rogers-Satchell intraday, and open-to-close components.
    /// 
    /// Formula:
    /// σ²_YZ = σ²_overnight + k*σ²_open_close + (1-k)*σ²_RS
    /// 
    /// Where:
    /// - σ²_overnight = variance of ln(O[i]/C[i-1]) - overnight gap component
    /// - σ²_open_close = variance of ln(C[i]/O[i]) - open-to-close component
    /// - σ²_RS = Rogers-Satchell variance - intraday component
    /// - k = 0.34 / (1.34 + (N+1)/(N-1)) - optimal weight
    /// 
    /// Reference: Yang, D. & Zhang, Q. (2000) "Drift-Independent Volatility Estimation Based on High, Low, Open, and Close Prices"
    /// Specification: Volatility_Spec_v1_0.md Section 6.4
    /// 
    /// Key properties:
    /// - Warm-up period: period bars
    /// - Handles both overnight gaps and intraday movements
    /// - Most efficient unbiased estimator
    /// - Thread-safe for batch operations
    /// </summary>
    public static class YangZhangVolatility
    {
        #region Batch Calculation

        /// <summary>
        /// Calculates Yang-Zhang Volatility for entire price series.
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

            if (period < 2)
                throw new ArgumentException($"Period must be >= 2, got {period}", nameof(period));

            if (tradingDaysPerYear <= 0)
                throw new ArgumentException($"tradingDaysPerYear must be > 0, got {tradingDaysPerYear}",
                    nameof(tradingDaysPerYear));

            double[] result = new double[len];

            // Calculate optimal weight k
            double n = period;
            double k = 0.34 / (1.34 + (n + 1) / (n - 1));

            // Pre-calculate components for each bar
            double[] overnightLog = new double[len];
            double[] openCloseLog = new double[len];
            double[] rsComponent = new double[len];

            // First bar has no overnight component
            overnightLog[0] = double.NaN;
            openCloseLog[0] = double.NaN;
            rsComponent[0] = double.NaN;

            for (int i = 1; i < len; i++)
            {
                if (!MathBase.IsFinite(open[i]) || !MathBase.IsFinite(high[i]) ||
                    !MathBase.IsFinite(low[i]) || !MathBase.IsFinite(close[i]) ||
                    !MathBase.IsFinite(close[i - 1]))
                {
                    overnightLog[i] = double.NaN;
                    openCloseLog[i] = double.NaN;
                    rsComponent[i] = double.NaN;
                    continue;
                }

                // Overnight: ln(O[i] / C[i-1])
                double overnightRatio = MathBase.SafeDivide(open[i], close[i - 1]);
                overnightLog[i] = MathBase.SafeLog(overnightRatio);

                // Open-Close: ln(C[i] / O[i])
                double ocRatio = MathBase.SafeDivide(close[i], open[i]);
                openCloseLog[i] = MathBase.SafeLog(ocRatio);

                // Rogers-Satchell component: ln(H/C)*ln(H/O) + ln(L/C)*ln(L/O)
                double hcRatio = MathBase.SafeDivide(high[i], close[i]);
                double hoRatio = MathBase.SafeDivide(high[i], open[i]);
                double hcLog = MathBase.SafeLog(hcRatio);
                double hoLog = MathBase.SafeLog(hoRatio);

                double lcRatio = MathBase.SafeDivide(low[i], close[i]);
                double loRatio = MathBase.SafeDivide(low[i], open[i]);
                double lcLog = MathBase.SafeLog(lcRatio);
                double loLog = MathBase.SafeLog(loRatio);

                if (!MathBase.IsFinite(overnightLog[i]) || !MathBase.IsFinite(openCloseLog[i]) ||
                    !MathBase.IsFinite(hcLog) || !MathBase.IsFinite(hoLog) ||
                    !MathBase.IsFinite(lcLog) || !MathBase.IsFinite(loLog))
                {
                    overnightLog[i] = double.NaN;
                    openCloseLog[i] = double.NaN;
                    rsComponent[i] = double.NaN;
                    continue;
                }

                rsComponent[i] = hcLog * hoLog + lcLog * loLog;
            }

            // Calculate rolling Yang-Zhang volatility
            for (int i = 0; i < len; i++)
            {
                if (i < period)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Extract windows [i - period + 1, i]
                double[] overnightWindow = new double[period];
                double[] openCloseWindow = new double[period];
                double[] rsWindow = new double[period];
                bool hasNaN = false;

                for (int j = 0; j < period; j++)
                {
                    int idx = i - period + 1 + j;
                    overnightWindow[j] = overnightLog[idx];
                    openCloseWindow[j] = openCloseLog[idx];
                    rsWindow[j] = rsComponent[idx];

                    if (!MathBase.IsFinite(overnightWindow[j]) ||
                        !MathBase.IsFinite(openCloseWindow[j]) ||
                        !MathBase.IsFinite(rsWindow[j]))
                    {
                        hasNaN = true;
                        break;
                    }
                }

                if (hasNaN)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate variances (sample variance)
                double varOvernight = CalculateVariance(overnightWindow);
                double varOpenClose = CalculateVariance(openCloseWindow);
                double meanRS = CalculateMean(rsWindow);

                // Ensure non-negative
                if (meanRS < 0) meanRS = 0;

                // Yang-Zhang formula: σ²_YZ = σ²_overnight + k*σ²_open_close + (1-k)*σ²_RS
                double yzVariance = varOvernight + k * varOpenClose + (1 - k) * meanRS;

                // Ensure non-negative
                if (yzVariance < 0) yzVariance = 0;

                // Standard deviation
                double volatility = Math.Sqrt(yzVariance);

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

        #region Helper Methods

        private static double CalculateMean(double[] values)
        {
            double sum = 0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return sum / values.Length;
        }

        private static double CalculateVariance(double[] values)
        {
            int n = values.Length;
            if (n < 2) return 0;

            double mean = CalculateMean(values);
            double sumSquares = 0;

            for (int i = 0; i < n; i++)
            {
                double diff = values[i] - mean;
                sumSquares += diff * diff;
            }

            return sumSquares / (n - 1); // Sample variance
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful Yang-Zhang Volatility calculator for streaming updates.
        /// </summary>
        public class YangZhangCalculator
        {
            private readonly int _period;
            private readonly bool _annualize;
            private readonly double _tradingDaysPerYear;
            private readonly double _k;
            private readonly CircularBuffer<double> _overnightBuffer;
            private readonly CircularBuffer<double> _openCloseBuffer;
            private readonly CircularBuffer<double> _rsBuffer;
            private double _prevClose;

            public double Value { get; private set; }
            public bool IsReady => _overnightBuffer.Count >= _period;
            public int WarmupBarsLeft => Math.Max(0, _period - _overnightBuffer.Count);

            public YangZhangCalculator(int period = 20,
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

                double n = period;
                _k = 0.34 / (1.34 + (n + 1) / (n - 1));

                _overnightBuffer = new CircularBuffer<double>(period);
                _openCloseBuffer = new CircularBuffer<double>(period);
                _rsBuffer = new CircularBuffer<double>(period);
                _prevClose = double.NaN;
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

                // Calculate components
                if (MathBase.IsFinite(_prevClose))
                {
                    // Overnight
                    double overnightRatio = MathBase.SafeDivide(open, _prevClose);
                    double overnightLog = MathBase.SafeLog(overnightRatio);

                    // Open-Close
                    double ocRatio = MathBase.SafeDivide(close, open);
                    double openCloseLog = MathBase.SafeLog(ocRatio);

                    // RS component
                    double hcRatio = MathBase.SafeDivide(high, close);
                    double hoRatio = MathBase.SafeDivide(high, open);
                    double hcLog = MathBase.SafeLog(hcRatio);
                    double hoLog = MathBase.SafeLog(hoRatio);

                    double lcRatio = MathBase.SafeDivide(low, close);
                    double loRatio = MathBase.SafeDivide(low, open);
                    double lcLog = MathBase.SafeLog(lcRatio);
                    double loLog = MathBase.SafeLog(loRatio);

                    if (MathBase.IsFinite(overnightLog) && MathBase.IsFinite(openCloseLog) &&
                        MathBase.IsFinite(hcLog) && MathBase.IsFinite(hoLog) &&
                        MathBase.IsFinite(lcLog) && MathBase.IsFinite(loLog))
                    {
                        double rsComponent = hcLog * hoLog + lcLog * loLog;

                        _overnightBuffer.Add(overnightLog);
                        _openCloseBuffer.Add(openCloseLog);
                        _rsBuffer.Add(rsComponent);
                    }
                }

                _prevClose = close;

                if (!IsReady)
                {
                    Value = double.NaN;
                    return Value;
                }

                // Calculate variances
                double[] overnightData = _overnightBuffer.ToArray();
                double[] openCloseData = _openCloseBuffer.ToArray();
                double[] rsData = _rsBuffer.ToArray();

                double varOvernight = CalculateVariance(overnightData);
                double varOpenClose = CalculateVariance(openCloseData);
                double meanRS = CalculateMean(rsData);

                if (meanRS < 0) meanRS = 0;

                double yzVariance = varOvernight + _k * varOpenClose + (1 - _k) * meanRS;
                if (yzVariance < 0) yzVariance = 0;

                double volatility = Math.Sqrt(yzVariance);

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
                _overnightBuffer.Clear();
                _openCloseBuffer.Clear();
                _rsBuffer.Clear();
                _prevClose = double.NaN;
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
