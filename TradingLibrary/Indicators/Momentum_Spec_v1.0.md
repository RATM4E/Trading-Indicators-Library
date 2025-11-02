# SharedLibrary/Indicators/Momentum — Specification (v1.0)

**Goal:** deterministic and cross-platform identical momentum and oscillator indicators for **cTrader (C#)** and **MT5 (MQL5)**, aligned with the `Core/` and `MovingAverages` rules.

---

## 0. Common Conventions

- **Indexing:** `0 … N-1` (0 = oldest).  
- **Numeric type:** `double`.  
- **NaN Policy:** incomplete or invalid data → `NaN`.  
- **Warm-up:** defined per indicator, equals window size or largest dependent subwindow.  
- **Output Ranges:** all normalized oscillators return values in **[0,1]** by default. Visual `[0,100]` scaling handled by UI layer.  
- **Batch + Stateful parity:** identical results (tolerance ±1e-12).  
- **Dependencies:** `MathBase`, `MovingAverages`, `PriceAction` (for typical prices, highs/lows), `Volatility/ATR` (for optional signal filters).

---

## 1) ROC & Momentum

### Purpose
Measure rate of change and raw momentum over N bars.

### Parameters
- `period: int ≥ 1`

### Inputs
- `src[]` (typically close)

### Outputs
- `ROC[] = (src/shift(src,period) - 1)`  
- `Momentum[] = src - shift(src,period)`

### Warm-up
- `period`

---

## 2) RSI (Relative Strength Index)

### Purpose
Classic momentum oscillator; compare up vs down moves.

### Parameters
- `period: int = 14`
- `mode: RsiMode = Wilder` (`Wilder`, `Cutler`, or `EMA`)

### Inputs
- `src[]`

### Outputs
- `RSI[] ∈ [0,1]`

### Warm-up
- `period`

### Variants
- `RSX(period=14)` — Ehlers smooth RSI (optional).

---

## 3) Stochastic Oscillator

### Purpose
Normalized momentum of price relative to high-low range.

### Parameters
- `kPeriod=14`, `kSmoothing=1`, `dPeriod=3`
- `AvgMode dAvg=SMA`
- `StochFlavor: Fast|Slow|Full`

### Inputs
- `high[], low[], close[]`

### Outputs
- `(k[], d[])`, both ∈ [0,1]

### Warm-up
- `max(kPeriod, kSmoothing, dPeriod)`

---

## 4) Stochastic RSI (StochRSI)

### Purpose
Apply stochastic normalization to RSI series.

### Parameters
- `rsiPeriod=14`, `stochPeriod=14`, `kSmoothing=3`, `dPeriod=3`

### Inputs
- `close[]`

### Outputs
- `(k[], d[])`, both ∈ [0,1]

### Warm-up
- `rsiPeriod + stochPeriod`

---

## 5) CCI (Commodity Channel Index)

### Purpose
Oscillator of price relative to its mean deviation.

### Parameters
- `period=20`
- `AvgMode ma=SMA`

### Inputs
- `typicalPrice[]` = (H+L+C)/3

### Outputs
- `CCI[]`, theoretically unbounded but scaled roughly [-3,+3] range.

### Warm-up
- `period`

---

## 6) Williams %R

### Purpose
Momentum oscillator measuring closing level relative to high-low range.

### Parameters
- `period=14`

### Inputs
- `high[], low[], close[]`

### Outputs
- `%R[] ∈ [-1,0]`  (can be scaled to [0,1] if needed)

### Warm-up
- `period`

---

## 7) SMI (Stochastic Momentum Index)

### Purpose
Enhanced Stochastic using distance from range midpoint.

### Parameters
- `period=14`
- `smoothing1=3`, `smoothing2=3`
- `AvgMode avg=EMA`

### Inputs
- `high[], low[], close[]`

### Outputs
- `(smi[], signal[])` both ∈ [-1,1]

### Warm-up
- `period + smoothing1 + smoothing2`

---

## 8) CMO (Chande Momentum Oscillator)

### Purpose
RSI-like oscillator using absolute sums of up/down changes.

### Parameters
- `period=14`

### Inputs
- `src[]`

### Outputs
- `CMO[] ∈ [-1,1]` (or scaled to [-100,100] for presentation)

### Warm-up
- `period`

---

## 9) Connors RSI (CRSI)

### Purpose
Short-term mean reversion oscillator combining three components:\n1. RSI of Price\n2. RSI of Up/Down Streak Lengths\n3. PercentRank of recent daily change

### Parameters
- `rsiPeriod=3`, `streakRsiPeriod=2`, `prPeriod=100`

### Inputs
- `close[]`

### Outputs
- `CRSI[] ∈ [0,1]`

### Warm-up
- `max(rsiPeriod, streakRsiPeriod, prPeriod)`

---

## 10) TSI (True Strength Index)

### Purpose
Double-smoothed momentum indicator with signal line.

### Parameters
- `longPeriod=25`, `shortPeriod=13`, `signalPeriod=7`
- `AvgMode signalAvg=EMA`

### Inputs
- `close[]`

### Outputs
- `(tsi[], signal[]) ∈ [-1,1]`

### Warm-up
- `longPeriod + shortPeriod + signalPeriod`

---

## 11) DPO (Detrended Price Oscillator)

### Purpose
Remove long-term trend component (causal form).

### Parameters
- `period=20`

### Inputs
- `close[]`

### Outputs
- `DPO[]`

### Warm-up
- `period`

### Notes
Causal version uses lag = floor(period/2)+1 and no future lookahead.

---

## 12) RVI (Relative Vigor Index)

### Purpose
Compare close–open vs high–low range to quantify bullish/bearish conviction.

### Parameters
- `period=10`
- `AvgMode avg=SMA`

### Inputs
- `open[], high[], low[], close[]`

### Outputs
- `(rvi[], signal[]) ∈ [-1,1]`

### Warm-up
- `period + signalPeriod (usually 4)`

---

## 13) Enumerations

```text
enum AvgMode { SMA, EMA, RMA, WMA }
enum RsiMode { Wilder, Cutler, EMA }
enum StochFlavor { Fast, Slow, Full }
```

---

## 14) C# and MQL5 Interfaces (no code)

### 14.1 Batch (C#)
```csharp
double[] ROC(double[] src, int period);
double[] Momentum(double[] src, int period);
double[] RSI(double[] src, int period=14, RsiMode mode=RsiMode.Wilder);
double[] RSX(double[] src, int period=14);
(double[] k, double[] d) Stochastic(double[] h, double[] l, double[] c, int kPeriod=14, int kSmoothing=1, int dPeriod=3, AvgMode dAvg=AvgMode.SMA, StochFlavor flavor=StochFlavor.Full);
(double[] k, double[] d) StochRSI(double[] close, int rsiPeriod=14, int stochPeriod=14, int kSmoothing=3, int dPeriod=3);
double[] CCI(double[] tp, int period=20, AvgMode ma=AvgMode.SMA);
double[] WilliamsR(double[] h, double[] l, double[] c, int period=14);
(double[] smi, double[] signal) SMI(double[] h, double[] l, double[] c, int period=14, int smoothing1=3, int smoothing2=3, AvgMode avg=AvgMode.EMA);
double[] CMO(double[] src, int period=14);
double[] ConnorsRSI(double[] close, int rsiPeriod=3, int streakRsiPeriod=2, int prPeriod=100);
(double[] tsi, double[] signal) TSI(double[] close, int longPeriod=25, int shortPeriod=13, int signalPeriod=7, AvgMode signalAvg=AvgMode.EMA);
double[] DPO(double[] close, int period=20);
(double[] rvi, double[] signal) RVI(double[] o, double[] h, double[] l, double[] c, int period=10, AvgMode avg=AvgMode.SMA);
```

### 14.2 Batch (MQL5)
```mqh
void ROC(const double &src[], const int period, double &out[]);
void Momentum(const double &src[], const int period, double &out[]);
void RSI(const double &src[], const int period, const int mode, double &out[]);
void RSX(const double &src[], const int period, double &out[]);
void Stochastic(const double &h[], const double &l[], const double &c[], const int kPeriod, const int kSmooth, const int dPeriod, const int avgMode, const int flavor, double &k[], double &d[]);
void StochRSI(const double &c[], const int rsiPeriod, const int stochPeriod, const int kSmooth, const int dPeriod, double &k[], double &d[]);
void CCI(const double &tp[], const int period, const int avgMode, double &out[]);
void WilliamsR(const double &h[], const double &l[], const double &c[], const int period, double &out[]);
void SMI(const double &h[], const double &l[], const double &c[], const int period, const int s1, const int s2, const int avgMode, double &smi[], double &signal[]);
void CMO(const double &src[], const int period, double &out[]);
void ConnorsRSI(const double &c[], const int rsiP, const int streakP, const int prP, double &out[]);
void TSI(const double &c[], const int longP, const int shortP, const int sigP, const int avgMode, double &tsi[], double &signal[]);
void DPO(const double &c[], const int period, double &out[]);
void RVI(const double &o[], const double &h[], const double &l[], const double &c[], const int period, const int avgMode, double &rvi[], double &signal[]);
```

---

## 15) Validation Checklist
- [x] Deterministic (Batch == Stateful)
- [x] Unified normalization (all [0,1] unless stated otherwise)
- [x] Identical floating results (±1e−12) on both platforms
- [x] Explicit warm-up and NaN policy
- [x] Seed rules inherited from MA (SMA-seed for RMA/EMA variants)

---

**Version:** 1.0  
**Author:** SharedLibrary Project  
**Status:** Approved for implementation  
