# SharedLibrary/Indicators/Oscillators — Specification (v1.0)

**Goal:** deterministic, cross‑platform‑identical oscillators for **cTrader (C#)** and **MT5 (MQL5)**, aligned with `Core/` and `MovingAverages` rules. Contracts only (interfaces, parameters, warm‑up, NaN policy).

---

## 0) Common Conventions

- **Indexing:** `0 … N-1` (0 = oldest, N-1 = newest).  
- **Numeric type:** `double`.  
- **NaN policy:** any invalid/incomplete window ⇒ `NaN` output. No NaN‑skipping by default.  
- **Warm‑up:** defined per indicator; first valid bar = after required windows are complete.  
- **Batch vs Stateful parity:** results must match within ±1e‑12.  
- **Default input:** `close[]` unless stated otherwise.  
- **Signal smoothing:** via `MovingAverages` (seed = SMA(P)).  
- **Ranges:** most oscillators centered at **0**; Ultimate Oscillator in **[0,1]** (UI may scale to [0,100]).

### 0.1 Enums (shared)
```text
enum AvgMode { SMA, EMA, RMA, WMA }
```

---

## 1) MACD Family

### 1.1 MACD (classic)
**Definition:**  
- `fastLine = EMA(src, fast)`  
- `slowLine = EMA(src, slow)`  
- `macd = fastLine − slowLine`  
- `signal = EMA(macd, signalPeriod)` (configurable avg mode allowed)  
- `hist = macd − signal`

**Parameters:** `fast=12`, `slow=26`, `signal=9`, `signalAvg=EMA`  
**Inputs:** `src[]`  
**Outputs:** `(macd[], signal[], hist[])` (centered at 0)  
**Warm‑up:** `max(fast, slow) + signal`

**Interfaces:**  
C#: `(double[] macd, double[] signal, double[] hist) MACD(double[] src, int fast=12, int slow=26, int signal=9, AvgMode signalAvg=AvgMode.EMA);`  
MQL5: `void MACD(const double &src[], const int fast, const int slow, const int signal, const int signalAvg, double &macd[], double &signal[], double &hist[]);`

### 1.2 ZMACD (Zero‑Lag MACD)
**Definition:** use `ZLEMA` for fast/slow legs before differencing (reduces lag).  
**Parameters:** same as MACD.  
**Warm‑up:** `max(fast, slow) + signal` (+ZLEMA lag handled internally; initial values `NaN`).  
**Interfaces:** same shape as MACD.

### 1.3 MACD_From (generalized)
**Definition:** custom MA types for fast/slow/signal.  
**Interfaces:**  
C#: `(double[] macd, double[] signal, double[] hist) MACD_From(double[] src, int fast, int slow, int signal, AvgMode fastMA, AvgMode slowMA, AvgMode signalMA);`  
MQL5: analogous.

---

## 2) PPO / PO

### 2.1 PPO (Percent Price Oscillator)
**Definition:** `ppo = 100 * (EMA_fast − EMA_slow) / EMA_slow` (use `SafeDivide`).  
**Outputs:** `(ppo[], signal[], hist[])` (centered at 0; units = %)  
**Parameters:** `fast=12, slow=26, signal=9, signalAvg=EMA`  
**Warm‑up:** `max(fast, slow) + signal`  
**Interfaces:** shapes mirror MACD.

### 2.2 PO (Absolute Price Oscillator)
**Definition:** `po = EMA_fast − EMA_slow`  
**Outputs:** `(po[], signal[], hist[])`  
**Parameters/Warm‑up:** same as PPO.

---

## 3) Awesome Oscillator (AO) & Accelerator (AC)

### 3.1 AO
**Definition:** `AO = SMA(HL2,5) − SMA(HL2,34)` with `HL2 = (H+L)/2`.  
**Inputs:** `high[], low[]`  
**Output:** `ao[]`  
**Warm‑up:** `34`

### 3.2 AC
**Definition:** `AC = AO − SMA(AO,5)`  
**Inputs:** `high[], low[]` (or `ao[]` precomputed)  
**Output:** `ac[]`  
**Warm‑up:** `34 + 5 − 1`

**Interfaces:**  
C#: `double[] AO(double[] high, double[] low);` / `double[] AC(double[] high, double[] low);`  
MQL5: `void AO(const double &h[], const double &l[], double &out[]);` / `void AC(const double &h[], const double &l[], double &out[]);`

---

## 4) TRIX (Triple EMA ROC)

**Definition:** TRIX = rate of change of triple‑EMA of `src`; signal = MA(TRIX, signal).  
**Parameters:** `period=14`, `signal=9`, `AvgMode=EMA`  
**Inputs:** `src[]`  
**Outputs:** `(trix[], signal[])` (centered at 0)  
**Warm‑up:** `3*period − 2 + signal`

**Interfaces:**  
C#: `(double[] trix, double[] signal) TRIX(double[] src, int period=14, int signal=9, AvgMode avg=AvgMode.EMA);`  
MQL5: analogous.

---

## 5) PMO (Price Momentum Oscillator, DecisionPoint)

**Definition (contract):** ROC of price (period `rocPeriod`), then two EMA smoothings (`ema1`, `ema2`), optional scaling factor (display layer). Signal = EMA(PMO, `signal`).  
**Parameters:** `rocPeriod=1, ema1=10, ema2=14, signal=10`  
**Inputs:** `src[]`  
**Outputs:** `(pmo[], signal[])` (centered at 0)  
**Warm‑up:** `rocPeriod + ema1 + ema2 + signal`

**Interfaces:**  
C#: `(double[] pmo, double[] signal) PMO(double[] src, int rocPeriod=1, int ema1=10, int ema2=14, int signal=10);`  
MQL5: analogous.

---

## 6) KST (Know Sure Thing)

**Definition:** Weighted sum of four smoothed ROC components; signal = EMA(KST, signal).  
- For i∈{1..4}: `ROC_i = ROC(src, r_i)` → `SMO_i = SMA(ROC_i, s_i)`  
- `KST = Σ weights_i * SMO_i` (default weights = 1,2,3,4)

**Parameters:** `r1=10,r2=15,r3=20,r4=30; s1=10,s2=10,s3=10,s4=15; weights=(1,2,3,4); signal=9; AvgMode=EMA`  
**Inputs:** `src[]`  
**Outputs:** `(kst[], signal[])` (centered at 0)  
**Warm‑up:** `max(r_i + s_i) + signal`

**Interfaces:**  
C#: `(double[] kst, double[] signal) KST(double[] src, int r1=10,int r2=15,int r3=20,int r4=30, int s1=10,int s2=10,int s3=10,int s4=15, double w1=1,double w2=2,double w3=3,double w4=4, int signal=9, AvgMode avg=AvgMode.EMA);`  
MQL5: analogous.

---

## 7) Ultimate Oscillator (UO)

**Definition:** Weighted average of Buying Pressure / True Range over 3 windows.  
- `BP = close − min(low, prevClose)`  
- `TR = max(high, prevClose) − min(low, prevClose)`  
- `UO = (w1*Avg(BP/TR, p1) + w2*Avg(BP/TR, p2) + w3*Avg(BP/TR, p3)) / (w1+w2+w3)`

**Parameters:** `p1=7,p2=14,p3=28; w1=4,w2=2,w3=1`  
**Inputs:** `open(optional), high, low, close` (only H/L/C and prevClose are required)  
**Output:** `uo[] ∈ [0,1]`  
**Warm‑up:** `p3`

**Interfaces:**  
C#: `double[] Ultimate(double[] high, double[] low, double[] close, int p1=7, int p2=14, int p3=28, double w1=4, double w2=2, double w3=1);`  
MQL5: analogous.

---

## 8) Fisher Transform

**Definition:** Normalize `src` via stochastic scaling in `period` window, clamp to ±`clamp`, then apply Fisher transform; optional signal MA.  
**Parameters:** `period=10, signal=9, clamp=0.999`  
**Inputs:** `src[]` (default recommend HL2)  
**Outputs:** `(fisher[], signal[])` (centered at 0)  
**Warm‑up:** `period + signal`

**Interfaces:**  
C#: `(double[] fisher, double[] signal) Fisher(double[] src, int period=10, int signal=9, double clamp=0.999);`  
MQL5: analogous.

---

## 9) Validation & Determinism Checklist
- Batch == Stateful outputs within ±1e‑12 for same sequences.
- Warm‑up clearly defined; return `NaN` before first valid bar.
- ATR/MA dependencies follow Core seeds and α rules.
- No centered/look‑ahead computations inside Oscillators.
- PPO division uses `SafeDivide` to avoid false spikes.

---

## 10) API Summary

### C# batch
```csharp
(double[] macd, double[] signal, double[] hist) MACD(double[] src, int fast=12, int slow=26, int signal=9, AvgMode signalAvg=AvgMode.EMA);
(double[] macd, double[] signal, double[] hist) ZMACD(double[] src, int fast=12, int slow=26, int signal=9, AvgMode signalAvg=AvgMode.EMA);
(double[] macd, double[] signal, double[] hist) MACD_From(double[] src, int fast, int slow, int signal, AvgMode fastMA, AvgMode slowMA, AvgMode signalMA);
(double[] ppo, double[] ppoSignal, double[] ppoHist) PPO(double[] src, int fast=12, int slow=26, int signal=9, AvgMode signalAvg=AvgMode.EMA);
(double[] po, double[] poSignal, double[] poHist) PO(double[] src, int fast=12, int slow=26, int signal=9, AvgMode signalAvg=AvgMode.EMA);
double[] AO(double[] high, double[] low);
double[] AC(double[] high, double[] low);
(double[] trix, double[] signal) TRIX(double[] src, int period=14, int signal=9, AvgMode avg=AvgMode.EMA);
(double[] pmo, double[] pmoSignal) PMO(double[] src, int rocPeriod=1, int ema1=10, int ema2=14, int signal=10);
(double[] kst, double[] kstSignal) KST(double[] src, int r1=10,int r2=15,int r3=20,int r4=30, int s1=10,int s2=10,int s3=10,int s4=15, double w1=1,double w2=2,double w3=3,double w4=4, int signal=9, AvgMode avg=AvgMode.EMA);
double[] Ultimate(double[] high, double[] low, double[] close, int p1=7, int p2=14, int p3=28, double w1=4, double w2=2, double w3=1);
(double[] fisher, double[] signal) Fisher(double[] src, int period=10, int signal=9, double clamp=0.999);
```

### MQL5 batch
```mqh
void MACD(const double &src[], const int fast, const int slow, const int signal, const int signalAvg, double &macd[], double &signal[], double &hist[]);
void ZMACD(const double &src[], const int fast, const int slow, const int signal, const int signalAvg, double &macd[], double &signal[], double &hist[]);
void MACD_From(const double &src[], const int fast, const int slow, const int signal, const int fastMA, const int slowMA, const int signalMA, double &macd[], double &signal[], double &hist[]);
void PPO(const double &src[], const int fast, const int slow, const int signal, const int signalAvg, double &ppo[], double &ppoSignal[], double &ppoHist[]);
void PO(const double &src[], const int fast, const int slow, const int signal, const int signalAvg, double &po[], double &poSignal[], double &poHist[]);
void AO(const double &h[], const double &l[], double &out[]);
void AC(const double &h[], const double &l[], double &out[]);
void TRIX(const double &src[], const int period, const int signal, const int avgMode, double &trix[], double &signalOut[]);
void PMO(const double &src[], const int rocPeriod, const int ema1, const int ema2, const int signal, double &pmo[], double &signalOut[]);
void KST(const double &src[], const int r1, const int r2, const int r3, const int r4, const int s1, const int s2, const int s3, const int s4, const double w1, const double w2, const double w3, const double w4, const int signal, const int avgMode, double &kst[], double &signalOut[]);
void Ultimate(const double &h[], const double &l[], const double &c[], const int p1, const int p2, const int p3, const double w1, const double w2, const double w3, double &uo[]);
void Fisher(const double &src[], const int period, const int signal, const double clamp, double &fisher[], double &signalOut[]);
```

---

**Version:** 1.0  
**Status:** Approved for implementation
