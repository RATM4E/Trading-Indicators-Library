//+------------------------------------------------------------------+
//|                                              MovingAverages.mqh  |
//|                                TradingLibrary.Core.MovingAverages |
//|              Comprehensive MA implementations for MT5             |
//+------------------------------------------------------------------+
#property copyright "TradingLibrary"
#property link      ""
#property version   "1.00"
#property strict

#include "MathBase.mqh"

//+------------------------------------------------------------------+
//| Comprehensive moving average implementations (batch forms)       |
//| All algorithms are deterministic and cross-platform identical.   |
//| Reference: SharedLibrary/Core specification - MovingAverages     |
//|                                                                   |
//| Key principles:                                                   |
//| - Indexing: 0 → oldest, N-1 → newest (chronological)            |
//| - NaN propagation: any NaN in required window → NaN output      |
//| - Warm-up: defined per type; first valid output after window    |
//| - Seed modes: SMA, FirstValue, Zero, NaN (default: SMA)         |
//+------------------------------------------------------------------+

//+------------------------------------------------------------------+
//| Seed initialization modes for exponential moving averages        |
//+------------------------------------------------------------------+
enum ENUM_SEED_MODE
{
   SEED_SMA = 0,           // Use SMA of first P values as seed (recommended)
   SEED_FIRST_VALUE = 1,   // Use first value directly as seed
   SEED_ZERO = 2,          // Start from zero
   SEED_NAN = 3            // Return NaN until full SMA period available
};

//+------------------------------------------------------------------+
//| SMA - Simple Moving Average                                       |
//| Warm-up: period bars                                              |
//+------------------------------------------------------------------+
void SMA(const double &src[], int period, double &result[])
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 1 || n < period)
      return;
   
   // First SMA using full period
   double sum = 0;
   for(int i = 0; i < period; i++)
   {
      if(!IsFinite(src[i]))
         return; // Strict NaN propagation
      sum += src[i];
   }
   result[period - 1] = sum / period;
   
   // Rolling SMA: subtract oldest, add newest
   for(int i = period; i < n; i++)
   {
      if(!IsFinite(src[i]))
      {
         // Fill remaining with EMPTY_VALUE
         for(int j = i; j < n; j++)
            result[j] = EMPTY_VALUE;
         return;
      }
      
      sum = sum - src[i - period] + src[i];
      result[i] = sum / period;
   }
}

//+------------------------------------------------------------------+
//| EMA - Exponential Moving Average                                  |
//| α = 2 / (period + 1)                                             |
//| Warm-up: period bars (with SmaSeed)                              |
//+------------------------------------------------------------------+
void EMA(const double &src[], int period, double &result[], 
         ENUM_SEED_MODE seed = SEED_SMA)
{
   if(period < 1)
      return;
   
   double alpha = 2.0 / (period + 1);
   EMAFromAlpha(src, alpha, period, result, seed);
}

//+------------------------------------------------------------------+
//| EMA using explicit alpha value                                    |
//+------------------------------------------------------------------+
void EMAFromAlpha(const double &src[], double alpha, int warmupPeriod,
                  double &result[], ENUM_SEED_MODE seed = SEED_SMA)
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(alpha <= 0 || alpha > 1)
      return;
   
   double ema = EMPTY_VALUE;
   int seedIndex = -1;
   
   switch(seed)
   {
      case SEED_SMA:
         if(n < warmupPeriod)
            return;
         // Calculate SMA seed
         {
            double sum = 0;
            for(int i = 0; i < warmupPeriod; i++)
            {
               if(!IsFinite(src[i]))
                  return;
               sum += src[i];
            }
            ema = sum / warmupPeriod;
            seedIndex = warmupPeriod - 1;
         }
         break;
         
      case SEED_FIRST_VALUE:
         if(n < 1 || !IsFinite(src[0]))
            return;
         ema = src[0];
         seedIndex = 0;
         break;
         
      case SEED_ZERO:
         ema = 0;
         seedIndex = 0;
         break;
         
      case SEED_NAN:
         if(n < warmupPeriod)
            return;
         seedIndex = warmupPeriod - 1;
         ema = EMPTY_VALUE;
         break;
   }
   
   // Seed value
   if(seedIndex >= 0)
      result[seedIndex] = ema;
   
   // EMA calculation
   for(int i = seedIndex + 1; i < n; i++)
   {
      if(!IsFinite(src[i]) || !IsFinite(ema))
      {
         for(int j = i; j < n; j++)
            result[j] = EMPTY_VALUE;
         return;
      }
      
      ema = alpha * src[i] + (1 - alpha) * ema;
      result[i] = ema;
   }
}

