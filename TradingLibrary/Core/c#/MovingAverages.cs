using System;
using System.Collections.Generic;

namespace TradingLibrary.Core
{
    /// <summary>
    /// Comprehensive moving average implementations with batch and stateful (streaming) variants.
    /// All algorithms are deterministic and cross-platform identical.
    /// Reference: SharedLibrary/Core specification - MovingAverages section
    /// 
    /// Key principles:
    /// - Indexing: 0 → oldest, N-1 → newest (chronological)
    /// - NaN propagation: any NaN in required window → NaN output
    /// - Warm-up: defined per type; first valid output after complete window
    /// - Seed modes: SMA, FirstValue, Zero, NaN (default: SMA)
    /// - Batch vs Stateful: must produce identical results (±1e-12 tolerance)
    /// </summary>
    public static class MovingAverages
    {
        #region Enums

        /// <summary>
        /// Seed initialization modes for exponential moving averages.
        /// Affects first valid value calculation.
        /// </summary>
        public enum SeedMode
        {
            /// <summary>Use SMA of first P values as seed (recommended for determinism)</summary>
            SmaSeed,
            /// <summary>Use first value directly as seed (faster warm-up)</summary>
            FirstValueSeed,
            /// <summary>Start from zero (may cause initial bias)</summary>
            ZeroSeed,
            /// <summary>Return NaN until full SMA period available</summary>
            NaNSeed
        }

        #endregion

        #region SMA (Simple Moving Average)

        /// <summary>
        /// Simple Moving Average - arithmetic mean over period.
        /// Warm-up: period bars
        /// </summary>
        /// <param name="src">Source values (chronological: 0 → oldest)</param>
        /// <param name="period">Window length (≥1)</param>
        /// <returns>Array of SMA values (NaN before warm-up)</returns>
        public static double[] SMA(double[] src, int period)
        {
            if (src == null || period < 1)
                return null;

            int n = src.Length;
            double[] result = new double[n];

            // Initialize with NaN
            for (int i = 0; i < Math.Min(period - 1, n); i++)
                result[i] = MathBase.NaN;

            if (n < period)
                return result;

            // First SMA using full period
            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                if (!MathBase.IsFinite(src[i]))
                {
                    result[period - 1] = MathBase.NaN;
                    return result; // Strict NaN propagation
                }
                sum += src[i];
            }
            result[period - 1] = sum / period;

            // Rolling SMA: subtract oldest, add newest
            for (int i = period; i < n; i++)
            {
                if (!MathBase.IsFinite(src[i]))
                {
                    // Fill remaining with NaN
                    for (int j = i; j < n; j++)
                        result[j] = MathBase.NaN;
                    return result;
                }

                sum = sum - src[i - period] + src[i];
                result[i] = sum / period;
            }

            return result;
        }

        /// <summary>
        /// Stateful SMA for streaming/incremental updates.
        /// Maintains ring buffer of last P values.
        /// </summary>
        public class SMAState
        {
            private readonly int _period;
            private readonly double[] _buffer;
            private int _head = 0;
            private int _count = 0;
            private double _sum = 0;

            public int Period => _period;
            public double Value { get; private set; } = MathBase.NaN;
            public int WarmupLeft => Math.Max(0, _period - _count);
            public bool IsWarmedUp => _count >= _period;

            public SMAState(int period)
            {
                if (period < 1)
                    throw new ArgumentException("Period must be ≥ 1", nameof(period));
                _period = period;
                _buffer = new double[period];
            }

            /// <summary>
            /// Updates SMA with new value.
            /// </summary>
            public double Update(double value)
            {
                if (!MathBase.IsFinite(value))
                {
                    Value = MathBase.NaN;
                    return Value;
                }

                if (_count < _period)
                {
                    // Filling initial buffer
                    _buffer[_count] = value;
                    _sum += value;
                    _count++;

                    if (_count == _period)
                    {
                        Value = _sum / _period;
                    }
                }
                else
                {
                    // Rolling update
                    _sum = _sum - _buffer[_head] + value;
                    _buffer[_head] = value;
                    _head = (_head + 1) % _period;
                    Value = _sum / _period;
                }

                return Value;
            }

