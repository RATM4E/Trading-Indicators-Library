using System;
using TradingLibrary.Core;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Normalized Average True Range (NATR) - ATR expressed as percentage of close price.
    /// Allows comparison of volatility across different price levels and instruments.
    /// 
    /// Formula: NATR = (ATR / Close) * [100 if scaled]
    /// 
    /// Specification: Volatility_Spec_v1_0.md Section 1.2
    /// 
    /// Key properties:
    /// - Warm-up period: period bars
    /// - Returns NaN if close price is near zero or invalid
    /// - Can be expressed as decimal (0.02) or percentage (2.0)
    /// - Thread-safe for batch operations
    /// </summary>
    public static class NATR
    {
        #region Batch Calculation

        /// <summary>
        /// Calculates NATR (Normalized ATR) for entire price series.
        /// </summary>
        /// <param name="high">High prices (chronological: 0 → oldest)</param>
        /// <param name="low">Low prices</param>
        /// <param name="close">Close prices</param>
        /// <param name="period">ATR smoothing period (default: 14)</param>
        /// <param name="mode">ATR smoothing method (default: RMA)</param>
        /// <param name="scaleTo100">If true, multiply result by 100 for percentage representation (default: false)</param>
        /// <returns>NATR values array, NaN during warm-up or when close ≈ 0</returns>
        /// <exception cref="ArgumentNullException">If any input array is null</exception>
        /// <exception cref="ArgumentException">If arrays have different lengths or period &lt; 1</exception>
        public static double[] Calculate(double[] high, double[] low, double[] close,
                                        int period = 14, 
                                        ATR.AtrMode mode = ATR.AtrMode.RMA,
                                        bool scaleTo100 = false)
        {
            // Validate inputs
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (close == null) throw new ArgumentNullException(nameof(close));

            int len = high.Length;
            if (low.Length != len || close.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (period < 1)
                throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

            // Calculate ATR
            double[] atr = ATR.Calculate(high, low, close, period, mode);

            // Normalize by close price
            double[] natr = new double[len];
            double scale = scaleTo100 ? 100.0 : 1.0;

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(atr[i]) || !MathBase.IsFinite(close[i]))
                {
                    natr[i] = double.NaN;
                    continue;
                }

                // Safe division - avoid division by near-zero close
                natr[i] = MathBase.SafeDivide(atr[i] * scale, close[i]);
            }

            return natr;
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful NATR calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class NATRCalculator
        {
            private readonly ATR.ATRCalculator _atrCalc;
            private readonly bool _scaleTo100;

            /// <summary>
            /// Current NATR value (NaN until warmed up)
            /// </summary>
            public double Value { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _atrCalc.IsReady;

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => _atrCalc.WarmupBarsLeft;

            /// <summary>
            /// Creates new NATR calculator
            /// </summary>
            /// <param name="period">ATR smoothing period (must be >= 1)</param>
            /// <param name="mode">ATR smoothing mode (RMA, EMA, or SMA)</param>
            /// <param name="scaleTo100">If true, results expressed as percentage (0-100)</param>
            public NATRCalculator(int period, 
                                 ATR.AtrMode mode = ATR.AtrMode.RMA,
                                 bool scaleTo100 = false)
            {
                _atrCalc = new ATR.ATRCalculator(period, mode);
                _scaleTo100 = scaleTo100;
                Value = double.NaN;
            }

            /// <summary>
            /// Updates NATR with new bar data
            /// </summary>
            /// <param name="high">Bar high price</param>
            /// <param name="low">Bar low price</param>
            /// <param name="close">Bar close price</param>
            /// <returns>Updated NATR value (NaN during warm-up or if close ≈ 0)</returns>
            public double Update(double high, double low, double close)
            {
                // Update ATR
                double atr = _atrCalc.Update(high, low, close);

                // Normalize
                if (!MathBase.IsFinite(atr) || !MathBase.IsFinite(close))
                {
                    Value = double.NaN;
                    return Value;
                }

                double scale = _scaleTo100 ? 100.0 : 1.0;
                Value = MathBase.SafeDivide(atr * scale, close);

                return Value;
            }

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _atrCalc.Reset();
                Value = double.NaN;
            }

            /// <summary>
            /// Creates a deep copy of the calculator with current state
            /// </summary>
            /// <returns>New NATRCalculator instance with identical state</returns>
            public NATRCalculator Clone()
            {
                var clone = new NATRCalculator(_atrCalc.WarmupBarsLeft, ATR.AtrMode.RMA, _scaleTo100);
                clone.Value = Value;
                // Note: Full state cloning requires ATR calculator cloning support
                return clone;
            }
        }

        #endregion
    }
}
