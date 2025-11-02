//+------------------------------------------------------------------+
//|                                                 PriceAction.mqh  |
//|                                   TradingLibrary.Core.PriceAction |
//|           Price action primitives and candle analysis for MT5     |
//+------------------------------------------------------------------+
#property copyright "TradingLibrary"
#property link      ""
#property version   "1.00"
#property strict

#include "MathBase.mqh"

//+------------------------------------------------------------------+
//| Price action primitives: candle geometry, True Range, DM,        |
//| volatility estimators, Heikin-Ashi, and swing detection.         |
//| Reference: SharedLibrary/Core specification - PriceAction        |
//|                                                                   |
//| Key principles:                                                   |
//| - All functions are stateless (batch)                            |
//| - NaN propagation: invalid inputs → EMPTY_VALUE outputs         |
//| - Index 0 → oldest bar                                          |
//| - TrueRange/DM require previous close → first bar = EMPTY_VALUE |
//+------------------------------------------------------------------+

//+------------------------------------------------------------------+
//| Candle Geometry                                                   |
//+------------------------------------------------------------------+

//--- Bar range: High - Low
double Range(double high, double low)
{
   if(!IsFinite(high) || !IsFinite(low))
      return EMPTY_VALUE;
   return high - low;
}

//--- Real body size: |Close - Open|
double RealBody(double open, double close)
{
   if(!IsFinite(open) || !IsFinite(close))
      return EMPTY_VALUE;
   return MathAbs(close - open);
}

//--- Upper wick: High - max(Open, Close)
double UpperWick(double open, double high, double close)
{
   if(!IsFinite(open) || !IsFinite(high) || !IsFinite(close))
      return EMPTY_VALUE;
   return high - MathMax(open, close);
}

//--- Lower wick: min(Open, Close) - Low
double LowerWick(double open, double low, double close)
{
   if(!IsFinite(open) || !IsFinite(low) || !IsFinite(close))
      return EMPTY_VALUE;
   return MathMin(open, close) - low;
}

//--- Body-to-range ratio
double BodyToRange(double open, double high, double low, double close)
{
   double body = RealBody(open, close);
   double range = Range(high, low);
   return SafeDivide(body, range);
}

//--- Checks if bar is bullish
bool IsBull(double open, double close)
{
   if(!IsFinite(open) || !IsFinite(close))
      return false;
   return close > open + EPS;
}

//--- Checks if bar is bearish
bool IsBear(double open, double close)
{
   if(!IsFinite(open) || !IsFinite(close))
      return false;
   return close < open - EPS;
}

//--- Checks if bar is doji
bool IsDoji(double open, double high, double low, double close, double maxBodyRatio = 0.1)
{
   double ratio = BodyToRange(open, high, low, close);
   if(!IsFinite(ratio))
      return false;
   return ratio <= maxBodyRatio;
}

//--- Checks if current bar is inside previous bar
bool IsInsideBar(double prevHigh, double prevLow, double currHigh, double currLow)
{
   if(!IsFinite(prevHigh) || !IsFinite(prevLow) ||
      !IsFinite(currHigh) || !IsFinite(currLow))
      return false;
   return currHigh <= prevHigh && currLow >= prevLow;
}

//--- Checks if current bar is outside previous bar
bool IsOutsideBar(double prevHigh, double prevLow, double currHigh, double currLow)
{
   if(!IsFinite(prevHigh) || !IsFinite(prevLow) ||
      !IsFinite(currHigh) || !IsFinite(currLow))
      return false;
   return currHigh > prevHigh && currLow < prevLow;
}

//--- Gap up detection
bool GapUp(double prevHigh, double currLow)
{
   if(!IsFinite(prevHigh) || !IsFinite(currLow))
      return false;
   return currLow > prevHigh + EPS;
}

//--- Gap down detection
bool GapDown(double prevLow, double currHigh)
{
   if(!IsFinite(prevLow) || !IsFinite(currHigh))
      return false;
   return currHigh < prevLow - EPS;
}

//--- Narrow Range bar (NRn)
bool IsNRn(const double &ranges[], int n = 4)
{
   int size = ArraySize(ranges);
   if(size < n)
      return false;
   
   int start = size - n;
   double currentRange = ranges[size - 1];
   
   if(!IsFinite(currentRange))
      return false;
   
   for(int i = start; i < size - 1; i++)
   {
      if(!IsFinite(ranges[i]))
         return false;
      if(ranges[i] <= currentRange)
         return false;
   }
   
   return true;
}