            public void Reset()
            {
                _head = 0;
                _count = 0;
                _sum = 0;
                Value = MathBase.NaN;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }

        #endregion

        #region EMA (Exponential Moving Average)

        /// <summary>
        /// Exponential Moving Average with configurable seed.
        /// α = 2 / (period + 1)
        /// EMA[t] = α * src[t] + (1-α) * EMA[t-1]
        /// Warm-up: period bars (with SmaSeed)
        /// </summary>
        public static double[] EMA(double[] src, int period, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1)
                return null;

            double alpha = 2.0 / (period + 1);
            return EMAFromAlpha(src, alpha, period, seed);
        }

        /// <summary>
        /// EMA using explicit alpha value.
        /// Useful for exact replication of specific decay rates.
        /// </summary>
        public static double[] EMAFromAlpha(double[] src, double alpha, int warmupPeriod, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || alpha <= 0 || alpha > 1)
                return null;

            int n = src.Length;
            double[] result = new double[n];
            double ema = MathBase.NaN;

            int seedIndex = -1;
            switch (seed)
            {
                case SeedMode.SmaSeed:
                    if (n < warmupPeriod)
                    {
                        return MathBase.FillNaN(n);
                    }
                    // Calculate SMA seed
                    double sum = 0;
                    for (int i = 0; i < warmupPeriod; i++)
                    {
                        if (!MathBase.IsFinite(src[i]))
                            return MathBase.FillNaN(n);
                        sum += src[i];
                    }
                    ema = sum / warmupPeriod;
                    seedIndex = warmupPeriod - 1;
                    break;

                case SeedMode.FirstValueSeed:
                    if (n < 1 || !MathBase.IsFinite(src[0]))
                        return MathBase.FillNaN(n);
                    ema = src[0];
                    seedIndex = 0;
                    break;

                case SeedMode.ZeroSeed:
                    ema = 0;
                    seedIndex = 0;
                    break;

                case SeedMode.NaNSeed:
                    if (n < warmupPeriod)
                        return MathBase.FillNaN(n);
                    seedIndex = warmupPeriod - 1;
                    ema = MathBase.NaN;
                    break;
            }

            // Fill NaN before seed
            for (int i = 0; i < seedIndex; i++)
                result[i] = MathBase.NaN;

            // Seed value
            if (seedIndex >= 0)
                result[seedIndex] = ema;

            // EMA calculation
            for (int i = seedIndex + 1; i < n; i++)
            {
                if (!MathBase.IsFinite(src[i]) || !MathBase.IsFinite(ema))
                {
                    for (int j = i; j < n; j++)
                        result[j] = MathBase.NaN;
                    return result;
                }

                ema = alpha * src[i] + (1 - alpha) * ema;
                result[i] = ema;
            }