//+------------------------------------------------------------------+
//| EMA from half-life                                                |
//| α = 1 - exp(-ln(2) / halfLife)                                  |
//+------------------------------------------------------------------+
void EMAFromHalfLife(const double &src[], double halfLife, double &result[],
                     ENUM_SEED_MODE seed = SEED_SMA)
{
   if(halfLife <= 0)
      return;
   double alpha = 1.0 - MathExp(-MathLog(2.0) / halfLife);
   int warmup = (int)MathCeil(halfLife);
   EMAFromAlpha(src, alpha, warmup, result, seed);
}

//+------------------------------------------------------------------+
//| EMA from time constant (tau)                                      |
//| α = 1 - exp(-1 / tau)                                           |
//+------------------------------------------------------------------+
void EMAFromTau(const double &src[], double tau, double &result[],
                ENUM_SEED_MODE seed = SEED_SMA)
{
   if(tau <= 0)
      return;
   double alpha = 1.0 - MathExp(-1.0 / tau);
   int warmup = (int)MathCeil(tau);
   EMAFromAlpha(src, alpha, warmup, result, seed);
}

//+------------------------------------------------------------------+
//| RMA - Wilder's Moving Average / SMMA                             |
//| α = 1 / period (slower decay than EMA)                          |
//| Warm-up: period bars                                              |
//+------------------------------------------------------------------+
void RMA(const double &src[], int period, double &result[],
         ENUM_SEED_MODE seed = SEED_SMA)
{
   if(period < 1)
      return;
   
   double alpha = 1.0 / period;
   EMAFromAlpha(src, alpha, period, result, seed);
}

//+------------------------------------------------------------------+
//| WMA - Weighted Moving Average                                     |
//| Linear weights 1, 2, ..., P                                      |
//| Warm-up: period bars                                              |
//+------------------------------------------------------------------+
void WMA(const double &src[], int period, double &result[])
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 1 || n < period)
      return;
   
   // Weight sum: 1 + 2 + ... + P = P*(P+1)/2
   double weightSum = period * (period + 1) / 2.0;
   
   for(int i = period - 1; i < n; i++)
   {
      double wma = 0;
      int weight = 1;
      bool valid = true;
      
      for(int j = i - period + 1; j <= i; j++)
      {
         if(!IsFinite(src[j]))
         {
            valid = false;
            break;
         }
         wma += src[j] * weight;
         weight++;
      }
      
      result[i] = valid ? wma / weightSum : EMPTY_VALUE;
   }
}

//+------------------------------------------------------------------+
//| HMA - Hull Moving Average                                         |
//| Formula: WMA(2*WMA(P/2) - WMA(P), √P)                           |
//| Warm-up: max(P, P/2) + √P - 1                                   |
//+------------------------------------------------------------------+
void HMA(const double &src[], int period, double &result[])
{
   if(period < 1)
      return;
   
   int n = ArraySize(src);
   int halfPeriod = (int)MathCeil(period / 2.0);
   int sqrtPeriod = (int)MathCeil(MathSqrt(period));
   
   double wmaHalf[], wmaFull[], diff[];
   WMA(src, halfPeriod, wmaHalf);
   WMA(src, period, wmaFull);
   
   if(ArraySize(wmaHalf) == 0 || ArraySize(wmaFull) == 0)
      return;
   
   ArrayResize(diff, n);
   for(int i = 0; i < n; i++)
   {
      if(IsFinite(wmaHalf[i]) && IsFinite(wmaFull[i]))
         diff[i] = 2 * wmaHalf[i] - wmaFull[i];
      else
         diff[i] = EMPTY_VALUE;
   }
   
   WMA(diff, sqrtPeriod, result);
}

//+------------------------------------------------------------------+
//| DEMA - Double Exponential Moving Average                         |
//| Formula: 2*EMA(P) - EMA(EMA(P))                                 |
//| Warm-up: 2*P - 1                                                 |
//+------------------------------------------------------------------+
void DEMA(const double &src[], int period, double &result[],
          ENUM_SEED_MODE seed = SEED_SMA)
{
   if(period < 1)
      return;
   
   int n = ArraySize(src);
   double ema1[], ema2[];
   
   EMA(src, period, ema1, seed);
   EMA(ema1, period, ema2, seed);
   
   ArrayResize(result, n);
   for(int i = 0; i < n; i++)
   {
      if(IsFinite(ema1[i]) && IsFinite(ema2[i]))
         result[i] = 2 * ema1[i] - ema2[i];
      else
         result[i] = EMPTY_VALUE;
   }
}

