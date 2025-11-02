//+------------------------------------------------------------------+
//|                                                     MathBase.mqh |
//|                                      TradingLibrary.Core.MathBase |
//|                   Cross-platform mathematical utilities for MT5   |
//+------------------------------------------------------------------+
#property copyright "TradingLibrary"
#property link      ""
#property version   "1.00"
#property strict

//+------------------------------------------------------------------+
//| Core mathematical utilities providing platform-independent ops.  |
//| All functions follow strict deterministic rules with explicit    |
//| NaN propagation.                                                  |
//| Reference: SharedLibrary/Core specification - MathBase section   |
//+------------------------------------------------------------------+

//--- Constants
#define EPS 1e-12                    // Epsilon for floating-point comparisons
#define NaN_VALUE DBL_MAX            // NaN representation (MQL5 doesn't have native NaN constant)

//+------------------------------------------------------------------+
//| Rounding modes for deterministic cross-platform results          |
//+------------------------------------------------------------------+
enum ENUM_ROUND_MODE
{
   ROUND_HALF_AWAY_FROM_ZERO = 0,   // Rounds to nearest, ties away from zero
   ROUND_HALF_TO_EVEN = 1,          // Rounds to nearest, ties to even (banker's)
   ROUND_TRUNCATE = 2,              // Truncates toward zero
   ROUND_FLOOR = 3,                 // Rounds toward negative infinity
   ROUND_CEILING = 4                // Rounds toward positive infinity
};

//+------------------------------------------------------------------+
//| Helper function to check if value is finite (not NaN, not ±Inf)  |
//+------------------------------------------------------------------+
bool IsFinite(double x)
{
   return !MathIsValidNumber(x) ? false : (x != EMPTY_VALUE && x > -DBL_MAX && x < DBL_MAX);
}

//+------------------------------------------------------------------+
//| Price Shortcuts                                                   |
//+------------------------------------------------------------------+

//--- High-Low midpoint: (H + L) / 2
double HL2(double high, double low)
{
   if(!IsFinite(high) || !IsFinite(low))
      return EMPTY_VALUE;
   return (high + low) * 0.5;
}

//--- Typical price: (H + L + C) / 3
double HLC3(double high, double low, double close)
{
   if(!IsFinite(high) || !IsFinite(low) || !IsFinite(close))
      return EMPTY_VALUE;
   return (high + low + close) / 3.0;
}

//--- Average price: (O + H + L + C) / 4
double OHLC4(double open, double high, double low, double close)
{
   if(!IsFinite(open) || !IsFinite(high) || !IsFinite(low) || !IsFinite(close))
      return EMPTY_VALUE;
   return (open + high + low + close) * 0.25;
}

//+------------------------------------------------------------------+
//| Safe Operations                                                   |
//+------------------------------------------------------------------+

//--- Safe division that returns EMPTY_VALUE on invalid denominator
double SafeDivide(double numerator, double denominator)
{
   if(!IsFinite(numerator) || !IsFinite(denominator))
      return EMPTY_VALUE;
   if(MathAbs(denominator) < EPS)
      return EMPTY_VALUE;
   return numerator / denominator;
}

//--- Safe division with fallback value
double SafeDivideOrDefault(double numerator, double denominator, double defaultValue)
{
   double result = SafeDivide(numerator, denominator);
   return (result == EMPTY_VALUE) ? defaultValue : result;
}

//--- Safe square root
double SafeSqrt(double x)
{
   if(!IsFinite(x) || x < 0)
      return EMPTY_VALUE;
   return MathSqrt(x);
}

//--- Safe natural logarithm
double SafeLog(double x)
{
   if(!IsFinite(x) || x <= 0)
      return EMPTY_VALUE;
   return MathLog(x);
}

//--- Safe base-10 logarithm
double SafeLog10(double x)
{
   if(!IsFinite(x) || x <= 0)
      return EMPTY_VALUE;
   return MathLog10(x);
}

//+------------------------------------------------------------------+
//| Comparison & Validation                                           |
//+------------------------------------------------------------------+