            return result;
        }

        /// <summary>
        /// EMA from half-life (time for value to decay to 50%).
        /// α = 1 - exp(-ln(2) / halfLife)
        /// </summary>
        public static double[] EMAFromHalfLife(double[] src, double halfLife, SeedMode seed = SeedMode.SmaSeed)
        {
            if (halfLife <= 0)
                return null;
            double alpha = 1.0 - Math.Exp(-Math.Log(2.0) / halfLife);
            int warmup = (int)Math.Ceiling(halfLife);
            return EMAFromAlpha(src, alpha, warmup, seed);
        }

        /// <summary>
        /// EMA from time constant (tau).
        /// α = 1 - exp(-1 / tau)
        /// </summary>
        public static double[] EMAFromTau(double[] src, double tau, SeedMode seed = SeedMode.SmaSeed)
        {
            if (tau <= 0)
                return null;
            double alpha = 1.0 - Math.Exp(-1.0 / tau);
            int warmup = (int)Math.Ceiling(tau);
            return EMAFromAlpha(src, alpha, warmup, seed);
        }

        /// <summary>
        /// Stateful EMA for streaming updates.
        /// </summary>
        public class EMAState
        {
            private readonly double _alpha;
            private readonly int _period;
            private readonly SeedMode _seedMode;
            private double _ema = MathBase.NaN;
            private int _count = 0;
            private double _seedSum = 0;

            public int Period => _period;
            public double Value => _ema;
            public int WarmupLeft => _seedMode == SeedMode.SmaSeed ? Math.Max(0, _period - _count) : 0;
            public bool IsWarmedUp => _count >= _period;

            public EMAState(int period, SeedMode seed = SeedMode.SmaSeed)
            {
                if (period < 1)
                    throw new ArgumentException("Period must be ≥ 1", nameof(period));
                _period = period;
                _alpha = 2.0 / (period + 1);
                _seedMode = seed;
            }

            public EMAState(double alpha, int warmupPeriod, SeedMode seed = SeedMode.SmaSeed)
            {
                if (alpha <= 0 || alpha > 1)
                    throw new ArgumentException("Alpha must be (0, 1]", nameof(alpha));
                _alpha = alpha;
                _period = warmupPeriod;
                _seedMode = seed;
            }

            public double Update(double value)
            {
                if (!MathBase.IsFinite(value))
                {
                    _ema = MathBase.NaN;
                    return _ema;
                }

                switch (_seedMode)
                {
                    case SeedMode.SmaSeed:
                        if (_count < _period)
                        {
                            _seedSum += value;
                            _count++;
                            if (_count == _period)
                                _ema = _seedSum / _period;
                            return _ema;
                        }
                        break;

                    case SeedMode.FirstValueSeed:
                        if (_count == 0)
                        {
                            _ema = value;
                            _count = 1;
                            return _ema;
                        }
                        break;

                    case SeedMode.ZeroSeed:
                        if (_count == 0)
                        {
                            _ema = 0;
                            _count = 1;
                        }
                        break;
                }

                if (MathBase.IsFinite(_ema))
                {
                    _ema = _alpha * value + (1 - _alpha) * _ema;
                    _count++;
                }

                return _ema;
            }

            public void Reset()
            {
                _ema = MathBase.NaN;
                _count = 0;
                _seedSum = 0;
            }
        }

        #endregion

        #region RMA (Wilder's Moving Average / SMMA)

        /// <summary>
        /// Wilder's Moving Average (also called Smoothed MA).
        /// α = 1 / period (slower decay than EMA)
        /// Used in: ATR, ADX, RSI (Wilder mode)
        /// Warm-up: period bars
        /// </summary>
        public static double[] RMA(double[] src, int period, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1)
                return null;

            double alpha = 1.0 / period;
            return EMAFromAlpha(src, alpha, period, seed);
        }

        /// <summary>
        /// Stateful RMA for streaming updates.
        /// </summary>
        public class RMAState
        {
            private readonly EMAState _emaState;

            public int Period => _emaState.Period;
            public double Value => _emaState.Value;
            public int WarmupLeft => _emaState.WarmupLeft;
            public bool IsWarmedUp => _emaState.IsWarmedUp;

            public RMAState(int period, SeedMode seed = SeedMode.SmaSeed)
            {
                if (period < 1)
                    throw new ArgumentException("Period must be ≥ 1", nameof(period));
                double alpha = 1.0 / period;
                _emaState = new EMAState(alpha, period, seed);
            }

            public double Update(double value) => _emaState.Update(value);
            public void Reset() => _emaState.Reset();
        }

        #endregion

        #region WMA (Weighted Moving Average)

        /// <summary>
        /// Weighted Moving Average with linear weights 1, 2, ..., P.
        /// More responsive to recent changes than SMA.
        /// Warm-up: period bars
        /// </summary>
        public static double[] WMA(double[] src, int period)
        {
            if (src == null || period < 1)
                return null;

            int n = src.Length;
            double[] result = new double[n];

            // Fill NaN before warm-up
            for (int i = 0; i < Math.Min(period - 1, n); i++)
                result[i] = MathBase.NaN;

            if (n < period)
                return result;

            // Weight sum: 1 + 2 + ... + P = P*(P+1)/2
            double weightSum = period * (period + 1) / 2.0;

            for (int i = period - 1; i < n; i++)
            {
                double wma = 0;
                int weight = 1;

                for (int j = i - period + 1; j <= i; j++)
                {
                    if (!MathBase.IsFinite(src[j]))
                    {
                        wma = MathBase.NaN;
                        break;
                    }
                    wma += src[j] * weight;
                    weight++;
                }

                result[i] = MathBase.IsFinite(wma) ? wma / weightSum : MathBase.NaN;
            }

            return result;
        }

        /// <summary>
        /// Stateful WMA - maintains ring buffer and rolling weighted sum.
        /// </summary>
        public class WMAState
        {
            private readonly int _period;
            private readonly double[] _buffer;
            private readonly double _weightSum;
            private int _head = 0;
            private int _count = 0;

            public int Period => _period;
            public double Value { get; private set; } = MathBase.NaN;
            public int WarmupLeft => Math.Max(0, _period - _count);
            public bool IsWarmedUp => _count >= _period;

            public WMAState(int period)
            {
                if (period < 1)
                    throw new ArgumentException("Period must be ≥ 1", nameof(period));
                _period = period;
                _buffer = new double[period];
                _weightSum = period * (period + 1) / 2.0;
            }

            public double Update(double value)
            {
                if (!MathBase.IsFinite(value))
                {
                    Value = MathBase.NaN;
                    return Value;
                }

                _buffer[_head] = value;
                _head = (_head + 1) % _period;
                if (_count < _period)
                    _count++;

                if (_count < _period)
                {
                    Value = MathBase.NaN;
                    return Value;
                }

                // Calculate WMA
                double wma = 0;
                int weight = 1;
                for (int i = 0; i < _period; i++)
                {
                    int idx = (_head + i) % _period;
                    wma += _buffer[idx] * weight;
                    weight++;
                }

                Value = wma / _weightSum;
                return Value;
            }

            public void Reset()
            {
                _head = 0;
                _count = 0;
                Value = MathBase.NaN;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }

        #endregion

        #region HMA (Hull Moving Average)

        /// <summary>
        /// Hull Moving Average - extremely responsive with minimal lag.
        /// Formula: WMA(2*WMA(P/2) - WMA(P), √P)
        /// Warm-up: max(P, P/2) + √P - 1
        /// </summary>
        public static double[] HMA(double[] src, int period)
        {
            if (src == null || period < 1)
                return null;

            int halfPeriod = (int)Math.Ceiling(period / 2.0);
            int sqrtPeriod = (int)Math.Ceiling(Math.Sqrt(period));

            double[] wmaHalf = WMA(src, halfPeriod);
            double[] wmaFull = WMA(src, period);

            if (wmaHalf == null || wmaFull == null)
                return null;

            int n = src.Length;
            double[] diff = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (MathBase.IsFinite(wmaHalf[i]) && MathBase.IsFinite(wmaFull[i]))
                    diff[i] = 2 * wmaHalf[i] - wmaFull[i];
                else
                    diff[i] = MathBase.NaN;
            }

            return WMA(diff, sqrtPeriod);
        }

        /// <summary>
        /// Stateful HMA - combines three WMA states.
        /// </summary>
        public class HMAState
        {
            private readonly WMAState _wmaHalf;
            private readonly WMAState _wmaFull;
            private readonly WMAState _wmaFinal;
            private readonly int _period;

            public int Period => _period;
            public double Value { get; private set; } = MathBase.NaN;
            public bool IsWarmedUp => _wmaFull.IsWarmedUp && _wmaFinal.IsWarmedUp;

            public HMAState(int period)
            {
                if (period < 1)
                    throw new ArgumentException("Period must be ≥ 1", nameof(period));
                _period = period;
                int halfPeriod = (int)Math.Ceiling(period / 2.0);
                int sqrtPeriod = (int)Math.Ceiling(Math.Sqrt(period));

                _wmaHalf = new WMAState(halfPeriod);
                _wmaFull = new WMAState(period);
                _wmaFinal = new WMAState(sqrtPeriod);
            }

            public double Update(double value)
            {
                double half = _wmaHalf.Update(value);
                double full = _wmaFull.Update(value);

                if (MathBase.IsFinite(half) && MathBase.IsFinite(full))
                {
                    double diff = 2 * half - full;
                    Value = _wmaFinal.Update(diff);
                }
                else
                {
                    Value = MathBase.NaN;
                }

                return Value;
            }

            public void Reset()
            {
                _wmaHalf.Reset();
                _wmaFull.Reset();
                _wmaFinal.Reset();
                Value = MathBase.NaN;
            }
        }

        #endregion

        #region DEMA (Double Exponential Moving Average)

        /// <summary>
        /// Double Exponential Moving Average.
        /// Formula: 2*EMA(P) - EMA(EMA(P))
        /// Reduces lag compared to single EMA.
        /// Warm-up: 2*P - 1
        /// </summary>
        public static double[] DEMA(double[] src, int period, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1)
                return null;

            double[] ema1 = EMA(src, period, seed);
            double[] ema2 = EMA(ema1, period, seed);

            int n = src.Length;
            double[] result = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (MathBase.IsFinite(ema1[i]) && MathBase.IsFinite(ema2[i]))
                    result[i] = 2 * ema1[i] - ema2[i];
                else
                    result[i] = MathBase.NaN;
            }

            return result;
        }

        /// <summary>
        /// Stateful DEMA combining two EMA states.
        /// </summary>
        public class DEMAState
        {
            private readonly EMAState _ema1;
            private readonly EMAState _ema2;

            public int Period => _ema1.Period;
            public double Value { get; private set; } = MathBase.NaN;
            public bool IsWarmedUp => _ema2.IsWarmedUp;

            public DEMAState(int period, SeedMode seed = SeedMode.SmaSeed)
            {
                _ema1 = new EMAState(period, seed);
                _ema2 = new EMAState(period, seed);
            }

            public double Update(double value)
            {
                double e1 = _ema1.Update(value);
                double e2 = _ema2.Update(e1);

                if (MathBase.IsFinite(e1) && MathBase.IsFinite(e2))
                    Value = 2 * e1 - e2;
                else
                    Value = MathBase.NaN;

                return Value;
            }

            public void Reset()
            {
                _ema1.Reset();
                _ema2.Reset();
                Value = MathBase.NaN;
            }
        }

        #endregion

        #region TEMA (Triple Exponential Moving Average)

        /// <summary>
        /// Triple Exponential Moving Average.
        /// Formula: 3*EMA(P) - 3*EMA(EMA(P)) + EMA(EMA(EMA(P)))
        /// Even more lag reduction than DEMA.
        /// Warm-up: 3*P - 2
        /// </summary>
        public static double[] TEMA(double[] src, int period, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1)
                return null;

            double[] ema1 = EMA(src, period, seed);
            double[] ema2 = EMA(ema1, period, seed);
            double[] ema3 = EMA(ema2, period, seed);

            int n = src.Length;
            double[] result = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (MathBase.IsFinite(ema1[i]) && MathBase.IsFinite(ema2[i]) && MathBase.IsFinite(ema3[i]))
                    result[i] = 3 * ema1[i] - 3 * ema2[i] + ema3[i];
                else
                    result[i] = MathBase.NaN;
            }

            return result;
        }

        /// <summary>
        /// Stateful TEMA combining three EMA states.
        /// </summary>
        public class TEMAState
        {
            private readonly EMAState _ema1;
            private readonly EMAState _ema2;
            private readonly EMAState _ema3;

            public int Period => _ema1.Period;
            public double Value { get; private set; } = MathBase.NaN;
            public bool IsWarmedUp => _ema3.IsWarmedUp;

            public TEMAState(int period, SeedMode seed = SeedMode.SmaSeed)
            {
                _ema1 = new EMAState(period, seed);
                _ema2 = new EMAState(period, seed);
                _ema3 = new EMAState(period, seed);
            }

            public double Update(double value)
            {
                double e1 = _ema1.Update(value);
                double e2 = _ema2.Update(e1);
                double e3 = _ema3.Update(e2);

                if (MathBase.IsFinite(e1) && MathBase.IsFinite(e2) && MathBase.IsFinite(e3))
                    Value = 3 * e1 - 3 * e2 + e3;
                else
                    Value = MathBase.NaN;

                return Value;
            }

            public void Reset()
            {
                _ema1.Reset();
                _ema2.Reset();
                _ema3.Reset();
                Value = MathBase.NaN;
            }
        }

        #endregion

        #region ZLEMA (Zero-Lag Exponential Moving Average)

        /// <summary>
        /// Zero-Lag EMA - adjusts input by lag before applying EMA.
        /// lag = ⌊(period - 1) / 2⌋
        /// Adjusted input: src[i] + (src[i] - src[i - lag])
        /// Warm-up: period + lag
        /// </summary>
        public static double[] ZLEMA(double[] src, int period, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1)
                return null;

            int n = src.Length;
            int lag = (period - 1) / 2;

            if (n < lag + 1)
                return MathBase.FillNaN(n);

            double[] adjusted = new double[n];

            // Fill initial values with NaN (can't calculate before lag)
            for (int i = 0; i < lag; i++)
                adjusted[i] = MathBase.NaN;

            // Calculate adjusted input
            for (int i = lag; i < n; i++)
            {
                if (!MathBase.IsFinite(src[i]) || !MathBase.IsFinite(src[i - lag]))
                    adjusted[i] = MathBase.NaN;
                else
                    adjusted[i] = src[i] + (src[i] - src[i - lag]);
            }

            return EMA(adjusted, period, seed);
        }

        /// <summary>
        /// Stateful ZLEMA - maintains lag buffer and EMA state.
        /// </summary>
        public class ZLEMAState
        {
            private readonly int _period;
            private readonly int _lag;
            private readonly EMAState _ema;
            private readonly Queue<double> _lagBuffer;

            public int Period => _period;
            public double Value => _ema.Value;
            public bool IsWarmedUp => _lagBuffer.Count > _lag && _ema.IsWarmedUp;

            public ZLEMAState(int period, SeedMode seed = SeedMode.SmaSeed)
            {
                if (period < 1)
                    throw new ArgumentException("Period must be ≥ 1", nameof(period));
                _period = period;
                _lag = (period - 1) / 2;
                _ema = new EMAState(period, seed);
                _lagBuffer = new Queue<double>(_lag + 1);
            }

            public double Update(double value)
            {
                if (!MathBase.IsFinite(value))
                {
                    _ema.Update(MathBase.NaN);
                    return MathBase.NaN;
                }

                _lagBuffer.Enqueue(value);
                if (_lagBuffer.Count > _lag + 1)
                    _lagBuffer.Dequeue();

                if (_lagBuffer.Count <= _lag)
                {
                    _ema.Update(MathBase.NaN);
                    return MathBase.NaN;
                }

                double lagged = _lagBuffer.Peek();
                double adjusted = value + (value - lagged);
                return _ema.Update(adjusted);
            }

            public void Reset()
            {
                _ema.Reset();
                _lagBuffer.Clear();
            }
        }

        #endregion

        #region TMA (Triangular Moving Average)

        /// <summary>
        /// Triangular Moving Average - SMA of SMA.
        /// Produces smoother curve than single SMA.
        /// Warm-up: 2*P - 1
        /// </summary>
        public static double[] TMA(double[] src, int period)
        {
            if (src == null || period < 1)
                return null;

            double[] sma1 = SMA(src, period);
            return SMA(sma1, period);
        }

        /// <summary>
        /// Stateful TMA - cascaded SMA states.
        /// </summary>
        public class TMAState
        {
            private readonly SMAState _sma1;
            private readonly SMAState _sma2;

            public int Period => _sma1.Period;
            public double Value => _sma2.Value;
            public bool IsWarmedUp => _sma2.IsWarmedUp;

            public TMAState(int period)
            {
                _sma1 = new SMAState(period);
                _sma2 = new SMAState(period);
            }

            public double Update(double value)
            {
                double s1 = _sma1.Update(value);
                return _sma2.Update(s1);
            }

            public void Reset()
            {
                _sma1.Reset();
                _sma2.Reset();
            }
        }

        #endregion

        #region SWMA (Symmetric Weighted Moving Average)

        /// <summary>
        /// Symmetric Weighted Moving Average with triangular weights.
        /// For period P, weights increase to middle then decrease.
        /// Example P=5: weights [1, 2, 3, 2, 1]
        /// Warm-up: period bars
        /// </summary>
        public static double[] SWMA(double[] src, int period)
        {
            if (src == null || period < 1)
                return null;

            int n = src.Length;
            double[] result = new double[n];

            // Build triangular weights
            int mid = (period + 1) / 2;
            double[] weights = new double[period];
            double weightSum = 0;

            for (int i = 0; i < period; i++)
            {
                weights[i] = i < mid ? (i + 1) : (period - i);
                weightSum += weights[i];
            }

            // Fill NaN before warm-up
            for (int i = 0; i < Math.Min(period - 1, n); i++)
                result[i] = MathBase.NaN;

            if (n < period)
                return result;

            // Calculate SWMA
            for (int i = period - 1; i < n; i++)
            {
                double swma = 0;
                bool valid = true;

                for (int j = 0; j < period; j++)
                {
                    double val = src[i - period + 1 + j];
                    if (!MathBase.IsFinite(val))
                    {
                        valid = false;
                        break;
                    }
                    swma += val * weights[j];
                }

                result[i] = valid ? swma / weightSum : MathBase.NaN;
            }

            return result;
        }

        #endregion

        #region LSMA (Least Squares Moving Average)

        /// <summary>
        /// Least Squares Moving Average - endpoint of linear regression line.
        /// Fits best-fit line to last P points and returns endpoint value.
        /// More responsive to trends than SMA.
        /// Warm-up: period bars
        /// </summary>
        public static double[] LSMA(double[] src, int period)
        {
            if (src == null || period < 2)
                return null;

            int n = src.Length;
            double[] result = new double[n];

            // Pre-calculate sums for x values (time indices)
            double sumX = 0;
            double sumX2 = 0;
            for (int i = 0; i < period; i++)
            {
                sumX += i;
                sumX2 += i * i;
            }

            // Fill NaN before warm-up
            for (int i = 0; i < Math.Min(period - 1, n); i++)
                result[i] = MathBase.NaN;

            if (n < period)
                return result;

            // Calculate LSMA for each window
            for (int i = period - 1; i < n; i++)
            {
                double sumY = 0;
                double sumXY = 0;
                bool valid = true;

                for (int j = 0; j < period; j++)
                {
                    double y = src[i - period + 1 + j];
                    if (!MathBase.IsFinite(y))
                    {
                        valid = false;
                        break;
                    }
                    sumY += y;
                    sumXY += j * y;
                }

                if (!valid)
                {
                    result[i] = MathBase.NaN;
                    continue;
                }

                // Linear regression: y = a + b*x
                // b = (n*sumXY - sumX*sumY) / (n*sumX2 - sumX*sumX)
                // a = (sumY - b*sumX) / n
                double denominator = period * sumX2 - sumX * sumX;
                if (Math.Abs(denominator) < MathBase.EPS)
                {
                    result[i] = sumY / period; // Fallback to mean
                    continue;
                }

                double b = (period * sumXY - sumX * sumY) / denominator;
                double a = (sumY - b * sumX) / period;

                // Endpoint value at x = period - 1
                result[i] = a + b * (period - 1);
            }

            return result;
        }

        #endregion

        #region MedianMA (Median Moving Average)

        /// <summary>
        /// Median Moving Average - median of window.
        /// Extremely robust to outliers and spikes.
        /// Warm-up: period bars
        /// </summary>
        public static double[] MedianMA(double[] src, int period)
        {
            if (src == null || period < 1)
                return null;

            int n = src.Length;
            double[] result = new double[n];

            // Fill NaN before warm-up
            for (int i = 0; i < Math.Min(period - 1, n); i++)
                result[i] = MathBase.NaN;

            if (n < period)
                return result;

            double[] window = new double[period];

            for (int i = period - 1; i < n; i++)
            {
                bool valid = true;
                for (int j = 0; j < period; j++)
                {
                    window[j] = src[i - period + 1 + j];
                    if (!MathBase.IsFinite(window[j]))
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                {
                    result[i] = MathBase.NaN;
                    continue;
                }

                Array.Sort(window);
                result[i] = period % 2 == 1
                    ? window[period / 2]
                    : (window[period / 2 - 1] + window[period / 2]) / 2.0;
            }

            return result;
        }

        #endregion

        #region KAMA (Kaufman Adaptive Moving Average)

        /// <summary>
        /// Kaufman Adaptive Moving Average - adjusts smoothing based on Efficiency Ratio.
        /// ER = |change| / sum(|changes|) - measures trend strength
        /// Fast during trends, slow during consolidation.
        /// Warm-up: period bars (variable due to adaptive nature)
        /// </summary>
        public static double[] KAMA(double[] src, int period, int fastPeriod = 2, int slowPeriod = 30, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1 || fastPeriod < 1 || slowPeriod < fastPeriod)
                return null;

            int n = src.Length;
            double[] result = new double[n];

            double fastAlpha = 2.0 / (fastPeriod + 1);
            double slowAlpha = 2.0 / (slowPeriod + 1);

            // Initialize with NaN
            for (int i = 0; i < Math.Min(period, n); i++)
                result[i] = MathBase.NaN;

            if (n < period + 1)
                return result;

            // Calculate initial KAMA value (use SMA as seed)
            double kama = MathBase.Mean(src, 0, period);
            if (!MathBase.IsFinite(kama))
                return result;

            result[period - 1] = kama;

            // Calculate KAMA for remaining points
            for (int i = period; i < n; i++)
            {
                if (!MathBase.IsFinite(src[i]))
                {
                    for (int j = i; j < n; j++)
                        result[j] = MathBase.NaN;
                    return result;
                }

                // Calculate Efficiency Ratio
                double change = Math.Abs(src[i] - src[i - period]);
                double volatility = 0;

                for (int j = i - period + 1; j <= i; j++)
                {
                    if (!MathBase.IsFinite(src[j]) || !MathBase.IsFinite(src[j - 1]))
                    {
                        volatility = double.NaN;
                        break;
                    }
                    volatility += Math.Abs(src[j] - src[j - 1]);
                }

                if (!MathBase.IsFinite(volatility) || volatility < MathBase.EPS)
                {
                    result[i] = kama; // No change
                    continue;
                }

                double er = change / volatility;
                
                // Calculate smoothing constant
                double sc = er * (fastAlpha - slowAlpha) + slowAlpha;
                sc = sc * sc; // Square it for even more adaptive behavior

                // Update KAMA
                kama = kama + sc * (src[i] - kama);
                result[i] = kama;
            }

            return result;
        }

        #endregion

        #region T3 (Tillson T3)

        /// <summary>
        /// Tillson T3 - ultra-smooth low-lag moving average.
        /// Uses 6 cascaded EMAs with volume factor.
        /// b = 0.7 (typical), can range 0 to 1
        /// Warm-up: 3*period - 2
        /// </summary>
        public static double[] T3(double[] src, int period, double volumeFactor = 0.7, SeedMode seed = SeedMode.SmaSeed)
        {
            if (src == null || period < 1 || volumeFactor < 0 || volumeFactor > 1)
                return null;

            double b = volumeFactor;
            double b2 = b * b;
            double b3 = b2 * b;
            
            double c1 = -b3;
            double c2 = 3 * b2 + 3 * b3;
            double c3 = -6 * b2 - 3 * b - 3 * b3;
            double c4 = 1 + 3 * b + b3 + 3 * b2;

            // Calculate 6 cascaded EMAs
            double[] e1 = EMA(src, period, seed);
            double[] e2 = EMA(e1, period, seed);
            double[] e3 = EMA(e2, period, seed);
            double[] e4 = EMA(e3, period, seed);
            double[] e5 = EMA(e4, period, seed);
            double[] e6 = EMA(e5, period, seed);

            int n = src.Length;
            double[] result = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (MathBase.IsFinite(e3[i]) && MathBase.IsFinite(e4[i]) && 
                    MathBase.IsFinite(e5[i]) && MathBase.IsFinite(e6[i]))
                {
                    result[i] = c1 * e6[i] + c2 * e5[i] + c3 * e4[i] + c4 * e3[i];
                }
                else
                {
                    result[i] = MathBase.NaN;
                }
            }

            return result;
        }

        #endregion
    }
}
