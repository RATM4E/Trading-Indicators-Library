using System;
using System.Linq;

namespace TradingLibrary.Core
{
    /// <summary>
    /// Core mathematical utilities providing platform-independent operations.
    /// All functions follow strict deterministic rules with explicit NaN propagation.
    /// Reference: SharedLibrary/Core specification - MathBase section
    /// </summary>
    public static class MathBase
    {
        #region Constants

        /// <summary>
        /// Epsilon for floating-point comparisons and safe operations.
        /// Used to avoid division by zero and determine equality within tolerance.
        /// </summary>
        public const double EPS = 1e-12;

        /// <summary>
        /// NaN constant for clarity in return statements
        /// </summary>
        public static readonly double NaN = double.NaN;

        #endregion

        #region Price Shortcuts

        /// <summary>
        /// High-Low midpoint: (H + L) / 2
        /// Common price reference for many indicators.
        /// </summary>
        /// <param name="high">Bar high price</param>
        /// <param name="low">Bar low price</param>
        /// <returns>Midpoint or NaN if inputs invalid</returns>
        public static double HL2(double high, double low)
        {
            if (!IsFinite(high) || !IsFinite(low))
                return NaN;
            return (high + low) * 0.5;
        }

        /// <summary>
        /// Typical price: (H + L + C) / 3
        /// Weighted average favoring close slightly less than OHLC4.
        /// </summary>
        public static double HLC3(double high, double low, double close)
        {
            if (!IsFinite(high) || !IsFinite(low) || !IsFinite(close))
                return NaN;
            return (high + low + close) / 3.0;
        }

        /// <summary>
        /// Average price: (O + H + L + C) / 4
        /// Most comprehensive average of bar components.
        /// </summary>
        public static double OHLC4(double open, double high, double low, double close)
        {
            if (!IsFinite(open) || !IsFinite(high) || !IsFinite(low) || !IsFinite(close))
                return NaN;
            return (open + high + low + close) * 0.25;
        }

        #endregion

        #region Safe Operations

        /// <summary>
        /// Safe division that returns NaN on invalid denominator.
        /// Prevents division by zero and propagates NaN inputs.
        /// </summary>
        /// <param name="numerator">Dividend</param>
        /// <param name="denominator">Divisor</param>
        /// <returns>numerator / denominator or NaN if denominator near zero or invalid</returns>
        public static double SafeDivide(double numerator, double denominator)
        {
            if (!IsFinite(numerator) || !IsFinite(denominator))
                return NaN;
            if (Math.Abs(denominator) < EPS)
                return NaN;
            return numerator / denominator;
        }

        /// <summary>
        /// Safe division with fallback value on invalid operation.
        /// Useful when a specific default is preferred over NaN.
        /// </summary>
        /// <param name="numerator">Dividend</param>
        /// <param name="denominator">Divisor</param>
        /// <param name="defaultValue">Return value if division invalid</param>
        /// <returns>numerator / denominator or defaultValue</returns>
        public static double SafeDivideOrDefault(double numerator, double denominator, double defaultValue)
        {
            var result = SafeDivide(numerator, denominator);
            return double.IsNaN(result) ? defaultValue : result;
        }

        /// <summary>
        /// Safe square root that returns NaN for negative inputs.
        /// </summary>
        public static double SafeSqrt(double x)
        {
            if (!IsFinite(x) || x < 0)
                return NaN;
            return Math.Sqrt(x);
        }

        /// <summary>
        /// Safe natural logarithm that returns NaN for non-positive inputs.
        /// </summary>
        public static double SafeLog(double x)
        {
            if (!IsFinite(x) || x <= 0)
                return NaN;
            return Math.Log(x);
        }

        /// <summary>
        /// Safe base-10 logarithm that returns NaN for non-positive inputs.
        /// </summary>
        public static double SafeLog10(double x)
        {
            if (!IsFinite(x) || x <= 0)
                return NaN;
            return Math.Log10(x);
        }

        #endregion

        #region Comparison & Validation

        /// <summary>
        /// Checks if value is finite (not NaN, not ±Infinity).
        /// Essential pre-condition for most calculations.
        /// </summary>
        public static bool IsFinite(double x)
        {
            return !double.IsNaN(x) && !double.IsInfinity(x);
        }

        /// <summary>
        /// Tolerance-based equality comparison.
        /// Two values are equal if their absolute difference is within epsilon.
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <param name="epsilon">Tolerance (default: EPS)</param>
        /// <returns>True if values are within epsilon of each other</returns>
        public static bool AlmostEqual(double a, double b, double epsilon = EPS)
        {
            if (!IsFinite(a) || !IsFinite(b))
                return double.IsNaN(a) && double.IsNaN(b); // Both NaN = equal
            return Math.Abs(a - b) <= epsilon;
        }