//--- Tolerance-based equality comparison
bool AlmostEqual(double a, double b, double epsilon = EPS)
{
   if(!IsFinite(a) || !IsFinite(b))
      return (a == EMPTY_VALUE && b == EMPTY_VALUE); // Both EMPTY = equal
   return MathAbs(a - b) <= epsilon;
}

//--- Clamps value to specified range [lo, hi]
double Clamp(double x, double lo, double hi)
{
   if(!IsFinite(x))
      return EMPTY_VALUE;
   if(x < lo) return lo;
   if(x > hi) return hi;
   return x;
}

//--- Clamps value to [0, 1] range
double Bound01(double x)
{
   return Clamp(x, 0.0, 1.0);
}

//+------------------------------------------------------------------+
//| Normalization & Rounding                                          |
//+------------------------------------------------------------------+

//--- Platform-independent rounding
double Round(double value, int digits, ENUM_ROUND_MODE mode = ROUND_HALF_AWAY_FROM_ZERO)
{
   if(!IsFinite(value))
      return EMPTY_VALUE;
      
   if(digits < 0 || digits > 15)
   {
      Print("Error: Digits must be 0-15");
      return EMPTY_VALUE;
   }
   
   double multiplier = MathPow(10, digits);
   double scaled = value * multiplier;
   double rounded;
   
   switch(mode)
   {
      case ROUND_HALF_AWAY_FROM_ZERO:
         rounded = MathRound(scaled);
         break;
         
      case ROUND_HALF_TO_EVEN:
         // Banker's rounding implementation
         {
            double floor_val = MathFloor(scaled);
            double frac = scaled - floor_val;
            
            if(MathAbs(frac - 0.5) < EPS)
            {
               // Exactly 0.5 - round to even
               int floor_int = (int)floor_val;
               rounded = (floor_int % 2 == 0) ? floor_val : MathCeil(scaled);
            }
            else
            {
               rounded = MathRound(scaled);
            }
         }
         break;
         
      case ROUND_TRUNCATE:
         rounded = (scaled >= 0) ? MathFloor(scaled) : MathCeil(scaled);
         break;
         
      case ROUND_FLOOR:
         rounded = MathFloor(scaled);
         break;
         
      case ROUND_CEILING:
         rounded = MathCeil(scaled);
         break;
         
      default:
         rounded = MathRound(scaled);
         break;
   }
   
   return rounded / multiplier;
}

//--- Normalizes double to specified precision
double NormalizeDouble(double value, int digits, ENUM_ROUND_MODE mode = ROUND_HALF_AWAY_FROM_ZERO)
{
   return Round(value, digits, mode);
}

//--- Quantizes value to discrete steps
double Quantize(double value, double step, ENUM_ROUND_MODE mode = ROUND_HALF_AWAY_FROM_ZERO)
{
   if(!IsFinite(value) || !IsFinite(step))
      return EMPTY_VALUE;
   if(step <= 0)
   {
      Print("Error: Step must be positive");
      return EMPTY_VALUE;
   }
   
   double scaled = value / step;
   double rounded;
   
   switch(mode)
   {
      case ROUND_HALF_AWAY_FROM_ZERO:
         rounded = MathRound(scaled);
         break;
         
      case ROUND_HALF_TO_EVEN:
         {
            double floor_val = MathFloor(scaled);
            double frac = scaled - floor_val;
            
            if(MathAbs(frac - 0.5) < EPS)
            {
               int floor_int = (int)floor_val;
               rounded = (floor_int % 2 == 0) ? floor_val : MathCeil(scaled);
            }
            else
            {
               rounded = MathRound(scaled);
            }
         }
         break;
         
      case ROUND_TRUNCATE:
         rounded = (scaled >= 0) ? MathFloor(scaled) : MathCeil(scaled);
         break;
         
      case ROUND_FLOOR:
         rounded = MathFloor(scaled);
         break;
         
      case ROUND_CEILING:
         rounded = MathCeil(scaled);
         break;
         
      default:
         rounded = MathRound(scaled);
         break;
   }
   
   return rounded * step;
}