//+------------------------------------------------------------------+
//| TEMA - Triple Exponential Moving Average                         |
//| Formula: 3*EMA(P) - 3*EMA(EMA(P)) + EMA(EMA(EMA(P)))           |
//| Warm-up: 3*P - 2                                                 |
//+------------------------------------------------------------------+
void TEMA(const double &src[], int period, double &result[],
          ENUM_SEED_MODE seed = SEED_SMA)
{
   if(period < 1)
      return;
   
   int n = ArraySize(src);
   double ema1[], ema2[], ema3[];
   
   EMA(src, period, ema1, seed);
   EMA(ema1, period, ema2, seed);
   EMA(ema2, period, ema3, seed);
   
   ArrayResize(result, n);
   for(int i = 0; i < n; i++)
   {
      if(IsFinite(ema1[i]) && IsFinite(ema2[i]) && IsFinite(ema3[i]))
         result[i] = 3 * ema1[i] - 3 * ema2[i] + ema3[i];
      else
         result[i] = EMPTY_VALUE;
   }
}

//+------------------------------------------------------------------+
//| ZLEMA - Zero-Lag Exponential Moving Average                      |
//| Adjusts input by lag before applying EMA                         |
//| lag = ⌊(period - 1) / 2⌋                                        |
//| Warm-up: period + lag                                            |
//+------------------------------------------------------------------+
void ZLEMA(const double &src[], int period, double &result[],
           ENUM_SEED_MODE seed = SEED_SMA)
{
   if(period < 1)
      return;
   
   int n = ArraySize(src);
   int lag = (period - 1) / 2;
   
   if(n < lag + 1)
   {
      FillNaN(result, n);
      return;
   }
   
   double adjusted[];
   ArrayResize(adjusted, n);
   ArrayInitialize(adjusted, EMPTY_VALUE);
   
   // Calculate adjusted input
   for(int i = lag; i < n; i++)
   {
      if(!IsFinite(src[i]) || !IsFinite(src[i - lag]))
         adjusted[i] = EMPTY_VALUE;
      else
         adjusted[i] = src[i] + (src[i] - src[i - lag]);
   }
   
   EMA(adjusted, period, result, seed);
}

//+------------------------------------------------------------------+
//| TMA - Triangular Moving Average                                  |
//| SMA of SMA                                                        |
//| Warm-up: 2*P - 1                                                 |
//+------------------------------------------------------------------+
void TMA(const double &src[], int period, double &result[])
{
   if(period < 1)
      return;
   
   double sma1[];
   SMA(src, period, sma1);
   SMA(sma1, period, result);
}

//+------------------------------------------------------------------+
//| SWMA - Symmetric Weighted Moving Average                         |
//| Triangular weights (1,2,3,2,1 for P=5)                          |
//| Warm-up: period bars                                             |
//+------------------------------------------------------------------+
void SWMA(const double &src[], int period, double &result[])
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 1 || n < period)
      return;
   
   // Build triangular weights
   int mid = (period + 1) / 2;
   double weights[];
   ArrayResize(weights, period);
   double weightSum = 0;
   
   for(int i = 0; i < period; i++)
   {
      weights[i] = i < mid ? (i + 1) : (period - i);
      weightSum += weights[i];
   }
   
   // Calculate SWMA
   for(int i = period - 1; i < n; i++)
   {
      double swma = 0;
      bool valid = true;
      
      for(int j = 0; j < period; j++)
      {
         double val = src[i - period + 1 + j];
         if(!IsFinite(val))
         {
            valid = false;
            break;
         }
         swma += val * weights[j];
      }
      
      result[i] = valid ? swma / weightSum : EMPTY_VALUE;
   }
}

//+------------------------------------------------------------------+
//| LSMA - Least Squares Moving Average                              |
//| Endpoint of linear regression line                               |
//| Warm-up: period bars                                             |
//+------------------------------------------------------------------+
void LSMA(const double &src[], int period, double &result[])
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 2 || n < period)
      return;
   
   // Pre-calculate sums for x values (time indices)
   double sumX = 0;
   double sumX2 = 0;
   for(int i = 0; i < period; i++)
   {
      sumX += i;
      sumX2 += i * i;
   }
   
   // Calculate LSMA for each window
   for(int i = period - 1; i < n; i++)
   {
      double sumY = 0;
      double sumXY = 0;
      bool valid = true;
      
      for(int j = 0; j < period; j++)
      {
         double y = src[i - period + 1 + j];
         if(!IsFinite(y))
         {
            valid = false;
            break;
         }
         sumY += y;
         sumXY += j * y;
      }
      
      if(!valid)
      {
         result[i] = EMPTY_VALUE;
         continue;
      }
      
      // Linear regression: y = a + b*x
      double denominator = period * sumX2 - sumX * sumX;
      if(MathAbs(denominator) < EPS)
      {
         result[i] = sumY / period; // Fallback to mean
         continue;
      }
      
      double b = (period * sumXY - sumX * sumY) / denominator;
      double a = (sumY - b * sumX) / period;
      
      // Endpoint value at x = period - 1
      result[i] = a + b * (period - 1);
   }
}