//--- Wide Range bar (WRn)
bool IsWRn(const double &ranges[], int n = 4)
{
   int size = ArraySize(ranges);
   if(size < n)
      return false;
   
   int start = size - n;
   double currentRange = ranges[size - 1];
   
   if(!IsFinite(currentRange))
      return false;
   
   for(int i = start; i < size - 1; i++)
   {
      if(!IsFinite(ranges[i]))
         return false;
      if(ranges[i] >= currentRange)
         return false;
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| Returns & Changes                                                 |
//+------------------------------------------------------------------+

//--- Simple price change
void Change(const double &prices[], double &result[])
{
   int n = ArraySize(prices);
   ArrayResize(result, n);
   result[0] = EMPTY_VALUE;
   
   for(int i = 1; i < n; i++)
   {
      if(!IsFinite(prices[i]) || !IsFinite(prices[i - 1]))
         result[i] = EMPTY_VALUE;
      else
         result[i] = prices[i] - prices[i - 1];
   }
}

//--- Percent change
void PercentChangeArray(const double &prices[], double &result[])
{
   int n = ArraySize(prices);
   ArrayResize(result, n);
   result[0] = EMPTY_VALUE;
   
   for(int i = 1; i < n; i++)
   {
      result[i] = PercentChange(prices[i], prices[i - 1]);
   }
}

//--- Log return
void LogReturn(const double &prices[], double &result[])
{
   int n = ArraySize(prices);
   ArrayResize(result, n);
   result[0] = EMPTY_VALUE;
   
   for(int i = 1; i < n; i++)
   {
      if(!IsFinite(prices[i]) || !IsFinite(prices[i - 1]))
         result[i] = EMPTY_VALUE;
      else if(prices[i - 1] <= 0 || prices[i] <= 0)
         result[i] = EMPTY_VALUE;
      else
         result[i] = MathLog(prices[i] / prices[i - 1]);
   }
}

//--- Cumulative return
double CumulativeReturn(const double &returns[], bool isLogReturns = false)
{
   int n = ArraySize(returns);
   if(n == 0)
      return EMPTY_VALUE;
   
   if(isLogReturns)
   {
      // Sum log returns
      double sum = 0;
      for(int i = 0; i < n; i++)
      {
         if(!IsFinite(returns[i]))
            return EMPTY_VALUE;
         sum += returns[i];
      }
      return MathExp(sum) - 1.0;
   }
   else
   {
      // Product of (1 + return)
      double product = 1.0;
      for(int i = 0; i < n; i++)
      {
         if(!IsFinite(returns[i]))
            return EMPTY_VALUE;
         product *= (1.0 + returns[i]);
      }
      return product - 1.0;
   }
}

//+------------------------------------------------------------------+
//| True Range & Directional Movement (Wilder)                       |
//+------------------------------------------------------------------+

//--- True Range batch version
void TrueRange(const double &high[], const double &low[], const double &close[], 
               double &result[])
{
   int n = ArraySize(high);
   if(ArraySize(low) != n || ArraySize(close) != n)
   {
      Print("Error: Arrays must have same length");
      return;
   }
   
   ArrayResize(result, n);
   result[0] = EMPTY_VALUE; // No previous close
   
   for(int i = 1; i < n; i++)
   {
      if(!IsFinite(high[i]) || !IsFinite(low[i]) || 
         !IsFinite(close[i]) || !IsFinite(close[i - 1]))
      {
         result[i] = EMPTY_VALUE;
         continue;
      }
      
      double tr1 = high[i] - low[i];
      double tr2 = MathAbs(high[i] - close[i - 1]);
      double tr3 = MathAbs(low[i] - close[i - 1]);
      result[i] = MathMax(tr1, MathMax(tr2, tr3));
   }
}

//--- Single bar True Range
double TrueRangeSingle(double high, double low, double close, double prevClose)
{
   if(!IsFinite(high) || !IsFinite(low) || 
      !IsFinite(close) || !IsFinite(prevClose))
      return EMPTY_VALUE;
   
   double tr1 = high - low;
   double tr2 = MathAbs(high - prevClose);
   double tr3 = MathAbs(low - prevClose);
   return MathMax(tr1, MathMax(tr2, tr3));
}

//--- Directional Movement Plus (+DM)
void DirectionalMovementPlus(const double &high[], const double &low[], 
                              double &result[])
{
   int n = ArraySize(high);
   if(ArraySize(low) != n)
   {
      Print("Error: Arrays must have same length");
      return;
   }
   
   ArrayResize(result, n);
   result[0] = EMPTY_VALUE;
   
   for(int i = 1; i < n; i++)
   {
      if(!IsFinite(high[i]) || !IsFinite(high[i - 1]) ||
         !IsFinite(low[i]) || !IsFinite(low[i - 1]))
      {
         result[i] = EMPTY_VALUE;
         continue;
      }
      
      double upMove = high[i] - high[i - 1];
      double downMove = low[i - 1] - low[i];
      
      if(upMove > downMove && upMove > 0)
         result[i] = upMove;
      else
         result[i] = 0;
   }
}

//--- Directional Movement Minus (-DM)
void DirectionalMovementMinus(const double &high[], const double &low[], 
                               double &result[])
{
   int n = ArraySize(high);
   if(ArraySize(low) != n)
   {
      Print("Error: Arrays must have same length");
      return;
   }
   
   ArrayResize(result, n);
   result[0] = EMPTY_VALUE;
   
   for(int i = 1; i < n; i++)
   {
      if(!IsFinite(high[i]) || !IsFinite(high[i - 1]) ||
         !IsFinite(low[i]) || !IsFinite(low[i - 1]))
      {
         result[i] = EMPTY_VALUE;
         continue;
      }
      
      double upMove = high[i] - high[i - 1];
      double downMove = low[i - 1] - low[i];
      
      if(downMove > upMove && downMove > 0)
         result[i] = downMove;
      else
         result[i] = 0;
   }
}

//+------------------------------------------------------------------+
//| OHLC Volatility Estimators                                        |
//+------------------------------------------------------------------+

//--- Parkinson volatility (per-bar variance)
double ParkinsonVariance(double high, double low)
{
   if(!IsFinite(high) || !IsFinite(low))
      return EMPTY_VALUE;
   if(high <= 0 || low <= 0 || high < low)
      return EMPTY_VALUE;
   
   double logHL = MathLog(high / low);
   return (logHL * logHL) / (4.0 * MathLog(2.0));
}

//--- Garman-Klass volatility (per-bar variance)
double GarmanKlassVariance(double open, double high, double low, double close)
{
   if(!IsFinite(open) || !IsFinite(high) || 
      !IsFinite(low) || !IsFinite(close))
      return EMPTY_VALUE;
   if(open <= 0 || high <= 0 || low <= 0 || close <= 0)
      return EMPTY_VALUE;
   
   double logHL = MathLog(high / low);
   double logCO = MathLog(close / open);
   return 0.5 * logHL * logHL - (2.0 * MathLog(2.0) - 1.0) * logCO * logCO;
}

//--- Rogers-Satchell volatility (per-bar variance)
double RogersSatchellVariance(double open, double high, double low, double close)
{
   if(!IsFinite(open) || !IsFinite(high) || 
      !IsFinite(low) || !IsFinite(close))
      return EMPTY_VALUE;
   if(open <= 0 || high <= 0 || low <= 0 || close <= 0)
      return EMPTY_VALUE;
   
   double logHC = MathLog(high / close);
   double logHO = MathLog(high / open);
   double logLC = MathLog(low / close);
   double logLO = MathLog(low / open);
   
   return logHC * logHO + logLC * logLO;
}

//--- Yang-Zhang Kinetic component
double YangZhangKinetic(double open, double high, double low, double close)
{
   return RogersSatchellVariance(open, high, low, close);
}

//+------------------------------------------------------------------+
//| Windowed High/Low & Donchian                                     |
//+------------------------------------------------------------------+

//--- Highest value in window
double Highest(const double &src[], int startIndex, int period)
{
   if(period < 1 || startIndex < 0)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(src))
      return EMPTY_VALUE;
   
   double maxVal = -DBL_MAX;
   for(int i = 0; i < period; i++)
   {
      double val = src[startIndex + i];
      if(!IsFinite(val))
         return EMPTY_VALUE;
      if(val > maxVal)
         maxVal = val;
   }
   
   return maxVal;
}

//--- Highest over most recent period
double HighestRecent(const double &src[], int period)
{
   int size = ArraySize(src);
   if(size < period)
      return EMPTY_VALUE;
   return Highest(src, size - period, period);
}

//--- Lowest value in window
double Lowest(const double &src[], int startIndex, int period)
{
   if(period < 1 || startIndex < 0)
      return EMPTY_VALUE;
   if(startIndex + period > ArraySize(src))
      return EMPTY_VALUE;
   
   double minVal = DBL_MAX;
   for(int i = 0; i < period; i++)
   {
      double val = src[startIndex + i];
      if(!IsFinite(val))
         return EMPTY_VALUE;
      if(val < minVal)
         minVal = val;
   }
   
   return minVal;
}

//--- Lowest over most recent period
double LowestRecent(const double &src[], int period)
{
   int size = ArraySize(src);
   if(size < period)
      return EMPTY_VALUE;
   return Lowest(src, size - period, period);
}

//--- Donchian Channel
void Donchian(const double &high[], const double &low[], int period,
              double &upper[], double &lower[], double &mid[])
{
   int n = ArraySize(high);
   if(ArraySize(low) != n || period < 1)
      return;
   
   ArrayResize(upper, n);
   ArrayResize(lower, n);
   ArrayResize(mid, n);
   ArrayInitialize(upper, EMPTY_VALUE);
   ArrayInitialize(lower, EMPTY_VALUE);
   ArrayInitialize(mid, EMPTY_VALUE);
   
   for(int i = period - 1; i < n; i++)
   {
      upper[i] = Highest(high, i - period + 1, period);
      lower[i] = Lowest(low, i - period + 1, period);
      
      if(IsFinite(upper[i]) && IsFinite(lower[i]))
         mid[i] = (upper[i] + lower[i]) / 2.0;
   }
}

//+------------------------------------------------------------------+
//| Swings & Fractals                                                 |
//+------------------------------------------------------------------+

//--- Swing high detection
bool IsSwingHigh(const double &high[], int index, int kLeft, int kRight)
{
   int n = ArraySize(high);
   if(index < kLeft || index + kRight >= n)
      return false;
   
   double pivotHigh = high[index];
   if(!IsFinite(pivotHigh))
      return false;
   
   // Check left side
   for(int i = index - kLeft; i < index; i++)
   {
      if(!IsFinite(high[i]))
         return false;
      if(high[i] >= pivotHigh)
         return false;
   }
   
   // Check right side
   for(int i = index + 1; i <= index + kRight; i++)
   {
      if(!IsFinite(high[i]))
         return false;
      if(high[i] > pivotHigh)
         return false;
   }
   
   return true;
}

//--- Swing low detection
bool IsSwingLow(const double &low[], int index, int kLeft, int kRight)
{
   int n = ArraySize(low);
   if(index < kLeft || index + kRight >= n)
      return false;
   
   double pivotLow = low[index];
   if(!IsFinite(pivotLow))
      return false;
   
   // Check left side
   for(int i = index - kLeft; i < index; i++)
   {
      if(!IsFinite(low[i]))
         return false;
      if(low[i] <= pivotLow)
         return false;
   }
   
   // Check right side
   for(int i = index + 1; i <= index + kRight; i++)
   {
      if(!IsFinite(low[i]))
         return false;
      if(low[i] < pivotLow)
         return false;
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| Heikin-Ashi Transform                                             |
//+------------------------------------------------------------------+

//--- Heikin-Ashi batch transformation
void HeikinAshi(const double &open[], const double &high[], const double &low[], 
                const double &close[], double &haOpen[], double &haHigh[], 
                double &haLow[], double &haClose[])
{
   int n = ArraySize(open);
   if(ArraySize(high) != n || ArraySize(low) != n || ArraySize(close) != n)
   {
      Print("Error: All OHLC arrays must have same length");
      return;
   }
   
   ArrayResize(haOpen, n);
   ArrayResize(haHigh, n);
   ArrayResize(haLow, n);
   ArrayResize(haClose, n);
   
   if(n == 0)
      return;
   
   // First bar - seed values
   if(!IsFinite(open[0]) || !IsFinite(high[0]) || 
      !IsFinite(low[0]) || !IsFinite(close[0]))
   {
      haOpen[0] = EMPTY_VALUE;
      haHigh[0] = EMPTY_VALUE;
      haLow[0] = EMPTY_VALUE;
      haClose[0] = EMPTY_VALUE;
   }
   else
   {
      haClose[0] = (open[0] + high[0] + low[0] + close[0]) / 4.0;
      haOpen[0] = (open[0] + close[0]) / 2.0;
      haHigh[0] = MathMax(high[0], MathMax(haOpen[0], haClose[0]));
      haLow[0] = MathMin(low[0], MathMin(haOpen[0], haClose[0]));
   }
   
   // Remaining bars - recursive calculation
   for(int i = 1; i < n; i++)
   {
      if(!IsFinite(open[i]) || !IsFinite(high[i]) || 
         !IsFinite(low[i]) || !IsFinite(close[i]) ||
         !IsFinite(haOpen[i - 1]) || !IsFinite(haClose[i - 1]))
      {
         haOpen[i] = EMPTY_VALUE;
         haHigh[i] = EMPTY_VALUE;
         haLow[i] = EMPTY_VALUE;
         haClose[i] = EMPTY_VALUE;
         continue;
      }
      
      haClose[i] = (open[i] + high[i] + low[i] + close[i]) / 4.0;
      haOpen[i] = (haOpen[i - 1] + haClose[i - 1]) / 2.0;
      haHigh[i] = MathMax(high[i], MathMax(haOpen[i], haClose[i]));
      haLow[i] = MathMin(low[i], MathMin(haOpen[i], haClose[i]));
   }
}

//+------------------------------------------------------------------+
//| End of PriceAction.mqh                                            |
//+------------------------------------------------------------------+