//--- Rounds price to valid tick size
double RoundToTick(double price, double tickSize, ENUM_ROUND_MODE mode = ROUND_HALF_AWAY_FROM_ZERO)
{
   return Quantize(price, tickSize, mode);
}

//+------------------------------------------------------------------+
//| Statistics                                                        |
//+------------------------------------------------------------------+

//--- Calculates arithmetic mean of array segment
double Mean(const double &values[], int startIndex, int period)
{
   if(period <= 0 || startIndex < 0)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(values))
      return EMPTY_VALUE;
   
   double sum = 0;
   for(int i = 0; i < period; i++)
   {
      double val = values[startIndex + i];
      if(!IsFinite(val))
         return EMPTY_VALUE; // Strict NaN propagation
      sum += val;
   }
   
   return sum / period;
}

//--- Mean over most recent period (convenience overload)
double Mean(const double &values[], int period)
{
   int size = ArraySize(values);
   if(size < period)
      return EMPTY_VALUE;
   return Mean(values, size - period, period);
}

//--- Calculates variance of array segment
double Variance(const double &values[], int startIndex, int period, bool sample = true)
{
   if(period <= 0 || startIndex < 0)
      return EMPTY_VALUE;
   if(sample && period < 2)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(values))
      return EMPTY_VALUE;
   
   double mean = Mean(values, startIndex, period);
   if(!IsFinite(mean))
      return EMPTY_VALUE;
   
   double sumSquares = 0;
   for(int i = 0; i < period; i++)
   {
      double val = values[startIndex + i];
      if(!IsFinite(val))
         return EMPTY_VALUE;
      double diff = val - mean;
      sumSquares += diff * diff;
   }
   
   int divisor = sample ? period - 1 : period;
   return sumSquares / divisor;
}

//--- Variance over most recent period
double Variance(const double &values[], int period, bool sample = true)
{
   int size = ArraySize(values);
   if(size < period)
      return EMPTY_VALUE;
   return Variance(values, size - period, period, sample);
}

//--- Calculates standard deviation
double StdDev(const double &values[], int startIndex, int period, bool sample = true)
{
   double variance = Variance(values, startIndex, period, sample);
   return SafeSqrt(variance);
}

//--- Standard deviation over most recent period
double StdDev(const double &values[], int period, bool sample = true)
{
   int size = ArraySize(values);
   if(size < period)
      return EMPTY_VALUE;
   return StdDev(values, size - period, period, sample);
}

//--- Calculates covariance between two series
double Covariance(const double &x[], const double &y[], int startIndex, int period, bool sample = true)
{
   if(period <= 0 || startIndex < 0)
      return EMPTY_VALUE;
   if(sample && period < 2)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(x) || startIndex + period > ArraySize(y))
      return EMPTY_VALUE;
   
   double meanX = Mean(x, startIndex, period);
   double meanY = Mean(y, startIndex, period);
   if(!IsFinite(meanX) || !IsFinite(meanY))
      return EMPTY_VALUE;
   
   double sumProduct = 0;
   for(int i = 0; i < period; i++)
   {
      double valX = x[startIndex + i];
      double valY = y[startIndex + i];
      if(!IsFinite(valX) || !IsFinite(valY))
         return EMPTY_VALUE;
      sumProduct += (valX - meanX) * (valY - meanY);
   }
   
   int divisor = sample ? period - 1 : period;
   return sumProduct / divisor;
}

//--- Covariance over most recent period
double Covariance(const double &x[], const double &y[], int period, bool sample = true)
{
   int sizeX = ArraySize(x);
   int sizeY = ArraySize(y);
   if(sizeX < period || sizeY < period)
      return EMPTY_VALUE;
   int startIndex = MathMin(sizeX, sizeY) - period;
   return Covariance(x, y, startIndex, period, sample);
}

//--- Pearson correlation coefficient
double Correlation(const double &x[], const double &y[], int startIndex, int period)
{
   if(period <= 0 || startIndex < 0)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(x) || startIndex + period > ArraySize(y))
      return EMPTY_VALUE;
   
   double cov = Covariance(x, y, startIndex, period, true);
   double stdX = StdDev(x, startIndex, period, true);
   double stdY = StdDev(y, startIndex, period, true);
   
   return SafeDivide(cov, stdX * stdY);
}