        /// <summary>
        /// Clamps value to specified range [lo, hi].
        /// </summary>
        public static double Clamp(double x, double lo, double hi)
        {
            if (!IsFinite(x))
                return NaN;
            if (x < lo) return lo;
            if (x > hi) return hi;
            return x;
        }

        /// <summary>
        /// Clamps value to [0, 1] range.
        /// Common for normalized indicators.
        /// </summary>
        public static double Bound01(double x)
        {
            return Clamp(x, 0.0, 1.0);
        }

        #endregion

        #region Normalization & Rounding

        /// <summary>
        /// Rounding modes for deterministic cross-platform results.
        /// </summary>
        public enum RoundMode
        {
            /// <summary>Rounds to nearest, ties away from zero (0.5 → 1, -0.5 → -1)</summary>
            HalfAwayFromZero,
            /// <summary>Rounds to nearest, ties to even (banker's rounding)</summary>
            HalfToEven,
            /// <summary>Truncates toward zero (floor of abs value with original sign)</summary>
            Truncate,
            /// <summary>Rounds toward negative infinity</summary>
            Floor,
            /// <summary>Rounds toward positive infinity</summary>
            Ceiling
        }

        /// <summary>
        /// Rounds value to specified number of decimal places using given mode.
        /// Platform-independent implementation ensuring identical results.
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <param name="digits">Number of decimal places (0-15)</param>
        /// <param name="mode">Rounding mode</param>
        /// <returns>Rounded value or NaN if input invalid</returns>
        public static double Round(double value, int digits, RoundMode mode = RoundMode.HalfAwayFromZero)
        {
            if (!IsFinite(value))
                return NaN;

            if (digits < 0 || digits > 15)
                throw new ArgumentOutOfRangeException(nameof(digits), "Digits must be 0-15");

            double multiplier = Math.Pow(10, digits);
            double scaled = value * multiplier;

            double rounded;
            switch (mode)
            {
                case RoundMode.HalfAwayFromZero:
                    rounded = Math.Round(scaled, MidpointRounding.AwayFromZero);
                    break;
                case RoundMode.HalfToEven:
                    rounded = Math.Round(scaled, MidpointRounding.ToEven);
                    break;
                case RoundMode.Truncate:
                    rounded = Math.Truncate(scaled);
                    break;
                case RoundMode.Floor:
                    rounded = Math.Floor(scaled);
                    break;
                case RoundMode.Ceiling:
                    rounded = Math.Ceiling(scaled);
                    break;
                default:
                    throw new ArgumentException($"Unknown rounding mode: {mode}");
            }

            return rounded / multiplier;
        }

        /// <summary>
        /// Normalizes double to specified precision (alias for Round with HalfAwayFromZero).
        /// Common in trading platforms for price normalization.
        /// </summary>
        public static double NormalizeDouble(double value, int digits, RoundMode mode = RoundMode.HalfAwayFromZero)
        {
            return Round(value, digits, mode);
        }

        /// <summary>
        /// Quantizes value to discrete steps.
        /// Example: Quantize(12.7, 5.0) → 15.0 (nearest multiple of 5)
        /// </summary>
        /// <param name="value">Value to quantize</param>
        /// <param name="step">Step size (must be positive)</param>
        /// <param name="mode">Rounding mode for snapping</param>
        /// <returns>Value snapped to nearest step or NaN</returns>
        public static double Quantize(double value, double step, RoundMode mode = RoundMode.HalfAwayFromZero)
        {
            if (!IsFinite(value) || !IsFinite(step))
                return NaN;
            if (step <= 0)
                throw new ArgumentException("Step must be positive", nameof(step));

            double scaled = value / step;
            double rounded;

            switch (mode)
            {
                case RoundMode.HalfAwayFromZero:
                    rounded = Math.Round(scaled, MidpointRounding.AwayFromZero);
                    break;
                case RoundMode.HalfToEven:
                    rounded = Math.Round(scaled, MidpointRounding.ToEven);
                    break;
                case RoundMode.Truncate:
                    rounded = Math.Truncate(scaled);
                    break;
                case RoundMode.Floor:
                    rounded = Math.Floor(scaled);
                    break;
                case RoundMode.Ceiling:
                    rounded = Math.Ceiling(scaled);
                    break;
                default:
                    throw new ArgumentException($"Unknown rounding mode: {mode}");
            }

            return rounded * step;
        }

