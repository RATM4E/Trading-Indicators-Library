using System;
using TradingLibrary.Core;
using static TradingLibrary.Core.MovingAveragesExtensions;
using AvgMode = TradingLibrary.Core.MovingAveragesExtensions.AvgMode;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// ATR Bands - volatility-based price channels using ATR.
    /// Similar to Bollinger Bands but uses ATR instead of standard deviation.
    /// 
    /// Formula:
    /// - Basis = MA(Close, basisPeriod, basisAvg)
    /// - Upper = Basis + mult × ATR(atrPeriod, atrMode)
    /// - Lower = Basis - mult × ATR(atrPeriod, atrMode)
    /// 
    /// Specification: Volatility_Spec_v1_0.md Section 1.3
    /// 
    /// Key properties:
    /// - Warm-up period: max(basisPeriod, atrPeriod)
    /// - Thread-safe for batch operations
    /// - Useful for adaptive stop-loss and take-profit levels
    /// </summary>
    public static class ATRBands
    {
        #region Result Structure

        /// <summary>
        /// ATR Bands calculation result
        /// </summary>
        public struct ATRBandsResult
        {
            /// <summary>Upper band (basis + mult × ATR)</summary>
            public double Upper;

            /// <summary>Middle line (basis MA)</summary>
            public double Basis;

            /// <summary>Lower band (basis - mult × ATR)</summary>
            public double Lower;

            /// <summary>
            /// Constructor for result
            /// </summary>
            public ATRBandsResult(double upper, double basis, double lower)
            {
                Upper = upper;
                Basis = basis;
                Lower = lower;
            }

            /// <summary>
            /// Creates invalid result (all NaN)
            /// </summary>
            public static ATRBandsResult Invalid => new ATRBandsResult(double.NaN, double.NaN, double.NaN);
        }

        #endregion

        #region Batch Calculation

        /// <summary>
        /// Calculates ATR Bands for entire price series.
        /// Returns three arrays: upper, basis, lower bands.
        /// </summary>
        /// <param name="close">Close prices (chronological: 0 → oldest)</param>
        /// <param name="high">High prices (for ATR calculation)</param>
        /// <param name="low">Low prices (for ATR calculation)</param>
        /// <param name="basisPeriod">Period for basis moving average (default: 20)</param>
        /// <param name="atrPeriod">Period for ATR calculation (default: 14)</param>
        /// <param name="atrMode">ATR smoothing mode (default: RMA)</param>
        /// <param name="mult">ATR multiplier for bands width (default: 2.0)</param>
        /// <param name="basisAvg">Basis line averaging method (default: EMA)</param>
        /// <returns>Tuple of (upper[], basis[], lower[]) arrays</returns>
        /// <exception cref="ArgumentNullException">If any input array is null</exception>
        /// <exception cref="ArgumentException">If arrays have different lengths or periods &lt; 1</exception>
        public static (double[] upper, double[] basis, double[] lower) Calculate(
            double[] close, double[] high, double[] low,
            int basisPeriod = 20,
            int atrPeriod = 14,
            ATR.AtrMode atrMode = ATR.AtrMode.RMA,
            double mult = 2.0,
            MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.EMA)
        {
            // Validate inputs
            if (close == null) throw new ArgumentNullException(nameof(close));
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));

            int len = close.Length;
            if (high.Length != len || low.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (basisPeriod < 1)
                throw new ArgumentException($"basisPeriod must be >= 1, got {basisPeriod}", nameof(basisPeriod));

            if (atrPeriod < 1)
                throw new ArgumentException($"atrPeriod must be >= 1, got {atrPeriod}", nameof(atrPeriod));

            // Calculate basis line
            double[] basis = MovingAverages.CalculateAverage(close, basisPeriod, basisAvg);

            // Calculate ATR
            double[] atr = ATR.Calculate(high, low, close, atrPeriod, atrMode);

            // Calculate bands
            double[] upper = new double[len];
            double[] lower = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(basis[i]) || !MathBase.IsFinite(atr[i]))
                {
                    upper[i] = double.NaN;
                    lower[i] = double.NaN;
                }
                else
                {
                    double offset = mult * atr[i];
                    upper[i] = basis[i] + offset;
                    lower[i] = basis[i] - offset;
                }
            }

            return (upper, basis, lower);
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful ATR Bands calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class ATRBandsCalculator
        {
            private readonly int _basisPeriod;
            private readonly int _atrPeriod;
            private readonly double _mult;
            private readonly ATR.ATRCalculator _atrCalc;
            private readonly object _basisMaState;
            private readonly MovingAverages.AvgMode _basisAvg;

            /// <summary>
            /// Current bands result (all NaN until warmed up)
            /// </summary>
            public ATRBandsResult Value { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _count >= Math.Max(_basisPeriod, _atrPeriod + 1);

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => Math.Max(0, Math.Max(_basisPeriod, _atrPeriod + 1) - _count);

            private int _count;

            /// <summary>
            /// Creates new ATR Bands calculator
            /// </summary>
            /// <param name="basisPeriod">Period for basis MA (must be >= 1)</param>
            /// <param name="atrPeriod">Period for ATR (must be >= 1)</param>
            /// <param name="atrMode">ATR smoothing mode</param>
            /// <param name="mult">ATR multiplier for bands width</param>
            /// <param name="basisAvg">Basis line averaging method</param>
            public ATRBandsCalculator(int basisPeriod = 20,
                                     int atrPeriod = 14,
                                     ATR.AtrMode atrMode = ATR.AtrMode.RMA,
                                     double mult = 2.0,
                                     MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.EMA)
            {
                if (basisPeriod < 1)
                    throw new ArgumentException($"basisPeriod must be >= 1, got {basisPeriod}", nameof(basisPeriod));

                if (atrPeriod < 1)
                    throw new ArgumentException($"atrPeriod must be >= 1, got {atrPeriod}", nameof(atrPeriod));

                _basisPeriod = basisPeriod;
                _atrPeriod = atrPeriod;
                _mult = mult;
                _basisAvg = basisAvg;
                _count = 0;

                Value = ATRBandsResult.Invalid;

                // Initialize ATR calculator
                _atrCalc = new ATR.ATRCalculator(atrPeriod, atrMode);

                // Initialize basis MA calculator based on selected mode
                switch (basisAvg)
                {
                    case MovingAverages.AvgMode.SMA:
                        _basisMaState = new MovingAverages.SMAState(basisPeriod);
                        break;

                    case MovingAverages.AvgMode.EMA:
                        _basisMaState = new MovingAverages.EMAState(basisPeriod, MovingAverages.SeedMode.SmaSeed);
                        break;

                    case MovingAverages.AvgMode.RMA:
                        _basisMaState = new MovingAverages.RMAState(basisPeriod);
                        break;

                    case MovingAverages.AvgMode.WMA:
                        _basisMaState = new MovingAverages.WMAState(basisPeriod);
                        break;

                    default:
                        throw new ArgumentException($"Unsupported basis average mode: {basisAvg}", nameof(basisAvg));
                }
            }

            /// <summary>
            /// Updates ATR Bands with new bar data
            /// </summary>
            /// <param name="close">Bar close price</param>
            /// <param name="high">Bar high price</param>
            /// <param name="low">Bar low price</param>
            /// <returns>Updated bands result (all NaN during warm-up)</returns>
            public ATRBandsResult Update(double close, double high, double low)
            {
                // Validate inputs
                if (!MathBase.IsFinite(close) || !MathBase.IsFinite(high) || !MathBase.IsFinite(low))
                {
                    Value = ATRBandsResult.Invalid;
                    return Value;
                }

                _count++;

                // Update basis MA
                double basis = double.NaN;
                switch (_basisAvg)
                {
                    case MovingAverages.AvgMode.SMA:
                        basis = ((MovingAverages.SMAState)_basisMaState).Update(close);
                        break;

                    case MovingAverages.AvgMode.EMA:
                        basis = ((MovingAverages.EMAState)_basisMaState).Update(close);
                        break;

                    case MovingAverages.AvgMode.RMA:
                        basis = ((MovingAverages.RMAState)_basisMaState).Update(close);
                        break;

                    case MovingAverages.AvgMode.WMA:
                        basis = ((MovingAverages.WMAState)_basisMaState).Update(close);
                        break;
                }

                // Update ATR
                double atr = _atrCalc.Update(high, low, close);

                // Calculate bands
                if (!MathBase.IsFinite(basis) || !MathBase.IsFinite(atr))
                {
                    Value = ATRBandsResult.Invalid;
                }
                else
                {
                    double offset = _mult * atr;
                    Value = new ATRBandsResult(
                        basis + offset,  // upper
                        basis,           // basis
                        basis - offset   // lower
                    );
                }

                return Value;
            }

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _count = 0;
                _atrCalc.Reset();
                Value = ATRBandsResult.Invalid;

                // Reset basis MA state
                switch (_basisAvg)
                {
                    case MovingAverages.AvgMode.SMA:
                        ((MovingAverages.SMAState)_basisMaState).Reset();
                        break;

                    case MovingAverages.AvgMode.EMA:
                        ((MovingAverages.EMAState)_basisMaState).Reset();
                        break;

                    case MovingAverages.AvgMode.RMA:
                        ((MovingAverages.RMAState)_basisMaState).Reset();
                        break;

                    case MovingAverages.AvgMode.WMA:
                        ((MovingAverages.WMAState)_basisMaState).Reset();
                        break;
                }
            }
        }

        #endregion
    }
}