//--- Correlation over most recent period
double Correlation(const double &x[], const double &y[], int period)
{
   int sizeX = ArraySize(x);
   int sizeY = ArraySize(y);
   if(sizeX < period || sizeY < period)
      return EMPTY_VALUE;
   int startIndex = MathMin(sizeX, sizeY) - period;
   return Correlation(x, y, startIndex, period);
}

//--- Simple sum over window (no NaN skipping)
double Sum(const double &values[], int startIndex, int period)
{
   if(period <= 0 || startIndex < 0)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(values))
      return EMPTY_VALUE;
   
   double sum = 0;
   for(int i = 0; i < period; i++)
   {
      double val = values[startIndex + i];
      if(!IsFinite(val))
         return EMPTY_VALUE;
      sum += val;
   }
   return sum;
}

//--- Sum over most recent period
double Sum(const double &values[], int period)
{
   int size = ArraySize(values);
   if(size < period)
      return EMPTY_VALUE;
   return Sum(values, size - period, period);
}

//+------------------------------------------------------------------+
//| Affine Transforms & Helpers                                       |
//+------------------------------------------------------------------+

//--- Linear interpolation: a + t*(b-a)
double Lerp(double a, double b, double t)
{
   if(!IsFinite(a) || !IsFinite(b) || !IsFinite(t))
      return EMPTY_VALUE;
   return a + t * (b - a);
}

//--- Inverse lerp: finds t such that x = Lerp(a, b, t)
double Unlerp(double a, double b, double x)
{
   if(!IsFinite(a) || !IsFinite(b) || !IsFinite(x))
      return EMPTY_VALUE;
   if(AlmostEqual(a, b))
      return EMPTY_VALUE;
   return (x - a) / (b - a);
}

//--- Maps value from input range to output range
double MapToRange(double x, double inLo, double inHi, double outLo, double outHi, bool clamp = false)
{
   if(!IsFinite(x) || !IsFinite(inLo) || !IsFinite(inHi) || 
      !IsFinite(outLo) || !IsFinite(outHi))
      return EMPTY_VALUE;
   
   double t = Unlerp(inLo, inHi, x);
   if(!IsFinite(t))
      return EMPTY_VALUE;
   
   double result = Lerp(outLo, outHi, t);
   
   if(clamp)
   {
      double minVal = MathMin(outLo, outHi);
      double maxVal = MathMax(outLo, outHi);
      result = Clamp(result, minVal, maxVal);
   }
   
   return result;
}

//--- Returns sign of value with EPS tolerance
int Sign(double x)
{
   if(!IsFinite(x))
      return 0;
   if(x > EPS) return 1;
   if(x < -EPS) return -1;
   return 0;
}

//--- Calculates percent change
double PercentChange(double current, double previous)
{
   if(!IsFinite(current) || !IsFinite(previous))
      return EMPTY_VALUE;
   if(MathAbs(previous) < EPS)
      return EMPTY_VALUE;
   return ((current - previous) / previous) * 100.0;
}

//+------------------------------------------------------------------+
//| Array Utilities                                                   |
//+------------------------------------------------------------------+

//--- Fills array with specified value
void Fill(double &arr[], int length, double value)
{
   if(length < 0)
   {
      Print("Error: Length must be non-negative");
      return;
   }
   
   ArrayResize(arr, length);
   ArrayInitialize(arr, value);
}

//--- Fills array with EMPTY_VALUE (NaN equivalent)
void FillNaN(double &arr[], int length)
{
   Fill(arr, length, EMPTY_VALUE);
}

//--- Checks if all values in array are finite
bool AllFinite(const double &values[])
{
   int size = ArraySize(values);
   for(int i = 0; i < size; i++)
   {
      if(!IsFinite(values[i]))
         return false;
   }
   return true;
}

//+------------------------------------------------------------------+
//| End of MathBase.mqh                                               |
//+------------------------------------------------------------------+