        /// <summary>
        /// Rounds price to valid tick size.
        /// Essential for order placement to avoid rejection.
        /// </summary>
        /// <param name="price">Price to round</param>
        /// <param name="tickSize">Minimum price increment</param>
        /// <param name="mode">Rounding mode</param>
        /// <returns>Price rounded to tick size or NaN</returns>
        public static double RoundToTick(double price, double tickSize, RoundMode mode = RoundMode.HalfAwayFromZero)
        {
            return Quantize(price, tickSize, mode);
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Calculates arithmetic mean of array segment.
        /// Returns NaN if any value in window is NaN (no NaN-skipping).
        /// </summary>
        /// <param name="values">Source array</param>
        /// <param name="startIndex">Start index (inclusive)</param>
        /// <param name="period">Window length</param>
        /// <returns>Mean or NaN if window invalid or contains NaN</returns>
        public static double Mean(double[] values, int startIndex, int period)
        {
            if (values == null || period <= 0)
                return NaN;
            if (startIndex < 0 || startIndex + period > values.Length)
                return NaN;

            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                double val = values[startIndex + i];
                if (!IsFinite(val))
                    return NaN; // Strict NaN propagation
                sum += val;
            }

            return sum / period;
        }

        /// <summary>
        /// Calculates mean over most recent 'period' values (convenience overload).
        /// </summary>
        public static double Mean(double[] values, int period)
        {
            if (values == null || values.Length < period)
                return NaN;
            return Mean(values, values.Length - period, period);
        }

        /// <summary>
        /// Calculates variance of array segment.
        /// </summary>
        /// <param name="values">Source array</param>
        /// <param name="startIndex">Start index</param>
        /// <param name="period">Window length</param>
        /// <param name="sample">If true, use sample variance (n-1), else population (n)</param>
        /// <returns>Variance or NaN</returns>
        public static double Variance(double[] values, int startIndex, int period, bool sample = true)
        {
            if (values == null || period <= 0)
                return NaN;
            if (sample && period < 2)
                return NaN; // Sample variance requires at least 2 points
            if (startIndex < 0 || startIndex + period > values.Length)
                return NaN;

            double mean = Mean(values, startIndex, period);
            if (!IsFinite(mean))
                return NaN;

            double sumSquares = 0;
            for (int i = 0; i < period; i++)
            {
                double val = values[startIndex + i];
                if (!IsFinite(val))
                    return NaN;
                double diff = val - mean;
                sumSquares += diff * diff;
            }

            int divisor = sample ? period - 1 : period;
            return sumSquares / divisor;
        }

        /// <summary>
        /// Variance over most recent period (convenience overload).
        /// </summary>
        public static double Variance(double[] values, int period, bool sample = true)
        {
            if (values == null || values.Length < period)
                return NaN;
            return Variance(values, values.Length - period, period, sample);
        }

        /// <summary>
        /// Calculates standard deviation (square root of variance).
        /// </summary>
        public static double StdDev(double[] values, int startIndex, int period, bool sample = true)
        {
            double variance = Variance(values, startIndex, period, sample);
            return SafeSqrt(variance);
        }

        /// <summary>
        /// Standard deviation over most recent period (convenience overload).
        /// </summary>
        public static double StdDev(double[] values, int period, bool sample = true)
        {
            if (values == null || values.Length < period)
                return NaN;
            return StdDev(values, values.Length - period, period, sample);
        }

        /// <summary>
        /// Calculates covariance between two series over same window.
        /// Both arrays must have valid values throughout the window.
        /// </summary>
        /// <param name="x">First series</param>
        /// <param name="y">Second series</param>
        /// <param name="startIndex">Start index in both arrays</param>
        /// <param name="period">Window length</param>
        /// <param name="sample">Sample (n-1) vs population (n) denominator</param>
        /// <returns>Covariance or NaN</returns>
        public static double Covariance(double[] x, double[] y, int startIndex, int period, bool sample = true)
        {
            if (x == null || y == null || period <= 0)
                return NaN;
            if (sample && period < 2)
                return NaN;
            if (startIndex < 0 || startIndex + period > x.Length || startIndex + period > y.Length)
                return NaN;

            double meanX = Mean(x, startIndex, period);
            double meanY = Mean(y, startIndex, period);
            if (!IsFinite(meanX) || !IsFinite(meanY))
                return NaN;

            double sumProduct = 0;
            for (int i = 0; i < period; i++)
            {
                double valX = x[startIndex + i];
                double valY = y[startIndex + i];
                if (!IsFinite(valX) || !IsFinite(valY))
                    return NaN;
                sumProduct += (valX - meanX) * (valY - meanY);
            }

            int divisor = sample ? period - 1 : period;
            return sumProduct / divisor;
        }

        /// <summary>
        /// Covariance over most recent period (convenience overload).
        /// </summary>
        public static double Covariance(double[] x, double[] y, int period, bool sample = true)
        {
            if (x == null || y == null || x.Length < period || y.Length < period)
                return NaN;
            int startIndex = Math.Min(x.Length, y.Length) - period;
            return Covariance(x, y, startIndex, period, sample);
        }

