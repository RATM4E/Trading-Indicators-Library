using System;
using TradingLibrary.Core;
using AvgMode = TradingLibrary.Core.MovingAveragesExtensions.AvgMode;

namespace TradingLibrary.Indicators.Volatility
{
    /// <summary>
    /// Bollinger Bands - volatility-based price channels using standard deviation.
    /// 
    /// Formula:
    /// - Basis = MA(src, basisPeriod, basisAvg)
    /// - Upper = Basis + devMult × StdDev(src, basisPeriod)
    /// - Lower = Basis - devMult × StdDev(src, basisPeriod)
    /// 
    /// Additional metrics:
    /// - %B = (src - Lower) / (Upper - Lower)  ∈ [0,1]
    /// - BandWidth = (Upper - Lower) / Basis
    /// 
    /// Reference: Bollinger, John (1992) "Bollinger on Bollinger Bands"
    /// Specification: Volatility_Spec_v1_0.md Section 3
    /// 
    /// Key properties:
    /// - Warm-up period: basisPeriod
    /// - Thread-safe for batch operations
    /// - %B > 1 means price above upper band
    /// - %B < 0 means price below lower band
    /// </summary>
    public static class BollingerBands
    {
        #region Result Structure

        /// <summary>
        /// Bollinger Bands calculation result
        /// </summary>
        public struct BollingerResult
        {
            /// <summary>Upper band (basis + devMult × StdDev)</summary>
            public double Upper;

            /// <summary>Middle line (basis MA)</summary>
            public double Basis;

            /// <summary>Lower band (basis - devMult × StdDev)</summary>
            public double Lower;

            /// <summary>
            /// Constructor for result
            /// </summary>
            public BollingerResult(double upper, double basis, double lower)
            {
                Upper = upper;
                Basis = basis;
                Lower = lower;
            }

            /// <summary>
            /// Creates invalid result (all NaN)
            /// </summary>
            public static BollingerResult Invalid => new BollingerResult(double.NaN, double.NaN, double.NaN);
        }

        #endregion

        #region Batch Calculation - Bollinger Bands

        /// <summary>
        /// Calculates Bollinger Bands for entire series.
        /// Returns three arrays: upper, basis, lower bands.
        /// </summary>
        /// <param name="src">Source values (chronological: 0 → oldest)</param>
        /// <param name="basisPeriod">Period for basis MA and StdDev (default: 20)</param>
        /// <param name="devMult">Standard deviation multiplier (default: 2.0)</param>
        /// <param name="basisAvg">Basis line averaging method (default: SMA)</param>
        /// <param name="sampleStd">Use sample std (n-1) if true, else population (n) (default: true)</param>
        /// <returns>Tuple of (upper[], basis[], lower[]) arrays</returns>
        /// <exception cref="ArgumentNullException">If src is null</exception>
        /// <exception cref="ArgumentException">If basisPeriod &lt; 1</exception>
        public static (double[] upper, double[] basis, double[] lower) Calculate(
            double[] src,
            int basisPeriod = 20,
            double devMult = 2.0,
            MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.SMA,
            bool sampleStd = true)
        {
            // Validate inputs
            if (src == null) throw new ArgumentNullException(nameof(src));

            if (basisPeriod < 1)
                throw new ArgumentException($"basisPeriod must be >= 1, got {basisPeriod}", nameof(basisPeriod));

            int len = src.Length;

            // Calculate basis line
            double[] basis = MovingAverages.CalculateAverage(src, basisPeriod, basisAvg);

            // Calculate standard deviation
            double[] stdDev = StdDevIndicator.StdDev(src, basisPeriod, sampleStd);

            // Calculate bands
            double[] upper = new double[len];
            double[] lower = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(basis[i]) || !MathBase.IsFinite(stdDev[i]))
                {
                    upper[i] = double.NaN;
                    lower[i] = double.NaN;
                }
                else
                {
                    double offset = devMult * stdDev[i];
                    upper[i] = basis[i] + offset;
                    lower[i] = basis[i] - offset;
                }
            }