//+------------------------------------------------------------------+
//| MedianMA - Median Moving Average                                 |
//| Median of window (robust to outliers)                            |
//| Warm-up: period bars                                             |
//+------------------------------------------------------------------+
void MedianMA(const double &src[], int period, double &result[])
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 1 || n < period)
      return;
   
   double window[];
   ArrayResize(window, period);
   
   for(int i = period - 1; i < n; i++)
   {
      bool valid = true;
      for(int j = 0; j < period; j++)
      {
         window[j] = src[i - period + 1 + j];
         if(!IsFinite(window[j]))
         {
            valid = false;
            break;
         }
      }
      
      if(!valid)
      {
         result[i] = EMPTY_VALUE;
         continue;
      }
      
      ArraySort(window);
      if(period % 2 == 1)
         result[i] = window[period / 2];
      else
         result[i] = (window[period / 2 - 1] + window[period / 2]) / 2.0;
   }
}

//+------------------------------------------------------------------+
//| KAMA - Kaufman Adaptive Moving Average                           |
//| Adjusts smoothing based on Efficiency Ratio                      |
//| Warm-up: period bars (variable due to adaptive nature)           |
//+------------------------------------------------------------------+
void KAMA(const double &src[], int period, double &result[],
          int fastPeriod = 2, int slowPeriod = 30, ENUM_SEED_MODE seed = SEED_SMA)
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 1 || fastPeriod < 1 || slowPeriod < fastPeriod || n < period + 1)
      return;
   
   double fastAlpha = 2.0 / (fastPeriod + 1);
   double slowAlpha = 2.0 / (slowPeriod + 1);
   
   // Calculate initial KAMA value (use SMA as seed)
   double kama = Mean(src, 0, period);
   if(!IsFinite(kama))
      return;
   
   result[period - 1] = kama;
   
   // Calculate KAMA for remaining points
   for(int i = period; i < n; i++)
   {
      if(!IsFinite(src[i]))
      {
         for(int j = i; j < n; j++)
            result[j] = EMPTY_VALUE;
         return;
      }
      
      // Calculate Efficiency Ratio
      double change = MathAbs(src[i] - src[i - period]);
      double volatility = 0;
      bool validVol = true;
      
      for(int j = i - period + 1; j <= i; j++)
      {
         if(!IsFinite(src[j]) || !IsFinite(src[j - 1]))
         {
            validVol = false;
            break;
         }
         volatility += MathAbs(src[j] - src[j - 1]);
      }
      
      if(!validVol || volatility < EPS)
      {
         result[i] = kama; // No change
         continue;
      }
      
      double er = change / volatility;
      
      // Calculate smoothing constant
      double sc = er * (fastAlpha - slowAlpha) + slowAlpha;
      sc = sc * sc; // Square it
      
      // Update KAMA
      kama = kama + sc * (src[i] - kama);
      result[i] = kama;
   }
}

//+------------------------------------------------------------------+
//| T3 - Tillson T3                                                  |
//| Ultra-smooth low-lag moving average                              |
//| Warm-up: 3*period - 2                                            |
//+------------------------------------------------------------------+
void T3(const double &src[], int period, double &result[],
        double volumeFactor = 0.7, ENUM_SEED_MODE seed = SEED_SMA)
{
   int n = ArraySize(src);
   ArrayResize(result, n);
   ArrayInitialize(result, EMPTY_VALUE);
   
   if(period < 1 || volumeFactor < 0 || volumeFactor > 1)
      return;
   
   double b = volumeFactor;
   double b2 = b * b;
   double b3 = b2 * b;
   
   double c1 = -b3;
   double c2 = 3 * b2 + 3 * b3;
   double c3 = -6 * b2 - 3 * b - 3 * b3;
   double c4 = 1 + 3 * b + b3 + 3 * b2;
   
   // Calculate 6 cascaded EMAs
   double e1[], e2[], e3[], e4[], e5[], e6[];
   EMA(src, period, e1, seed);
   EMA(e1, period, e2, seed);
   EMA(e2, period, e3, seed);
   EMA(e3, period, e4, seed);
   EMA(e4, period, e5, seed);
   EMA(e5, period, e6, seed);
   
   for(int i = 0; i < n; i++)
   {
      if(IsFinite(e3[i]) && IsFinite(e4[i]) && 
         IsFinite(e5[i]) && IsFinite(e6[i]))
      {
         result[i] = c1 * e6[i] + c2 * e5[i] + c3 * e4[i] + c4 * e3[i];
      }
      else
      {
         result[i] = EMPTY_VALUE;
      }
   }
}

//+------------------------------------------------------------------+
//| End of MovingAverages.mqh                                         |
//+------------------------------------------------------------------+