        /// <summary>
        /// Pearson correlation coefficient between two series.
        /// Returns value in [-1, 1] or NaN if calculation invalid.
        /// </summary>
        public static double Correlation(double[] x, double[] y, int startIndex, int period)
        {
            if (x == null || y == null || period <= 0)
                return NaN;
            if (startIndex < 0 || startIndex + period > x.Length || startIndex + period > y.Length)
                return NaN;

            double cov = Covariance(x, y, startIndex, period, true);
            double stdX = StdDev(x, startIndex, period, true);
            double stdY = StdDev(y, startIndex, period, true);

            return SafeDivide(cov, stdX * stdY);
        }

        /// <summary>
        /// Correlation over most recent period (convenience overload).
        /// </summary>
        public static double Correlation(double[] x, double[] y, int period)
        {
            if (x == null || y == null || x.Length < period || y.Length < period)
                return NaN;
            int startIndex = Math.Min(x.Length, y.Length) - period;
            return Correlation(x, y, startIndex, period);
        }

        /// <summary>
        /// Simple sum over window (no NaN skipping).
        /// </summary>
        public static double Sum(double[] values, int startIndex, int period)
        {
            if (values == null || period <= 0)
                return NaN;
            if (startIndex < 0 || startIndex + period > values.Length)
                return NaN;

            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                double val = values[startIndex + i];
                if (!IsFinite(val))
                    return NaN;
                sum += val;
            }
            return sum;
        }

        /// <summary>
        /// Sum over most recent period (convenience overload).
        /// </summary>
        public static double Sum(double[] values, int period)
        {
            if (values == null || values.Length < period)
                return NaN;
            return Sum(values, values.Length - period, period);
        }

        #endregion

        #region Affine Transforms & Helpers

        /// <summary>
        /// Linear interpolation: a + t*(b-a)
        /// Returns value between a and b based on parameter t ∈ [0,1].
        /// </summary>
        public static double Lerp(double a, double b, double t)
        {
            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(t))
                return NaN;
            return a + t * (b - a);
        }

        /// <summary>
        /// Inverse lerp: finds t such that x = Lerp(a, b, t).
        /// Returns how far x is between a and b as fraction.
        /// </summary>
        public static double Unlerp(double a, double b, double x)
        {
            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(x))
                return NaN;
            if (AlmostEqual(a, b))
                return NaN; // Undefined when a == b
            return (x - a) / (b - a);
        }

        /// <summary>
        /// Maps value from input range [inLo, inHi] to output range [outLo, outHi].
        /// Optionally clamps to output range.
        /// </summary>
        public static double MapToRange(double x, double inLo, double inHi, double outLo, double outHi, bool clamp = false)
        {
            if (!IsFinite(x) || !IsFinite(inLo) || !IsFinite(inHi) || !IsFinite(outLo) || !IsFinite(outHi))
                return NaN;

            double t = Unlerp(inLo, inHi, x);
            if (!IsFinite(t))
                return NaN;

            double result = Lerp(outLo, outHi, t);
            
            if (clamp)
            {
                double min = Math.Min(outLo, outHi);
                double max = Math.Max(outLo, outHi);
                result = Clamp(result, min, max);
            }

            return result;
        }

        /// <summary>
        /// Returns sign of value with EPS tolerance.
        /// Returns +1 if x > EPS, -1 if x < -EPS, 0 otherwise.
        /// </summary>
        public static int Sign(double x)
        {
            if (!IsFinite(x))
                return 0;
            if (x > EPS) return 1;
            if (x < -EPS) return -1;
            return 0;
        }

        /// <summary>
        /// Calculates percent change: (current - previous) / previous * 100
        /// Safe division handles zero previous values.
        /// </summary>
        public static double PercentChange(double current, double previous)
        {
            if (!IsFinite(current) || !IsFinite(previous))
                return NaN;
            if (Math.Abs(previous) < EPS)
                return NaN;
            return ((current - previous) / previous) * 100.0;
        }

        #endregion

        #region Array Utilities

        /// <summary>
        /// Creates array filled with specified value.
        /// Useful for initializing buffers with NaN or zero.
        /// </summary>
        public static double[] Fill(int length, double value)
        {
            if (length < 0)
                throw new ArgumentException("Length must be non-negative", nameof(length));

            double[] result = new double[length];
            for (int i = 0; i < length; i++)
                result[i] = value;
            return result;
        }

        /// <summary>
        /// Creates array filled with NaN.
        /// </summary>
        public static double[] FillNaN(int length)
        {
            return Fill(length, NaN);
        }

        /// <summary>
        /// Checks if all values in array are finite (no NaN, no Infinity).
        /// </summary>
        public static bool AllFinite(double[] values)
        {
            if (values == null)
                return false;
            return values.All(IsFinite);
        }

        #endregion
    }
}