            return (upper, basis, lower);
        }

        #endregion

        #region Batch Calculation - Bollinger %B

        /// <summary>
        /// Calculates Bollinger %B (Percent B) indicator.
        /// 
        /// Formula: %B = (src - Lower) / (Upper - Lower)
        /// 
        /// Interpretation:
        /// - %B = 1.0: price at upper band
        /// - %B = 0.5: price at middle band
        /// - %B = 0.0: price at lower band
        /// - %B > 1.0: price above upper band
        /// - %B < 0.0: price below lower band
        /// </summary>
        /// <param name="src">Source values</param>
        /// <param name="basisPeriod">Period for BB calculation</param>
        /// <param name="devMult">Standard deviation multiplier</param>
        /// <param name="basisAvg">Basis averaging method</param>
        /// <param name="sampleStd">Use sample std</param>
        /// <returns>%B values array, NaN during warm-up or when bands width ≈ 0</returns>
        public static double[] PercentB(
            double[] src,
            int basisPeriod = 20,
            double devMult = 2.0,
            MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.SMA,
            bool sampleStd = true)
        {
            // Calculate Bollinger Bands
            var (upper, basis, lower) = Calculate(src, basisPeriod, devMult, basisAvg, sampleStd);

            int len = src.Length;
            double[] percentB = new double[len];

            for (int i = 0; i < len; i++)
            {
                if (!MathBase.IsFinite(upper[i]) || !MathBase.IsFinite(lower[i]) || !MathBase.IsFinite(src[i]))
                {
                    percentB[i] = double.NaN;
                    continue;
                }

                // %B = (src - lower) / (upper - lower)
                double width = upper[i] - lower[i];
                percentB[i] = MathBase.SafeDivide(src[i] - lower[i], width);
            }

            return percentB;
        }

        #endregion

        #region Batch Calculation - Bollinger BandWidth

        /// <summary>
        /// Calculates Bollinger BandWidth indicator.
        /// 
        /// Formula: BandWidth = (Upper - Lower) / Basis
        /// 
        /// Useful for identifying:
        /// - Volatility squeeze (low BandWidth)
        /// - Volatility expansion (high BandWidth)
        /// </summary>
        /// <param name="src">Source values</param>
        /// <param name="basisPeriod">Period for BB calculation</param>
        /// <param name="devMult">Standard deviation multiplier</param>
        /// <param name="basisAvg">Basis averaging method</param>
        /// <param name="sampleStd">Use sample std</param>
        /// <returns>BandWidth values array, NaN during warm-up or when basis ≈ 0</returns>
        public static double[] BandWidth(
            double[] src,
            int basisPeriod = 20,
            double devMult = 2.0,
            MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.SMA,
            bool sampleStd = true)
        {
            // Calculate Bollinger Bands
            var (upper, basis, lower) = Calculate(src, basisPeriod, devMult, basisAvg, sampleStd);

            int len = src.Length;
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
        /// Stateful Bollinger Bands calculator for streaming/incremental updates.
        /// Thread-safety: Not thread-safe, use one instance per thread.
        /// </summary>
        public class BollingerCalculator
        {
            private readonly int _basisPeriod;
            private readonly double _devMult;
            private readonly bool _sampleStd;
            private readonly MovingAverages.AvgMode _basisAvg;
            private readonly object _basisMaState;
            private readonly StdDevIndicator.StdDevCalculator _stdCalc;

            /// <summary>
            /// Current bands result (all NaN until warmed up)
            /// </summary>
            public BollingerResult Value { get; private set; }

            /// <summary>
            /// Current %B value (NaN until warmed up)
            /// </summary>
            public double PercentB { get; private set; }

            /// <summary>
            /// Current BandWidth value (NaN until warmed up)
            /// </summary>
            public double BandWidth { get; private set; }

            /// <summary>
            /// True if calculator has received enough data for valid output
            /// </summary>
            public bool IsReady => _count >= _basisPeriod;

            /// <summary>
            /// Number of bars remaining until warm-up complete
            /// </summary>
            public int WarmupBarsLeft => Math.Max(0, _basisPeriod - _count);

            private int _count;
            private double _lastSrc;

            /// <summary>
            /// Creates new Bollinger Bands calculator
            /// </summary>
            /// <param name="basisPeriod">Period for basis MA and StdDev (must be >= 1)</param>
            /// <param name="devMult">Standard deviation multiplier</param>
            /// <param name="basisAvg">Basis line averaging method</param>
            /// <param name="sampleStd">Use sample std (n-1) if true, else population (n)</param>
            public BollingerCalculator(int basisPeriod = 20,
                                      double devMult = 2.0,
                                      MovingAverages.AvgMode basisAvg = MovingAverages.AvgMode.SMA,
                                      bool sampleStd = true)
            {
                if (basisPeriod < 1)
                    throw new ArgumentException($"basisPeriod must be >= 1, got {basisPeriod}", nameof(basisPeriod));

                _basisPeriod = basisPeriod;
                _devMult = devMult;
                _basisAvg = basisAvg;
                _sampleStd = sampleStd;
                _count = 0;

                Value = BollingerResult.Invalid;
                PercentB = double.NaN;
                BandWidth = double.NaN;
                _lastSrc = double.NaN;

                // Initialize StdDev calculator
                _stdCalc = new StdDevIndicator.StdDevCalculator(basisPeriod, sampleStd);

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
            }

            /// <summary>
            /// Updates Bollinger Bands with new value
            /// </summary>
            /// <param name="src">New source value</param>
            /// <returns>Updated bands result (all NaN during warm-up)</returns>
            public BollingerResult Update(double src)
            {
                // Validate input
                if (!MathBase.IsFinite(src))
                {
                    Value = BollingerResult.Invalid;
                    PercentB = double.NaN;
                    BandWidth = double.NaN;
                    return Value;
                }

                _count++;
                _lastSrc = src;

                // Update basis MA
                double basis = double.NaN;
                switch (_basisAvg)
                {
                    case MovingAverages.AvgMode.SMA:
                        basis = ((MovingAverages.SMAState)_basisMaState).Update(src);
                        break;

                    case MovingAverages.AvgMode.EMA:
                        basis = ((MovingAverages.EMAState)_basisMaState).Update(src);
                        break;

                    case MovingAverages.AvgMode.RMA:
                        basis = ((MovingAverages.RMAState)_basisMaState).Update(src);
                        break;

                    case MovingAverages.AvgMode.WMA:
                        basis = ((MovingAverages.WMAState)_basisMaState).Update(src);
                        break;
                }

                // Update StdDev
                double stdDev = _stdCalc.Update(src);

                // Calculate bands
                if (!MathBase.IsFinite(basis) || !MathBase.IsFinite(stdDev))
                {
                    Value = BollingerResult.Invalid;
                    PercentB = double.NaN;
                    BandWidth = double.NaN;
                }
                else
                {
                    double offset = _devMult * stdDev;
                    double upper = basis + offset;
                    double lower = basis - offset;

                    Value = new BollingerResult(upper, basis, lower);

                    // Calculate %B = (src - lower) / (upper - lower)
                    double width = upper - lower;
                    PercentB = MathBase.SafeDivide(src - lower, width);

                    // Calculate BandWidth = (upper - lower) / basis
                    BandWidth = MathBase.SafeDivide(width, basis);
                }

                return Value;
            }

            /// <summary>
            /// Resets calculator to initial state
            /// </summary>
            public void Reset()
            {
                _count = 0;
                _lastSrc = double.NaN;
                Value = BollingerResult.Invalid;
                PercentB = double.NaN;
                BandWidth = double.NaN;

                _stdCalc.Reset();

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
