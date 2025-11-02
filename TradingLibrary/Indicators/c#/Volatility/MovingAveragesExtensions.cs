using System;

namespace TradingLibrary.Core
{
    /// <summary>
    /// Extensions and utilities for MovingAverages module.
    /// Provides unified enum and helper methods for indicator implementations.
    /// </summary>
    public static class MovingAveragesExtensions
    {
        #region Enums

        /// <summary>
        /// Unified averaging modes for indicators.
        /// Maps to specific MovingAverages methods.
        /// </summary>
        public enum AvgMode
        {
            /// <summary>Simple Moving Average - equal weights</summary>
            SMA = 0,
            /// <summary>Exponential Moving Average - exponential decay</summary>
            EMA = 1,
            /// <summary>RMA (Wilder's smoothing) - special EMA with alpha=1/period</summary>
            RMA = 2,
            /// <summary>Weighted Moving Average - linear weights</summary>
            WMA = 3
        }

        #endregion

        #region Unified Calculate Method

        /// <summary>
        /// Calculates moving average using specified mode.
        /// Unified interface for all MA types.
        /// </summary>
        /// <param name="src">Source values (chronological: 0 → oldest)</param>
        /// <param name="period">Period for MA calculation</param>
        /// <param name="mode">Averaging mode (SMA, EMA, RMA, WMA)</param>
        /// <returns>Moving average array</returns>
        /// <exception cref="ArgumentNullException">If src is null</exception>
        /// <exception cref="ArgumentException">If period &lt; 1 or unsupported mode</exception>
        public static double[] CalculateAverage(double[] src, int period, AvgMode mode)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));

            if (period < 1)
                throw new ArgumentException($"Period must be >= 1, got {period}", nameof(period));

            switch (mode)
            {
                case AvgMode.SMA:
                    return MovingAverages.SMA(src, period);

                case AvgMode.EMA:
                    return MovingAverages.EMA(src, period, MovingAverages.SeedMode.SmaSeed);

                case AvgMode.RMA:
                    return MovingAverages.RMA(src, period, MovingAverages.SeedMode.SmaSeed);

                case AvgMode.WMA:
                    return MovingAverages.WMA(src, period);

                default:
                    throw new ArgumentException($"Unsupported averaging mode: {mode}", nameof(mode));
            }
        }

        /// <summary>
        /// Creates stateful MA calculator based on mode.
        /// Returns object that needs to be cast to appropriate type.
        /// </summary>
        /// <param name="period">Period for MA calculation</param>
        /// <param name="mode">Averaging mode</param>
        /// <returns>MA state object (SMAState, EMAState, RMAState, or WMAState)</returns>
        public static object CreateMaState(int period, AvgMode mode)
        {
            switch (mode)
            {
                case AvgMode.SMA:
                    return new MovingAverages.SMAState(period);

                case AvgMode.EMA:
                    return new MovingAverages.EMAState(period, MovingAverages.SeedMode.SmaSeed);

                case AvgMode.RMA:
                    return new MovingAverages.RMAState(period);

                case AvgMode.WMA:
                    return new MovingAverages.WMAState(period);

                default:
                    throw new ArgumentException($"Unsupported averaging mode: {mode}", nameof(mode));
            }
        }

        /// <summary>
        /// Updates MA state object with new value.
        /// Handles type casting internally.
        /// </summary>
        /// <param name="state">MA state object from CreateMaState</param>
        /// <param name="value">New value to add</param>
        /// <param name="mode">Original mode used to create state</param>
        /// <returns>Updated MA value</returns>
        public static double UpdateMaState(object state, double value, AvgMode mode)
        {
            switch (mode)
            {
                case AvgMode.SMA:
                    return ((MovingAverages.SMAState)state).Update(value);

                case AvgMode.EMA:
                    return ((MovingAverages.EMAState)state).Update(value);

                case AvgMode.RMA:
                    return ((MovingAverages.RMAState)state).Update(value);

                case AvgMode.WMA:
                    return ((MovingAverages.WMAState)state).Update(value);

                default:
                    throw new ArgumentException($"Unsupported averaging mode: {mode}", nameof(mode));
            }
        }

        /// <summary>
        /// Resets MA state to initial condition.
        /// </summary>
        /// <param name="state">MA state object to reset</param>
        /// <param name="mode">Original mode used to create state</param>
        public static void ResetMaState(object state, AvgMode mode)
        {
            switch (mode)
            {
                case AvgMode.SMA:
                    ((MovingAverages.SMAState)state).Reset();
                    break;

                case AvgMode.EMA:
                    ((MovingAverages.EMAState)state).Reset();
                    break;

                case AvgMode.RMA:
                    ((MovingAverages.RMAState)state).Reset();
                    break;

                case AvgMode.WMA:
                    ((MovingAverages.WMAState)state).Reset();
                    break;

                default:
                    throw new ArgumentException($"Unsupported averaging mode: {mode}", nameof(mode));
            }
        }

        #endregion
    }
}
