using System;
using TradingLibrary.Core;
using AvgMode = TradingLibrary.Core.MovingAveragesExtensions.AvgMode;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Keltner Channels - volatility-based price channels using ATR or TR-based EMA.
    /// 
    /// Formula:
    /// - Basis = MA(Close, basisPeriod, basisAvg)
    /// - Upper = Basis + mult × Dev
    /// - Lower = Basis - mult × Dev
    /// 
    /// где Dev = ATR(period, atrMode) или EMA(TrueRange, period)
    /// 
    /// Reference: Keltner, Chester W. (1960) and Linda Bradford Raschke modification
    /// Specification: Volatility_Spec_v1_0.md Section 4
    /// 
    /// Key properties:
    /// - Warm-up period: max(basisPeriod, atrPeriod)
    /// - More adaptive than Donchian, smoother than Bollinger
    /// - Thread-safe for batch operations
    /// </summary>
    public static class KeltnerChannels
    {
        #region Enums

        /// <summary>
        /// Deviation calculation mode for Keltner Channels
        /// </summary>
        public enum AtrDevMode
        {
            /// <summary>Use ATR (Average True Range)</summary>
            ATR = 0,
            /// <summary>Use EMA of True Range directly</summary>
            TR_EMA = 1
        }

        #endregion

        #region Result Structure

        /// <summary>
        /// Keltner Channels calculation result
        /// </summary>
        public struct KeltnerResult
        {
            /// <summary>Upper channel (basis + mult × Dev)</summary>
            public double Upper;

            /// <summary>Middle line (basis MA)</summary>
            public double Basis;

            /// <summary>Lower channel (basis - mult × Dev)</summary>
            public double Lower;

            /// <summary>
            /// Constructor for result
            /// </summary>
            public KeltnerResult(double upper, double basis, double lower)
            {
                Upper = upper;
                Basis = basis;
                Lower = lower;
            }

            /// <summary>
            /// Creates invalid result (all NaN)
            /// </summary>
            public static KeltnerResult Invalid => new KeltnerResult(double.NaN, double.NaN, double.NaN);
        }

        #endregion

        #region Batch Calculation - Keltner Channels

        /// <summary>
        /// Calculates Keltner Channels for entire price series.
        /// Returns three arrays: upper, basis, lower channels.
        /// </summary>
        /// <param name="high">High prices (chronological: 0 → oldest)</param>
        /// <param name="low">Low prices</param>
        /// <param name="close">Close prices</param>
        /// <param name="basisPeriod">Period for basis MA (default: 20)</param>
        /// <param name="basisAvg">Basis line averaging method (default: EMA)</param>
        /// <param name="atrPeriod">Period for ATR/deviation calculation (default: 10)</param>
        /// <param name="devMode">Deviation calculation mode (default: ATR)</param>
        /// <param name="atrMode">ATR smoothing mode if devMode=ATR (default: RMA)</param>
        /// <param name="mult">Deviation multiplier for channels width (default: 2.0)</param>
        /// <returns>Tuple of (upper[], basis[], lower[]) arrays</returns>
        /// <exception cref="ArgumentNullException">If any input array is null</exception>
        /// <exception cref="ArgumentException">If arrays have different lengths or periods &lt; 1</exception>
        public static (double[] upper, double[] basis, double[] lower) Calculate(
            double[] high, double[] low, double[] close,
            int basisPeriod = 20,
            MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.EMA,
            int atrPeriod = 10,
            AtrDevMode devMode = AtrDevMode.ATR,
            ATR.AtrMode atrMode = ATR.AtrMode.RMA,
            double mult = 2.0)
        {
            // Validate inputs
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (close == null) throw new ArgumentNullException(nameof(close));

            int len = high.Length;
            if (low.Length != len || close.Length != len)
                throw new ArgumentException("Input arrays must have equal length");

            if (basisPeriod < 1)
                throw new ArgumentException($"basisPeriod must be >= 1, got {basisPeriod}", nameof(basisPeriod));

            if (atrPeriod < 1)
                throw new ArgumentException($"atrPeriod must be >= 1, got {atrPeriod}", nameof(atrPeriod));

            // Calculate basis line
            double[] basis = MovingAverages.CalculateAverage(close, basisPeriod, basisAvg);

            // Calculate deviation based on mode
            double[] dev;
            if (devMode == AtrDevMode.ATR)
            {
                // Use ATR
                dev = ATR.Calculate(high, low, close, atrPeriod, atrMode);
            }
            else // TR_EMA
            {
                // Calculate True Range and apply EMA
                double[] tr = PriceAction.TrueRange(high, low, close);
                dev = MovingAverages.EMA(tr, atrPeriod, MovingAverages.SeedMode.SmaSeed);
            }

            // Calculate channels
            double[] upper = new double[len];
            double[] lower = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(basis[i]) || !MathBase.IsFinite(dev[i]))
                {
                    upper[i] = double.NaN;
                    lower[i] = double.NaN;
                }
                else
                {
                    double offset = mult * dev[i];
                    upper[i] = basis[i] + offset;
                    lower[i] = basis[i] - offset;
                }
            }

            return (upper, basis, lower);
        }

        #endregion

        #region Batch Calculation - Keltner BandWidth

        /// <summary>
        /// Calculates Keltner BandWidth indicator.
        /// 
        /// Formula: KBW = (Upper - Lower) / Basis
        /// 
        /// Useful for identifying volatility compression/expansion.
        /// </summary>
        /// <param name="high">High prices</param>
        /// <param name="low">Low prices</param>
        /// <param name="close">Close prices</param>
        /// <param name="basisPeriod">Period for basis MA</param>
        /// <param name="basisAvg">Basis averaging method</param>
        /// <param name="atrPeriod">Period for ATR/deviation</param>
        /// <param name="devMode">Deviation calculation mode</param>
        /// <param name="atrMode">ATR smoothing mode</param>
        /// <param name="mult">Deviation multiplier</param>
        /// <returns>BandWidth values array, NaN during warm-up or when basis ≈ 0</returns>
        public static double[] BandWidth(
            double[] high, double[] low, double[] close,
            int basisPeriod = 20,
            MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.EMA,
            int atrPeriod = 10,
            AtrDevMode devMode = AtrDevMode.ATR,
            ATR.AtrMode atrMode = ATR.AtrMode.RMA,
            double mult = 2.0)
        {
            // Calculate Keltner Channels
            var (upper, basis, lower) = Calculate(high, low, close, basisPeriod, basisAvg, 
                                                  atrPeriod, devMode, atrMode, mult);

            int len = high.Length;
            double[] bandWidth = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(upper[i]) || !MathBase.IsFinite(lower[i]) || !MathBase.IsFinite(basis[i]))
                {
                    bandWidth[i] = double.NaN;
                    continue;
                }

                // BandWidth = (upper - lower) / basis
                double width = upper[i] - lower[i];
                bandWidth[i] = MathBase.SafeDivide(width, basis[i]);
            }

            return bandWidth;
        }

        #endregion

        #region Stateful Calculator

        /// <summary>
        /// Stateful Keltner Channels calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class KeltnerCalculator
        {
            private readonly int _basisPeriod;
            private readonly int _atrPeriod;
            private readonly double _mult;
            private readonly MovingAverages.AvgMode _basisAvg;
            private readonly AtrDevMode _devMode;
            private readonly ATR.AtrMode _atrMode;
            private readonly object _basisMaState;
            private readonly object _devState; // ATRCalculator or EMAState depending on devMode
            private int _count;

            /// <summary>
            /// Current channels result (all NaN until warmed up)
            /// </summary>
            public KeltnerResult Value { get; private set; }

            /// <summary>
            /// Current BandWidth value (NaN until warmed up)
            /// </summary>
            public double BandWidth { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _count >= Math.Max(_basisPeriod, _atrPeriod + 1);

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => Math.Max(0, Math.Max(_basisPeriod, _atrPeriod + 1) - _count);

            /// <summary>
            /// Creates new Keltner Channels calculator
            /// </summary>
            /// <param name="basisPeriod">Period for basis MA (must be >= 1)</param>
            /// <param name="basisAvg">Basis line averaging method</param>
            /// <param name="atrPeriod">Period for ATR/deviation (must be >= 1)</param>
            /// <param name="devMode">Deviation calculation mode</param>
            /// <param name="atrMode">ATR smoothing mode if devMode=ATR</param>
            /// <param name="mult">Deviation multiplier for channels width</param>
            public KeltnerCalculator(int basisPeriod = 20,
                                    MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.EMA,
                                    int atrPeriod = 10,
                                    AtrDevMode devMode = AtrDevMode.ATR,
                                    ATR.AtrMode atrMode = ATR.AtrMode.RMA,
                                    double mult = 2.0)
            {
                if (basisPeriod < 1)
                    throw new ArgumentException($"basisPeriod must be >= 1, got {basisPeriod}", nameof(basisPeriod));

                if (atrPeriod < 1)
                    throw new ArgumentException($"atrPeriod must be >= 1, got {atrPeriod}", nameof(atrPeriod));

                _basisPeriod = basisPeriod;
                _atrPeriod = atrPeriod;
                _mult = mult;
                _basisAvg = basisAvg;
                _devMode = devMode;
                _atrMode = atrMode;
                _count = 0;

                Value = KeltnerResult.Invalid;
                BandWidth = double.NaN;

                // Initialize basis MA calculator
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

                // Initialize deviation calculator
                if (devMode == AtrDevMode.ATR)
                {
                    _devState = new ATR.ATRCalculator(atrPeriod, atrMode);
                }
                else // TR_EMA
                {
                    _devState = new MovingAverages.EMAState(atrPeriod, MovingAverages.SeedMode.SmaSeed);
                }
            }

            /// <summary>
            /// Updates Keltner Channels with new bar data
            /// </summary>
            /// <param name="high">Bar high price</param>
            /// <param name="low">Bar low price</param>
            /// <param name="close">Bar close price</param>
            /// <returns>Updated channels result (all NaN during warm-up)</returns>
            public KeltnerResult Update(double high, double low, double close)
            {
                // Validate inputs
                if (!MathBase.IsFinite(high) || !MathBase.IsFinite(low) || !MathBase.IsFinite(close))
                {
                    Value = KeltnerResult.Invalid;
                    BandWidth = double.NaN;
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

                // Update deviation
                double dev = double.NaN;
                if (_devMode == AtrDevMode.ATR)
                {
                    dev = ((ATR.ATRCalculator)_devState).Update(high, low, close);
                }
                else // TR_EMA
                {
                    // Calculate True Range for current bar
                    // For first bar, TR = high - low
                    // For subsequent bars, TR already calculated by EMA state receiving TR values
                    // We need to maintain previous close
                    // Simplified: calculate TR inline
                    double tr = PriceAction.TrueRange(high, low, close, _prevClose);
                    dev = ((MovingAverages.EMAState)_devState).Update(tr);
                    _prevClose = close;
                }

                // Calculate channels
                if (!MathBase.IsFinite(basis) || !MathBase.IsFinite(dev))
                {
                    Value = KeltnerResult.Invalid;
                    BandWidth = double.NaN;
                }
                else
                {
                    double offset = _mult * dev;
                    double upper = basis + offset;
                    double lower = basis - offset;

                    Value = new KeltnerResult(upper, basis, lower);

                    // Calculate BandWidth = (upper - lower) / basis
                    double width = upper - lower;
                    BandWidth = MathBase.SafeDivide(width, basis);
                }

                return Value;
            }

            private double _prevClose = double.NaN;

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _count = 0;
                _prevClose = double.NaN;
                Value = KeltnerResult.Invalid;
                BandWidth = double.NaN;

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

                // Reset deviation state
                if (_devMode == AtrDevMode.ATR)
                {
                    ((ATR.ATRCalculator)_devState).Reset();
                }
                else
                {
                    ((MovingAverages.EMAState)_devState).Reset();
                }
            }
        }

        #endregion
    }
}
